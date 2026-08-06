using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Authentication.RevokedTokens;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRevokedTokenRepository"/>.
/// The validation query is backed by a unique index on
/// <c>Jti</c> (see <c>RevokedTokenConfiguration</c>); the
/// sweeper's purge is a range scan on
/// <c>TokenExpiresAt</c>.
/// </summary>
public sealed class RevokedTokenRepository(
    CardscapeDbContext context) : IRevokedTokenRepository
{
    public async Task AddAsync(RevokedToken revokedToken, CancellationToken ct = default)
    {
        await context.RevokedTokens.AddAsync(revokedToken, ct);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return false;
        }

        // AnyAsync translates to a single EXISTS subquery
        // against the unique index on Jti. The result is
        // sub-millisecond even with millions of rows.
        return await context.RevokedTokens
            .AsNoTracking()
            .AnyAsync(t => t.Jti == jti, ct);
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        // BETA-2-#13 — see test-results/BETA-TEST-REPORT.md.
        //
        // The previous implementation called
        // ExecuteDeleteAsync(Where(t => t.TokenExpiresAt <= now))
        // expecting EF Core 7+ to emit a single bulk DELETE.
        // In this build the SQLite provider refuses to
        // translate the `DateTimeOffset` comparison in a
        // `ExecuteDelete` context (the SQL translation
        // visitor throws "could not be translated") so the
        // whole call 500s and the RevocationSweeper retries
        // every poll interval. The pragmatic fix is the same
        // pattern used by the other bulk-cleanup paths in
        // the project: load the matching row ids, then
        // issue a regular RemoveRange + SaveChanges. Sweeper
        // runs every 60s on a small table; the cost of the
        // SELECT is dwarfed by the DELETE itself.
        var expired = await context.RevokedTokens
            .Where(t => t.TokenExpiresAt <= now)
            .Select(t => t.Id)
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            return 0;
        }

        var rows = await context.RevokedTokens
            .Where(t => expired.Contains(t.Id))
            .ToListAsync(ct);
        context.RevokedTokens.RemoveRange(rows);
        await context.SaveChangesAsync(ct);
        return rows.Count;
    }
}
