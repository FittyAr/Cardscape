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
        var configuredConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        var builder = new DbContextOptionsBuilder<CardscapeDbContext>();

        switch (provider.ToLowerInvariant())
        {
            case "sqlite":
                builder.UseSqlite(configuredConnectionString ?? "Data Source=Data/cardscape.db",
                    b => b.MigrationsAssembly("Cardscape.Infrastructure"));
                break;
            case "postgresql":
            case "postgres":
            case "npgsql":
                builder.UseNpgsql(configuredConnectionString
                    ?? "Host=localhost;Database=cardscape;Username=cardscape;Password=cardscape",
                    b => b.MigrationsAssembly("Cardscape.Migrations.PostgreSql"));
                break;
            case "mysql":
                builder.UseMySQL(configuredConnectionString
                    ?? "server=localhost;database=cardscape;user=cardscape;password=cardscape",
                    b => b.MigrationsAssembly("Cardscape.Migrations.MySql"));
                break;
            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }

        return new CardscapeDbContext(builder.Options);
    }
}
