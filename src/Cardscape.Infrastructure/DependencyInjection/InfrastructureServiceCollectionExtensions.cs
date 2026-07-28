using Cardscape.Application.Abstractions.Email;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Storage;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Cardscape.Domain.Security;
using Cardscape.Domain.Workspaces;
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

        // Identity-shaped repositories (a few extra generics to satisfy the IRepository
        // contract for non-aggregate roots). The Application layer depends only on the
        // typed interfaces above.

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IApiTokenService, ApiTokenService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<IEmailService, ConsoleEmailService>();
        services.AddSingleton<IInvitationEmailService, ConsoleInvitationEmailService>();
        services.AddSingleton<ISearchIndex, InMemorySearchIndex>();

        var storageRoot = configuration["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "storage");
        services.AddSingleton<IStorageService>(_ => new LocalFileStorageService(storageRoot));

        return services;
    }
}
