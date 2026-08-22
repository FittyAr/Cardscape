using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Webhooks;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class WebhookEndpointRepository(CardscapeDbContext db)
    : RepositoryBase<WebhookEndpoint, WebhookEndpointId>(db), IWebhookEndpointRepository
{
    public async Task<IReadOnlyList<WebhookEndpoint>> ListForBoardAsync(
        BoardId boardId, CancellationToken ct = default)
    {
        IQueryable<WebhookEndpoint> query = Db.Set<WebhookEndpoint>()
            .AsNoTracking()
            .Where(endpoint => endpoint.BoardId == boardId && !endpoint.IsDeleted);
        if (!Db.Database.IsSqlite())
        {
            return await query.OrderBy(endpoint => endpoint.CreatedAt).ToListAsync(ct);
        }

        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return rows;
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> ListActiveForEventAsync(
        string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        // Preserve exact comma-delimited token semantics while pushing the
        // selective active/deleted predicate to the database.
        var candidates = await Db.Set<WebhookEndpoint>()
            .AsNoTracking()
            .Where(endpoint => endpoint.Active && !endpoint.IsDeleted)
            .ToListAsync(ct);
        return candidates.Where(endpoint => endpoint.SubscribesTo(eventType)).ToList();
    }
}
