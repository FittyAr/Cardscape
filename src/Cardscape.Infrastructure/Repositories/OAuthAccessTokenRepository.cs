using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Members;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class OAuthAccessTokenRepository(CardscapeDbContext db)
    : RepositoryBase<OAuthAccessToken, OAuthAccessTokenId>(db),
      IOAuthAccessTokenRepository
{
    public async Task<OAuthAccessToken?> FindByTokenHashAsync(
        string tokenHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        return await Db.Set<OAuthAccessToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);
    }

    public async Task<IReadOnlyList<OAuthAccessToken>> ListForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        IQueryable<OAuthAccessToken> query = Db.Set<OAuthAccessToken>()
            .AsNoTracking()
            .Where(token => token.UserId == new UserId(userId));
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderByDescending(token => token.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        IQueryable<OAuthAccessToken> revoked = Db.Set<OAuthAccessToken>()
            .Where(token => token.RevokedAt != null);
        if (!Db.Database.IsSqlite())
        {
            return await revoked.Where(token => token.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
        }

        var expiredIds = new List<OAuthAccessTokenId>();
        await foreach (OAuthAccessToken token in revoked.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (token.ExpiresAt < cutoff) expiredIds.Add(token.Id);
        }
        return expiredIds.Count == 0
            ? 0
            : await Db.Set<OAuthAccessToken>().Where(token => expiredIds.Contains(token.Id)).ExecuteDeleteAsync(ct);
    }
}
