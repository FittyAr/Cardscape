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

        return await Set
            .FirstOrDefaultAsync(a => a.ClientId == clientId, ct);
    }

    public async Task<IReadOnlyList<OAuthApp>> ListForOwnerAsync(
        Guid ownerId, CancellationToken ct = default)
    {
        IQueryable<OAuthApp> query = Set
            .AsNoTracking()
            .Where(app => app.OwnerId == ownerId);
        return await query.ToListOrderedAsync(Db, app => app.CreatedAt, descending: true, ct: ct);
    }
}
