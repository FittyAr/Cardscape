using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to materialise
/// the <see cref="CardscapeDbContext"/> when no host is running.
/// </summary>
public sealed class DesignTimeCardscapeDbContextFactory : IDesignTimeDbContextFactory<CardscapeDbContext>
{
    public CardscapeDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("Database__Provider") ?? "Sqlite";

        var builder = new DbContextOptionsBuilder<CardscapeDbContext>();

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                builder.UseSqlite("Data Source=Data/cardscape.db",
                    b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                break;
            case "postgresql":
            case "postgres":
            case "npgsql":
                builder.UseNpgsql("Host=localhost;Database=cardscape;Username=cardscape;Password=cardscape",
                    b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                break;
            case "mariadb":
            case "mysql":
                builder.UseMySQL("server=localhost;database=cardscape;user=cardscape;password=cardscape",
                    b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }

        return new CardscapeDbContext(builder.Options);
    }
}
