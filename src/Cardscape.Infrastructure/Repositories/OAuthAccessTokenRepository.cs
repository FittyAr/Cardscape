using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.OAuthApps;
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
        var rows = new List<OAuthAccessToken>();
        await foreach (var token in Db.Set<OAuthAccessToken>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (token.UserId.Value == userId)
            {
                rows.Add(token);
            }
        }

        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }

    public async Task<int> PurgeExpiredAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        return await Db.Set<OAuthAccessToken>()
            .Where(t => t.ExpiresAt < cutoff && t.RevokedAt != null)
            .ExecuteDeleteAsync(ct);
    }
}
