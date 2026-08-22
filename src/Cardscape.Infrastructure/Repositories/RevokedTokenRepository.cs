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
        if (!context.Database.IsSqlite())
        {
            return await context.RevokedTokens
                .Where(token => token.TokenExpiresAt <= now)
                .ExecuteDeleteAsync(ct);
        }

        // SQLite cannot compare DateTimeOffset. Select only the ids locally,
        // then execute one set-based DELETE rather than loading tracked rows.
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

        return await context.RevokedTokens
            .Where(token => expired.Contains(token.Id))
            .ExecuteDeleteAsync(ct);
    }
}
