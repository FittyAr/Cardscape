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
        var builder = new DbContextOptionsBuilder<CardscapeDbContext>();
        builder.UseSqlite("Data Source=Data/cardscape.db",
            sqlite => sqlite.MigrationsAssembly("Cardscape.Infrastructure"));

        return new CardscapeDbContext(builder.Options);
    }
}
