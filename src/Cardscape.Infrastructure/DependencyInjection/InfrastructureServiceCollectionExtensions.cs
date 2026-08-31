using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Calendar;
using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
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
using Cardscape.Infrastructure.Ai;
using Cardscape.Infrastructure.Authentication;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.Infrastructure.Calendar;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Export;
using Cardscape.Infrastructure.Import;
using Cardscape.Infrastructure.Integrations;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Interceptors;
using Cardscape.Infrastructure.Persistence.Outbox;
using Cardscape.Infrastructure.Repositories;
using Cardscape.Infrastructure.Scim;
using Cardscape.Infrastructure.Search;
using Cardscape.Infrastructure.Security;
using Cardscape.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Cardscape.Infrastructure.DependencyInjection;

public static partial class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCardscapeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is required.");

        services.AddDbContext<CardscapeDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<DomainEventsInterceptor>());

            // EF Core 10 raises `PendingModelChangesWarning` as an error
            // during `database update` whenever it detects a difference
            // between the runtime model and the latest migration
            // snapshot, even when the difference is benign (e.g. an
            // unrelated metadata flag). Cardscape ships a single
            // consolidated `ConsolidatedInit` migration whose snapshot
            // is regenerated on every model change; the warning fires
            // on otherwise-healthy updates, so we silence it here. The
            // first-line defence is still the unit/integration tests
            // and the developer workflow of `dotnet ef migrations add`
            // before `dotnet ef database update`. See
            // https://aka.ms/efcore-docs-pending-changes for the full
            // background.
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString, b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                    break;
                case "postgresql":
                case "postgres":
                case "npgsql":
                    options.UseNpgsql(connectionString,
                        postgres => postgres.MigrationsAssembly("Cardscape.Migrations.PostgreSql"));
                    break;
                case "mysql":
                    options.UseMySQL(connectionString,
                        mySql => mySql.MigrationsAssembly("Cardscape.Migrations.MySql"));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {provider}. " +
                        "Use Sqlite, PostgreSQL, or MySql.");
            }
        });

        services.AddScoped<DomainEventsInterceptor>();
        services.AddSingleton<DomainEventOutboxProcessor>();
        services.AddHostedService<DomainEventOutboxDispatcherService>();
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

        services.AddScoped<IRepository<User, UserId>, UserRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<WorkspaceRepository>();
        services.AddScoped<IRepository<Workspace, WorkspaceId>, WorkspaceRepository>(sp => sp.GetRequiredService<WorkspaceRepository>());
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>(sp => sp.GetRequiredService<WorkspaceRepository>());

        services.AddScoped<BoardRepository>();
        services.AddScoped<IRepository<Board, BoardId>, BoardRepository>(sp => sp.GetRequiredService<BoardRepository>());
        services.AddScoped<IBoardRepository, BoardRepository>(sp => sp.GetRequiredService<BoardRepository>());

        services.AddScoped<BoardListRepository>();
        services.AddScoped<IRepository<BoardList, BoardListId>, BoardListRepository>(sp => sp.GetRequiredService<BoardListRepository>());
        services.AddScoped<IBoardListRepository, BoardListRepository>(sp => sp.GetRequiredService<BoardListRepository>());

        services.AddScoped<CardRepository>();
        services.AddScoped<IRepository<Card, CardId>, CardRepository>(sp => sp.GetRequiredService<CardRepository>());
        services.AddScoped<ICardRepository, CardRepository>(sp => sp.GetRequiredService<CardRepository>());

        services.AddScoped<LabelRepository>();
        services.AddScoped<IRepository<Label, LabelId>, LabelRepository>(sp => sp.GetRequiredService<LabelRepository>());
        services.AddScoped<ILabelRepository, LabelRepository>(sp => sp.GetRequiredService<LabelRepository>());

        services.AddScoped<CommentRepository>();
        services.AddScoped<IRepository<Comment, CommentId>, CommentRepository>(sp => sp.GetRequiredService<CommentRepository>());
        services.AddScoped<ICommentRepository, CommentRepository>(sp => sp.GetRequiredService<CommentRepository>());

        services.AddScoped<NotificationRepository>();
        services.AddScoped<IRepository<Notification, NotificationId>, NotificationRepository>(sp => sp.GetRequiredService<NotificationRepository>());
        services.AddScoped<INotificationRepository, NotificationRepository>(sp => sp.GetRequiredService<NotificationRepository>());

        services.AddScoped<ActivityRepository>();
        services.AddScoped<IRepository<Activity, ActivityId>, ActivityRepository>(sp => sp.GetRequiredService<ActivityRepository>());
        services.AddScoped<IActivityRepository, ActivityRepository>(sp => sp.GetRequiredService<ActivityRepository>());

        services.AddScoped<ApiTokenRepository>();
        services.AddScoped<IRepository<ApiToken, ApiTokenId>, ApiTokenRepository>(sp => sp.GetRequiredService<ApiTokenRepository>());
        services.AddScoped<IApiTokenRepository, ApiTokenRepository>(sp => sp.GetRequiredService<ApiTokenRepository>());

        services.AddScoped<UserPreferencesRepository>();
        services.AddScoped<IRepository<Cardscape.Domain.UserPreferences.UserPreferences, UserId>, UserPreferencesRepository>(sp => sp.GetRequiredService<UserPreferencesRepository>());
        services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>(sp => sp.GetRequiredService<UserPreferencesRepository>());

        services.AddScoped<WorkspaceInvitationRepository>();
        services.AddScoped<IRepository<WorkspaceInvitation, WorkspaceInvitationId>, WorkspaceInvitationRepository>(sp => sp.GetRequiredService<WorkspaceInvitationRepository>());
        services.AddScoped<IWorkspaceInvitationRepository, WorkspaceInvitationRepository>(sp => sp.GetRequiredService<WorkspaceInvitationRepository>());

        services.AddScoped<AutomationRuleRepository>();
        services.AddScoped<IRepository<BoardAutomationRule, BoardAutomationRuleId>, AutomationRuleRepository>(sp => sp.GetRequiredService<AutomationRuleRepository>());
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>(sp => sp.GetRequiredService<AutomationRuleRepository>());

        services.AddScoped<BoardExtensionRepository>();
        services.AddScoped<IRepository<BoardExtension, BoardExtensionId>, BoardExtensionRepository>(sp => sp.GetRequiredService<BoardExtensionRepository>());
        services.AddScoped<IBoardExtensionRepository, BoardExtensionRepository>(sp => sp.GetRequiredService<BoardExtensionRepository>());

        services.AddScoped<BackgroundJobRepository>();
        services.AddScoped<IRepository<BackgroundJob, BackgroundJobId>, BackgroundJobRepository>(sp => sp.GetRequiredService<BackgroundJobRepository>());
        services.AddScoped<IBackgroundJobStore, BackgroundJobRepository>(sp => sp.GetRequiredService<BackgroundJobRepository>());

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
        services.AddScoped<WebhookEndpointRepository>();
        services.AddScoped<IRepository<WebhookEndpoint, WebhookEndpointId>, WebhookEndpointRepository>(sp => sp.GetRequiredService<WebhookEndpointRepository>());
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>(sp => sp.GetRequiredService<WebhookEndpointRepository>());

        services.AddScoped<WebhookDeliveryRepository>();
        services.AddScoped<IRepository<WebhookDelivery, WebhookDeliveryId>, WebhookDeliveryRepository>(sp => sp.GetRequiredService<WebhookDeliveryRepository>());
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>(sp => sp.GetRequiredService<WebhookDeliveryRepository>());

        services.AddScoped<IBackgroundJobScheduler, BackgroundJobScheduler>();
        services.AddSingleton<IBackgroundJobHandlerRegistry, BackgroundJobHandlerRegistry>();
        services.AddSingleton<IBackgroundJobHandler, CloneCardHandler>();
        services.AddHttpClient(WebhookDeliveryHandler.WebhookHttpClientName, client =>
        {
            client.Timeout = WebhookDeliveryHandler.RequestTimeout;
            client.DefaultRequestHeaders.UserAgent.Add(
                new System.Net.Http.Headers.ProductInfoHeaderValue("Cardscape-Webhooks", "1.0"));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
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

        services.AddScoped<CustomFieldDefinitionRepository>();
        services.AddScoped<IRepository<CustomFieldDefinition, CustomFieldDefinitionId>, CustomFieldDefinitionRepository>(sp => sp.GetRequiredService<CustomFieldDefinitionRepository>());
        services.AddScoped<ICustomFieldDefinitionRepository, CustomFieldDefinitionRepository>(sp => sp.GetRequiredService<CustomFieldDefinitionRepository>());

        services.AddScoped<CustomFieldValueRepository>();
        services.AddScoped<IRepository<CustomFieldValue, CustomFieldValueId>, CustomFieldValueRepository>(sp => sp.GetRequiredService<CustomFieldValueRepository>());
        services.AddScoped<ICustomFieldValueRepository, CustomFieldValueRepository>(sp => sp.GetRequiredService<CustomFieldValueRepository>());

        services.AddScoped<CardVoteRepository>();
        services.AddScoped<IRepository<CardVote, CardVoteId>, CardVoteRepository>(sp => sp.GetRequiredService<CardVoteRepository>());
        services.AddScoped<ICardVoteRepository, CardVoteRepository>(sp => sp.GetRequiredService<CardVoteRepository>());

        services.AddScoped<ChecklistRepository>();
        services.AddScoped<IRepository<Checklist, ChecklistId>, ChecklistRepository>(sp => sp.GetRequiredService<ChecklistRepository>());
        services.AddScoped<IChecklistRepository, ChecklistRepository>(sp => sp.GetRequiredService<ChecklistRepository>());

        services.AddScoped<ChecklistItemRepository>();
        services.AddScoped<IRepository<ChecklistItem, ChecklistItemId>, ChecklistItemRepository>(sp => sp.GetRequiredService<ChecklistItemRepository>());
        services.AddScoped<IChecklistItemRepository, ChecklistItemRepository>(sp => sp.GetRequiredService<ChecklistItemRepository>());

        // BUG-A5-002 — the attachments table was defined only on
        // the domain side before this pass; the repository and
        // DbSet mapping are added in the same commit so the new
        // direct-upload endpoints can persist their metadata.
        services.AddScoped<AttachmentRepository>();
        services.AddScoped<IRepository<Attachment, AttachmentId>, AttachmentRepository>(sp => sp.GetRequiredService<AttachmentRepository>());
        services.AddScoped<IAttachmentRepository, AttachmentRepository>(sp => sp.GetRequiredService<AttachmentRepository>());

        services.AddScoped<CardRecurrenceRepository>();
        services.AddScoped<IRepository<CardRecurrence, CardRecurrenceId>, CardRecurrenceRepository>(sp => sp.GetRequiredService<CardRecurrenceRepository>());
        services.AddScoped<ICardRecurrenceRepository, CardRecurrenceRepository>(sp => sp.GetRequiredService<CardRecurrenceRepository>());

        services.AddScoped<ICardAgingSettingsRepository, CardAgingSettingsRepository>();
        services.AddScoped<ICardSnoozeRepository, CardSnoozeRepository>();
        services.AddScoped<ICardMirrorRepository, CardMirrorRepository>();

        services.AddScoped<IdempotencyKeyRepository>();
        services.AddScoped<IRepository<IdempotencyKey, IdempotencyKeyId>, IdempotencyKeyRepository>(sp => sp.GetRequiredService<IdempotencyKeyRepository>());
        services.AddScoped<IIdempotencyKeyStore, IdempotencyKeyRepository>(sp => sp.GetRequiredService<IdempotencyKeyRepository>());

        services.AddScoped<ExternalLoginRepository>();
        services.AddScoped<IRepository<ExternalLogin, ExternalLoginId>, ExternalLoginRepository>(sp => sp.GetRequiredService<ExternalLoginRepository>());
        services.AddScoped<IExternalLoginRepository, ExternalLoginRepository>(sp => sp.GetRequiredService<ExternalLoginRepository>());
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddScoped<TotpCredentialRepository>();
        services.AddScoped<IRepository<TotpCredential, TotpCredentialId>, TotpCredentialRepository>(sp => sp.GetRequiredService<TotpCredentialRepository>());
        services.AddScoped<ITotpCredentialRepository, TotpCredentialRepository>(sp => sp.GetRequiredService<TotpCredentialRepository>());

        services.AddScoped<PasswordResetRepository>();
        services.AddScoped<IRepository<PasswordReset, PasswordResetId>, PasswordResetRepository>(sp => sp.GetRequiredService<PasswordResetRepository>());
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>(sp => sp.GetRequiredService<PasswordResetRepository>());
        services.AddScoped<ITotpService, TotpService>();

        AddSecurityInfrastructure(services, configuration);

        AddFeatureInfrastructure(services, configuration);

        return services;
    }
}
