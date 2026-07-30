using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cardscape.Infrastructure.Repositories;

public sealed class OAuthAppRepository(CardscapeDbContext db)
    : RepositoryBase<OAuthApp, OAuthAppId>(db), IOAuthAppRepository
{
    public async Task<OAuthApp?> FindByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        return await Db.Set<OAuthApp>()
            .FirstOrDefaultAsync(a => a.ClientId == clientId, ct);
    }

    public async Task<IReadOnlyList<OAuthApp>> ListForOwnerAsync(
        Guid ownerId, CancellationToken ct = default)
    {
        // EF Core 10 / SQLite can't ORDER BY DateTimeOffset
        // reliably; the per-owner result set is small so we
        // stream client-side and sort in memory.
        var rows = new List<OAuthApp>();
        await foreach (var app in Db.Set<OAuthApp>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (app.OwnerId == ownerId)
            {
                rows.Add(app);
            }
        }

        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }
}
