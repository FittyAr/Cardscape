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
        DatabaseProvider provider = DatabaseProvider.Parse(Environment.GetEnvironmentVariable("Database__Provider"));
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? provider.DesignTimeConnectionString;

        var builder = new DbContextOptionsBuilder<CardscapeDbContext>();
        builder.ConfigureWarnings(w => w.Throw(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        builder.UseCardscapeDatabase(provider, connectionString);

        return new CardscapeDbContext(builder.Options);
    }
}
