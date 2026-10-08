using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>The relational engines Cardscape ships migrations for (ADR 0013).</summary>
public enum DatabaseProvider
{
    Sqlite,
    PostgreSql,
    MySql,
    MariaDb,
}

public static class DatabaseProviderExtensions
{
    extension(DatabaseProvider provider)
    {
        /// <summary>
        /// Parses the <c>Database:Provider</c> setting. Missing means SQLite;
        /// <c>postgres</c> and <c>npgsql</c> are accepted aliases for PostgreSQL.
        /// </summary>
        public static DatabaseProvider Parse(string? name) => (name ?? "Sqlite").ToLowerInvariant() switch
        {
            "sqlite" => DatabaseProvider.Sqlite,
            "postgresql" or "postgres" or "npgsql" => DatabaseProvider.PostgreSql,
            "mysql" => DatabaseProvider.MySql,
            "mariadb" => DatabaseProvider.MariaDb,
            _ => throw new InvalidOperationException(
                $"Unsupported database provider: {name}. Use Sqlite, PostgreSQL, MySql, or MariaDB."),
        };

        /// <summary>Connection string <c>dotnet ef</c> uses when none is configured.</summary>
        public string DesignTimeConnectionString => provider switch
        {
            DatabaseProvider.Sqlite => "Data Source=Data/cardscape.db",
            DatabaseProvider.PostgreSql => "Host=localhost;Database=cardscape;Username=cardscape;Password=cardscape",
            _ => "server=localhost;database=cardscape;user=cardscape;password=cardscape",
        };
    }
}

public static class CardscapeDbContextOptionsBuilderExtensions
{
    extension(DbContextOptionsBuilder options)
    {
        /// <summary>
        /// Points the context at <paramref name="provider"/> with the migrations
        /// assembly that owns that engine's history.
        /// </summary>
        public DbContextOptionsBuilder UseCardscapeDatabase(DatabaseProvider provider, string connectionString) =>
            provider switch
            {
                DatabaseProvider.Sqlite => options.UseSqlite(connectionString,
                    sqlite => sqlite.MigrationsAssembly("Cardscape.Infrastructure")),
                DatabaseProvider.PostgreSql => options.UseNpgsql(connectionString,
                    postgres => postgres.MigrationsAssembly("Cardscape.Migrations.PostgreSql")),
                DatabaseProvider.MySql => options.UseMySQL(connectionString,
                    mySql => mySql.MigrationsAssembly("Cardscape.Migrations.MySql")),
                // MariaDB is a distinct engine/provider, not an Oracle MySQL alias (ADR 0013).
                DatabaseProvider.MariaDb => options.UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 4, 0)),
                    mariaDb => mariaDb.MigrationsAssembly("Cardscape.Migrations.MariaDb")),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
            };
    }
}
