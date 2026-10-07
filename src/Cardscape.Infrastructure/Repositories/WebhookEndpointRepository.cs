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
        IQueryable<WebhookEndpoint> query = Set
            .AsNoTracking()
            .Where(endpoint => endpoint.BoardId == boardId && !endpoint.IsDeleted);
        return await query.ToListOrderedAsync(Db, endpoint => endpoint.CreatedAt, ct: ct);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> ListActiveForEventAsync(
        BoardId boardId,
        string eventType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        // Preserve exact comma-delimited token semantics after EF Core has
        // restricted the candidate set to the owning board and active rows.
        var candidates = await Set
            .AsNoTracking()
            .Where(endpoint => endpoint.BoardId == boardId
                && endpoint.Active
                && !endpoint.IsDeleted)
            .ToListAsync(ct);
        return candidates.Where(endpoint => endpoint.SubscribesTo(eventType)).ToList();
    }
}
