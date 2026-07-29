using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Domain.Activities;
using Cardscape.Domain.BackgroundJobs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Checklists;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Cardscape.Domain.Recurrence;
using Cardscape.Domain.Security;
using Cardscape.Domain.Voting;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.Infrastructure.Ai;
using Cardscape.Infrastructure.Email;
using Cardscape.Infrastructure.Persistence;
using Cardscape.Infrastructure.Persistence.Interceptors;
using Cardscape.Infrastructure.Repositories;
using Cardscape.Infrastructure.Search;
using Cardscape.Infrastructure.Security;
using Cardscape.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Identity-shaped repositories (a few extra generics to satisfy the IRepository
        // contract for non-aggregate roots). The Application layer depends only on the
        // typed interfaces above.

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddSingleton<IRateLimiter, RateLimiter>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
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

        return services;
    }
}
