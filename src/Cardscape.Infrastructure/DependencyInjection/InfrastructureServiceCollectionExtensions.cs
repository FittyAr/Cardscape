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
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IRepository<User, UserId>, UserRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IBoardListRepository, BoardListRepository>();
        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();

        // Identity-shaped repositories (a few extra generics to satisfy the IRepository
        // contract for non-aggregate roots). The Application layer depends only on the
        // typed interfaces above.

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<IEmailService, ConsoleEmailService>();
        services.AddSingleton<ISearchIndex, InMemorySearchIndex>();

        var storageRoot = configuration["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "storage");
        services.AddSingleton<IStorageService>(_ => new LocalFileStorageService(storageRoot));

        return services;
    }
}
