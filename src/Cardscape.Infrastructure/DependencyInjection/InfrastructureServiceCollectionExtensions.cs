using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Realtime;
using Cardscape.Application.Webhooks;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Authentication.ExternalLogins;
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
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Ai;
using Cardscape.Infrastructure.Authentication;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.Infrastructure.Calendar;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Infrastructure.Email;
using Cardscape.Infrastructure.Export;
using Cardscape.Infrastructure.Import;
using Cardscape.Infrastructure.Integrations;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Interceptors;
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

namespace Cardscape.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
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

            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString, b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                    break;
                case "postgresql":
                case "postgres":
                case "npgsql":
                    options.UseNpgsql(connectionString, b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                    break;
                case "mariadb":
                case "mysql":
                    options.UseMySQL(connectionString, b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported database provider: {provider}. " +
                        "Use Sqlite, PostgreSQL, or MariaDB.");
            }
        });

        services.AddScoped<DomainEventsInterceptor>();
        services.AddScoped<IDomainEventDispatcher, WolverineDomainEventDispatcher>();
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

        services.AddScoped<IBackgroundJobScheduler, BackgroundJobScheduler>();
        services.AddSingleton<IBackgroundJobHandlerRegistry, BackgroundJobHandlerRegistry>();
        services.AddSingleton<IBackgroundJobHandler, CloneCardHandler>();
        services.AddScoped<IUserDataExportService, UserDataExportService>();

        // GDPR retention sweeper (Art. 5(1)(e), Art. 17).
        // The sweeper is a periodic background service
        // that anonymises soft-deleted users past the
        // grace period and purges the activity feed +
        // audit log per the configured retention. The
        // host picks it up via AddHostedService.
        services.Configure<Cardscape.Infrastructure.Hosting.RetentionSettingsOptions>(
            configuration.GetSection(Cardscape.Infrastructure.Hosting.RetentionSettingsOptions.SectionName));
        services.AddSingleton<Cardscape.Infrastructure.Hosting.IRetentionSettings,
            Cardscape.Infrastructure.Hosting.RetentionSettings>();
        services.AddHostedService<Cardscape.Infrastructure.Hosting.RetentionSweeper>();

        // JWT revocation sweeper. Drops every
        // revoked-token row whose TokenExpiresAt is
        // in the past so the validation hot path
        // (JwtRevocationValidator) stays sub-millisecond
        // regardless of how many revocations the
        // system has ever recorded.
        services.Configure<Cardscape.Infrastructure.Hosting.RevocationSweeperOptions>(
            configuration.GetSection(Cardscape.Infrastructure.Hosting.RevocationSweeperOptions.SectionName));
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
        services.AddScoped<ITotpService, TotpService>();

        // Two-step 2FA login: the password check mints a one-shot
        // PendingTotpToken; the /api/auth/login/totp endpoint consumes
        // it. Singleton because the store is in-memory and shared by
        // every request.
        services.AddSingleton<Cardscape.Application.Authentication.Abstractions.IPendingTotpLoginStore, InMemoryPendingTotpLoginStore>();

        // 2FA secret encryption: protected with the same
        // ASP.NET Core data-protection ring the rest of the
        // app uses (Cookie + antiforgery + now TOTP secrets).
        services.AddDataProtection();
        services.AddSingleton<IDataProtector>(sp =>
        {
            var provider = sp.GetRequiredService<IDataProtectionProvider>();
            return provider.CreateProtector("Cardscape.Secrets.v1");
        });
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        // Identity-shaped repositories (a few extra generics to satisfy the IRepository
        // contract for non-aggregate roots). The Application layer depends only on the
        // typed interfaces above.

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<
            Cardscape.Application.Abstractions.Persistence.IRevokedTokenRepository,
            Cardscape.Infrastructure.Repositories.RevokedTokenRepository>();
        services.AddSingleton<IRateLimiter, RateLimiter>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

        // OAuth 2.0 / OIDC for third-party apps. The repos are
        // scoped (they wrap the EF Core DbContext); the service
        // is scoped because it composes the repos + a clock.
        services.AddScoped<IOAuthAppRepository, OAuthAppRepository>();
        services.AddScoped<IOAuthAuthorizationCodeRepository, OAuthAuthorizationCodeRepository>();
        services.AddScoped<IOAuthAccessTokenRepository, OAuthAccessTokenRepository>();
        services.AddScoped<IOAuthAppService, OAuthAppService>();

        // Import pipeline (Trello default implementation; other kanban
        // tools can plug in their own IImportService). The import is
        // fully scoped because the work touches the cardscape DB
        // through the standard UnitOfWork pipeline.
        services.AddScoped<IImportService, TrelloImportService>();
        services.AddSingleton<IEmailService, ConsoleEmailService>();
        services.AddSingleton<IInvitationEmailService, ConsoleInvitationEmailService>();
        services.AddSingleton<ISearchIndex, InMemorySearchIndex>();

        // AI provider (Cardscape AI). The choice is configuration-driven:
        //   Ai:Provider = RuleBased         → deterministic templates, no network (default)
        //   Ai:Provider = OpenAiCompatible  → posts to a /v1/chat/completions endpoint
        string aiProvider = configuration["Ai:Provider"] ?? "RuleBased";
        services.Configure<AiProviderOptions>(configuration.GetSection("Ai"));
        if (aiProvider.Equals("OpenAiCompatible", StringComparison.OrdinalIgnoreCase))
        {
            string? endpoint = configuration["Ai:Endpoint"]
                ?? throw new InvalidOperationException(
                    "Ai:Endpoint is required when Ai:Provider is OpenAiCompatible.");
            services.AddHttpClient<IAiService, OpenAiCompatibleAiService>(client =>
            {
                client.BaseAddress = new Uri(endpoint);
                client.Timeout = TimeSpan.FromSeconds(60);
            });
        }
        else
        {
            services.AddSingleton<IAiService, RuleBasedAiService>();
        }

        var storageRoot = configuration["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "storage");
        services.AddSingleton<IStorageService>(_ => new LocalFileStorageService(storageRoot));

        // Deployment region — read from Cardscape:Deployment:Region.
        // Unspecified (the default) disables cross-region gating.
        services.AddSingleton<IDeploymentRegion, ConfigurationDeploymentRegion>();

        // Google Calendar sync — uses the Google Calendar API v3
        // + the oauth2.googleapis.com token endpoint. The
        // IGoogleCalendarConnectionRepository is scoped because it
        // wraps the EF Core DbContext; the sync service itself is
        // transient (cheap to instantiate per call).
        services.AddScoped<IGoogleCalendarConnectionRepository, GoogleCalendarConnectionRepository>();
        services.AddTransient<IGoogleCalendarSyncService, HttpGoogleCalendarSyncService>();
        services.AddHttpClient("google-oauth");

        // SCIM v2 provisioning — the per-workspace token
        // repository, the user-provisioning service, and the
        // IScimTokenRepository used by ScimAuthenticationHandler.
        services.AddScoped<IScimTokenRepository, ScimTokenRepository>();
        services.AddScoped<IScimService, ScimService>();

        // SAML 2.0 SSO — per-workspace connection
        // configuration. The Sustainsys.Saml2 handler is
        // registered in the API layer
        // (SamlAuthenticationHandler) when at least one
        // workspace has a connection configured.
        services.AddScoped<ISamlConnectionRepository, SamlConnectionRepository>();

        // Dashboards (P3.5) — per-board widgets. The
        // repository is scoped because it wraps the EF Core
        // DbContext.
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // Slack integration (§3.7) — the bounded context ships
        // the aggregates, services, endpoints, MCP tools, and
        // migrations, but the abstractions were not wired into
        // DI. Repositories are scoped (EF Core DbContext);
        // HttpSlackNotificationService is registered via
        // AddHttpClient so the typed HttpClient lifetime is
        // managed by the factory and the bearer token
        // configured at construction (Integrations:Slack:BotToken)
        // lives for the lifetime of the instance.
        services.AddScoped<ISlackWorkspaceRepository, SlackWorkspaceRepository>();
        services.AddScoped<ISlackChannelRepository, SlackChannelRepository>();
        services.AddHttpClient<ISlackNotificationService, HttpSlackNotificationService>(client =>
        {
            client.BaseAddress = new Uri("https://slack.com/api/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Google Drive integration (§3.8) — the picker service
        // and the per-user connection repository. The picker is
        // a typed HttpClient because every call hits Google's
        // OAuth + Drive REST endpoints; the connection
        // repository is scoped (EF Core DbContext).
        services.AddScoped<IGoogleDriveConnectionRepository, GoogleDriveConnectionRepository>();
        services.AddHttpClient<IGoogleDrivePickerService, HttpGoogleDrivePickerService>(client =>
        {
            client.BaseAddress = new Uri("https://www.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // GitHub integration (§3.9) — the REST service, the
        // board → repo link repository, and the card → PR link
        // repository. GitHubService is a typed HttpClient; the
        // two repos are scoped (EF Core DbContext).
        services.AddScoped<IGitHubRepoLinkRepository, GitHubRepoLinkRepository>();
        services.AddScoped<IGitHubPullRequestLinkRepository, GitHubPullRequestLinkRepository>();
        services.AddHttpClient<IGitHubService, HttpGitHubService>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Email-to-board integration (§3.10) — the inbound
        // address repository (scoped: EF Core DbContext) and
        // the parser/handler service. The handler is scoped
        // because it composes the address repository + the
        // Wolverine IMessageBus to dispatch CreateCardCommand.
        services.AddScoped<IInboundEmailAddressRepository, InboundEmailAddressRepository>();
        services.AddScoped<IInboundEmailService, DefaultInboundEmailService>();

        // Per-board export (board.json + attachments) and
        // per-board iCalendar feed (RFC 5545) — both shipped
        // with the v1.1.0 release. The implementations live
        // in Cardscape.Infrastructure.Export / .Calendar and
        // are scoped because they compose the EF Core
        // DbContext. Without these registrations the
        // /api/boards/{id}/export and /api/boards/{id}/ics
        // endpoints throw "No service for type" on the first
        // call (caught by the G15 integration test pass).
        services.AddScoped<Application.Abstractions.Export.IExportService, BoardExportService>();
        services.AddScoped<Application.Calendar.IIcalendarService, IcsCalendarService>();

        return services;
    }
}
