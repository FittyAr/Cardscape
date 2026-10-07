using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Realtime;
using Cardscape.Application.Webhooks;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Attachments;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.PasswordResets;
using Cardscape.Domain.Authentication.Totp;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Common;
using Cardscape.Domain.Idempotency;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Cardscape.Domain.Recurrence;
using Cardscape.Domain.Security;
using Cardscape.Domain.Voting;
using Cardscape.Domain.Webhooks;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Authentication;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Inbox;
using Cardscape.Infrastructure.Persistence.Interceptors;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.Infrastructure.DependencyInjection;

public static partial class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCardscapeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        DatabaseProvider provider = DatabaseProvider.Parse(configuration["Database:Provider"]);
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required.");

        services.AddDbContext<CardscapeDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<DomainEventsInterceptor>());
            // Enforce alignment explicitly: provider defaults differ (ADR 0013).
            options.ConfigureWarnings(w => w.Throw(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            options.UseCardscapeDatabase(provider, connectionString);
        });

        services.AddScoped<DomainEventsInterceptor>();
        services.AddSingleton<DomainEventOutboxProcessor>();
        services.AddHostedService<DomainEventOutboxDispatcherService>();
        services.AddScoped<IExternalMessageInbox, ExternalMessageInbox>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Domain-event fan-out. Three broadcasters run
        // side-by-side: BoardEventBroadcaster pushes the
        // realtime update (SignalR + MCP resource
        // subscription); WebhookEventBroadcaster queues the
        // matching webhook deliveries on the background-job
        // scheduler; SlackEventBroadcaster mirrors the same
        // four events to subscribed Slack channels. All three
        // are singletons because they create a fresh
        // IServiceScope per event — the EF Core repositories
        // they resolve (scoped) cannot live on a singleton
        // directly.
        services.AddSingleton<IDomainEventBroadcaster, BoardEventBroadcaster>();
        services.AddSingleton<IDomainEventBroadcaster, WebhookEventBroadcaster>();
        services.AddSingleton<
            IDomainEventBroadcaster,
            Cardscape.Application.Integrations.Slack.SlackEventBroadcaster>();
        // BETA-A7-001 — see test-results/beta/reports/A7-advanced.md.
        // The previous AutomationDispatcher sat in the API
        // project as a static class whose four `Handle` methods
        // were meant to be discovered by Wolverine. They never
        // ran: the card events do not implement IMessage, so
        // Wolverine's static-handler discovery skips them, and
        // there is no manual subscription in Program.cs. The
        // rules created via the API persisted correctly but
        // were never executed. Converted to a proper
        // IDomainEventBroadcaster so the durable outbox fan-out picks it up.
        services.AddSingleton<
            IDomainEventBroadcaster,
            Cardscape.Application.Automation.AutomationEventBroadcaster>();

        services.AddRepository<UserRepository, User, UserId, IUserRepository>();

        services.AddRepository<WorkspaceRepository, Workspace, WorkspaceId, IWorkspaceRepository>();

        services.AddRepository<BoardRepository, Board, BoardId, IBoardRepository>();

        services.AddRepository<BoardListRepository, BoardList, BoardListId, IBoardListRepository>();

        services.AddRepository<CardRepository, Card, CardId, ICardRepository>();

        services.AddRepository<LabelRepository, Label, LabelId, ILabelRepository>();

        services.AddRepository<CommentRepository, Comment, CommentId, ICommentRepository>();

        services.AddRepository<NotificationRepository, Notification, NotificationId, INotificationRepository>();

        services.AddRepository<ActivityRepository, Activity, ActivityId, IActivityRepository>();

        services.AddRepository<ApiTokenRepository, ApiToken, ApiTokenId, IApiTokenRepository>();

        services.AddRepository<UserPreferencesRepository, Cardscape.Domain.UserPreferences.UserPreferences, UserId, IUserPreferencesRepository>();

        services.AddRepository<WorkspaceInvitationRepository, WorkspaceInvitation, WorkspaceInvitationId, IWorkspaceInvitationRepository>();

        services.AddRepository<AutomationRuleRepository, BoardAutomationRule, BoardAutomationRuleId, IAutomationRuleRepository>();

        services.AddRepository<BoardExtensionRepository, BoardExtension, BoardExtensionId, IBoardExtensionRepository>();

        services.AddRepository<BackgroundJobRepository, BackgroundJob, BackgroundJobId, IBackgroundJobStore>();

        // BETA-4-#1 — see test-results/BETA-TEST-REPORT.md.
        //
        // The repository + the broadcaster were both in place
        // (Infrastructure/Repositories/WebhookEndpointRepository.cs
        // + Application/Webhooks/WebhookEventBroadcaster.cs) but
        // the DI line was missing, so every domain event that
        // needed to fan out to webhooks threw
        // InvalidOperationException("No service for type
        // IWebhookEndpointRepository has been registered") and
        // surfaced as a 500. The unit tests passed because they
        // mock the broadcaster; the smoke test caught it because
        // 50 concurrent board mutations each triggered a
        // BoardUpdated domain event. The same fix applies to
        // WebhookDeliveryRepository — both repositories sit
        // behind the broadcaster and both were unregistered.
        services.AddRepository<WebhookEndpointRepository, WebhookEndpoint, WebhookEndpointId, IWebhookEndpointRepository>();

        services.AddRepository<WebhookDeliveryRepository, WebhookDelivery, WebhookDeliveryId, IWebhookDeliveryRepository>();

        services.AddScoped<IBackgroundJobScheduler, BackgroundJobScheduler>();
        services.AddSingleton<IBackgroundJobHandlerRegistry, BackgroundJobHandlerRegistry>();
        services.AddSingleton<IBackgroundJobHandler, CloneCardHandler>();
        services.AddHttpClient(WebhookDeliveryHandler.WebhookHttpClientName, client =>
        {
            client.Timeout = WebhookDeliveryHandler.RequestTimeout;
            client.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("Cardscape-Webhooks", "1.0"));
        }).WithoutAutoRedirect();
        // BETA-A7-009 — see test-results/beta/reports/A7-advanced.md.
        // The webhook delivery handler is responsible for POSTing the
        // queued payload to the user's endpoint with the HMAC-SHA256
        // signature, and for marking the WebhookDelivery row as
        // Success / Failed / DeadLettered. Without this DI line the
        // BackgroundJobDispatcherService claims the job, the
        // ExecuteBackgroundJobCommandHandler tries to resolve the
        // handler by type from the registry, gets null, and the
        // delivery stays Pending forever (status=0, attemptCount=0).
        services.AddSingleton<IBackgroundJobHandler, WebhookDeliveryHandler>();
        services.AddScoped<IUserDataExportService, UserDataExportService>();
        services.AddSingleton<ISystemSettingsService, Cardscape.Infrastructure.Settings.SystemSettingsService>();

        // GDPR retention sweeper (Art. 5(1)(e), Art. 17).
        // The sweeper is a periodic background service
        // that anonymises soft-deleted users past the
        // grace period and purges the activity feed +
        // audit log per the configured retention. The
        // host picks it up via AddHostedService.
        services.AddOptions<Cardscape.Infrastructure.Hosting.RetentionSettingsOptions>()
            .Bind(configuration.GetSection(Cardscape.Infrastructure.Hosting.RetentionSettingsOptions.SectionName))
            .Validate(
                options => options.SweepIntervalSeconds > 0
                    && options.UserGracePeriodDays >= 0
                    && options.ActivityRetentionDays > 0
                    && options.AuditRetentionDays > 0
                    && options.BatchSize > 0,
                "Retention settings require a positive sweep interval, retention periods and batch size; the user grace period cannot be negative.")
            .ValidateOnStart();
        services.AddHostedService<Cardscape.Infrastructure.Hosting.RetentionSweeper>();

        // JWT revocation sweeper. Drops every
        // revoked-token row whose TokenExpiresAt is
        // in the past so the validation hot path
        // (JwtRevocationValidator) stays sub-millisecond
        // regardless of how many revocations the
        // system has ever recorded.
        services.AddOptions<Cardscape.Infrastructure.Hosting.RevocationSweeperOptions>()
            .Bind(configuration.GetSection(Cardscape.Infrastructure.Hosting.RevocationSweeperOptions.SectionName))
            .Validate(
                options => options.SweepInterval > TimeSpan.Zero
                    && options.InitialDelay >= TimeSpan.Zero,
                "Revocation sweeper requires a positive sweep interval and a non-negative initial delay.")
            .ValidateOnStart();
        services.AddHostedService<Cardscape.Infrastructure.Hosting.RevocationSweeper>();

        services.AddRepository<CustomFieldDefinitionRepository, CustomFieldDefinition, CustomFieldDefinitionId, ICustomFieldDefinitionRepository>();

        services.AddRepository<CustomFieldValueRepository, CustomFieldValue, CustomFieldValueId, ICustomFieldValueRepository>();

        services.AddRepository<CardVoteRepository, CardVote, CardVoteId, ICardVoteRepository>();

        services.AddRepository<ChecklistRepository, Checklist, ChecklistId, IChecklistRepository>();

        services.AddRepository<ChecklistItemRepository, ChecklistItem, ChecklistItemId, IChecklistItemRepository>();

        // BUG-A5-002 — the attachments table was defined only on
        // the domain side before this pass; the repository and
        // DbSet mapping are added in the same commit so the new
        // direct-upload endpoints can persist their metadata.
        services.AddRepository<AttachmentRepository, Attachment, AttachmentId, IAttachmentRepository>();

        services.AddRepository<CardRecurrenceRepository, CardRecurrence, CardRecurrenceId, ICardRecurrenceRepository>();

        services.AddScoped<ICardAgingSettingsRepository, CardAgingSettingsRepository>();
        services.AddScoped<ICardSnoozeRepository, CardSnoozeRepository>();
        services.AddScoped<ICardMirrorRepository, CardMirrorRepository>();

        services.AddRepository<IdempotencyKeyRepository, IdempotencyKey, IdempotencyKeyId, IIdempotencyKeyStore>();

        services.AddRepository<ExternalLoginRepository, ExternalLogin, ExternalLoginId, IExternalLoginRepository>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddRepository<TotpCredentialRepository, TotpCredential, TotpCredentialId, ITotpCredentialRepository>();

        services.AddRepository<PasswordResetRepository, PasswordReset, PasswordResetId, IPasswordResetRepository>();
        services.AddScoped<ITotpService, TotpService>();

        AddSecurityInfrastructure(services, configuration);

        AddFeatureInfrastructure(services, configuration);

        return services;
    }

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <typeparamref name="TRepository"/> once per scope and
        /// forwards both the generic <see cref="IRepository{TAggregate, TId}"/>
        /// and the aggregate-specific <typeparamref name="TContract"/> to that
        /// same instance, so a handler that asks for either shares one change
        /// tracker view.
        /// </summary>
        private IServiceCollection AddRepository<TRepository, TAggregate, TId, TContract>()
            where TRepository : class, IRepository<TAggregate, TId>, TContract
            where TAggregate : Entity<TId>
            where TId : notnull
            where TContract : class
        {
            services.AddScoped<TRepository>();
            services.AddScoped<IRepository<TAggregate, TId>>(sp => sp.GetRequiredService<TRepository>());
            services.AddScoped<TContract>(sp => sp.GetRequiredService<TRepository>());
            return services;
        }
    }
}
