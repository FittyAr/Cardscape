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
        // BETA-2-#13 / BETA-4-#2 — see test-results/BETA-TEST-REPORT.md.
        //
        // The original implementation called
        // ExecuteDeleteAsync(Where(t => t.TokenExpiresAt <= now))
        // and the R2 fix split it into a SELECT + RemoveRange,
        // but EF Core 10 + SQLite still cannot translate
        // `TokenExpiresAt <= now` (a DateTimeOffset comparison
        // against a captured local) — the provider throws
        // "could not be translated" at runtime and the
        // RevocationSweeper's background loop logs an error
        // every minute. The pragmatic fix is the same pattern
        // BETA-2-#7 used: pull the rows with AsAsyncEnumerable
        // and filter on the client. The revoked-tokens table
        // is bounded by the JWT TTL, so the client-side
        // filter is cheap; the win is that the sweeper
        // actually completes instead of erroring on every tick.
        var expired = new List<RevokedTokenId>();
        await foreach (var token in context.RevokedTokens.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (token.TokenExpiresAt <= now)
            {
                expired.Add(token.Id);
            }
        }

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
