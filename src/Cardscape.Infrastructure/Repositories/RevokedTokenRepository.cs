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
        // ExecuteDelete is the bulk-delete path. EF Core 7+
        // emits a single DELETE … WHERE TokenExpiresAt < @p0
        // — no SELECT, no change tracking, no entity
        // materialisation.
        return await context.RevokedTokens
            .Where(t => t.TokenExpiresAt <= now)
            .ExecuteDeleteAsync(ct);
    }
}
