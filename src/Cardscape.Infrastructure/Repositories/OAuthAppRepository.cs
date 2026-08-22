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
        IQueryable<OAuthApp> query = Db.Set<OAuthApp>()
            .AsNoTracking()
            .Where(app => app.OwnerId == ownerId);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderByDescending(app => app.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        return rows;
    }
}
