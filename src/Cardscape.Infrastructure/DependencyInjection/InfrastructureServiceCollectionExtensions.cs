using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Import;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Application.Authentication.Abstractions;
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
using StackExchange.Redis;

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
        // BETA-A7-001 — see test-results/beta/reports/A7-advanced.md.
        // The previous AutomationDispatcher sat in the API
        // project as a static class whose four `Handle` methods
        // were meant to be discovered by Wolverine. They never
        // ran: the card events do not implement IMessage, so
        // Wolverine's static-handler discovery skips them, and
        // there is no manual subscription in Program.cs. The
        // rules created via the API persisted correctly but
        // were never executed. Converted to a proper
        // IDomainEventBroadcaster so the existing
        // WolverineDomainEventDispatcher fan-out picks it up.
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

        // BUG-A8-014 — backing store for the new password
        // reset tokens issued by POST /api/auth/forgot-password
        // and consumed by POST /api/auth/reset-password. The
        // token hash is stored in the cleartext, not the
        // token itself, so a leaked DB row still cannot be
        // used to log in.
        services.AddScoped<PasswordResetRepository>();
        services.AddScoped<IRepository<PasswordReset, PasswordResetId>, PasswordResetRepository>(sp => sp.GetRequiredService<PasswordResetRepository>());
        services.AddScoped<IPasswordResetRepository, PasswordResetRepository>(sp => sp.GetRequiredService<PasswordResetRepository>());
        services.AddScoped<ITotpService, TotpService>();

        // Two-step 2FA login: the password check mints a one-shot
        // PendingTotpToken; the /api/auth/login/totp endpoint consumes
        // it. The backend is operator-selectable via
        // Cardscape:Infrastructure:PendingTotpStore:Backend — see
        // docs/operations/06-configurable-subsystems.md. The
        // in-memory implementation is still the default so a
        // single-instance deploy needs no extra infrastructure.
        InfrastructureOptions infraOptions = InfrastructureOptions.Bind(configuration);
        bool pendingTotpWantsRedis = infraOptions.PendingTotpStore.Backend == DistributedBackend.Redis;
        bool rateLimiterWantsRedis = infraOptions.RateLimiter.Backend == DistributedBackend.Redis;
        bool anyRedis = pendingTotpWantsRedis || rateLimiterWantsRedis;

        if (anyRedis)
        {
            if (string.IsNullOrWhiteSpace(infraOptions.Redis.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Cardscape:Infrastructure:Redis:ConnectionString is required when at least "
                    + "one subsystem sets its Backend to 'Redis'. Check "
                    + "Cardscape:Infrastructure:RateLimiter:Backend and "
                    + "Cardscape:Infrastructure:PendingTotpStore:Backend.");
            }

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                IConfiguration cfg = sp.GetRequiredService<IConfiguration>();
                string connectionString = cfg["Cardscape:Infrastructure:Redis:ConnectionString"]
                    ?? throw new InvalidOperationException(
                        "Cardscape:Infrastructure:Redis:ConnectionString is required.");
                ConfigurationOptions parsed = ConfigurationOptions.Parse(connectionString);
                // AbortConnect=false: the multiplexer keeps
                // trying to connect in the background instead
                // of throwing at startup. The rate limiter and
                // pending-2FA store both fail open on transport
                // errors, so a temporary Redis outage is a
                // degraded mode, not an outage.
                parsed.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(parsed);
            });
        }

        if (pendingTotpWantsRedis)
        {
            services.AddSingleton<IPendingTotpLoginStore, RedisPendingTotpLoginStore>();
        }
        else
        {
            services.AddSingleton<IPendingTotpLoginStore, InMemoryPendingTotpLoginStore>();
        }

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

        // Rate limiter backend. The in-memory implementation
        // is a per-instance bucket; the Redis implementation
        // shares one bucket across every API instance. The
        // choice is operator-facing configuration — see
        // docs/operations/06-configurable-subsystems.md.
        services.Configure<InfrastructureOptions>(configuration.GetSection(InfrastructureOptions.SectionName));
        if (rateLimiterWantsRedis)
        {
            services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        }
        else
        {
            services.AddSingleton<IRateLimiter, RateLimiter>();
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer)
                    && !string.IsNullOrWhiteSpace(options.Audience)
                    && options.AccessTokenMinutes is >= 5 and <= 1_440,
                "JWT requires non-empty issuer/audience and an access-token lifetime between 5 minutes and 24 hours.")
            .ValidateOnStart();

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
        services.AddScoped<Application.Calendar.ICalendarFeedRenderer, IcsCalendarService>();

        return services;
    }
}
