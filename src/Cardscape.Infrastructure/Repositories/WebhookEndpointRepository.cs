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
        var boardValue = boardId.Value;
        var rows = new List<WebhookEndpoint>();
        await foreach (var e in Db.Set<WebhookEndpoint>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (e.BoardId.Value == boardValue && !e.IsDeleted)
            {
                rows.Add(e);
            }
        }
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

        var rows = new List<WebhookEndpoint>();
        await foreach (var e in Db.Set<WebhookEndpoint>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (e.Active && !e.IsDeleted && e.SubscribesTo(eventType))
            {
                rows.Add(e);
            }
        }
        return rows;
    }
}
