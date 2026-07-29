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
        return await Task.Run<IReadOnlyList<WebhookEndpoint>>(() =>
        {
            return Db.Set<WebhookEndpoint>().AsEnumerable()
                .Where(e => e.BoardId.Value == boardValue && !e.IsDeleted)
                .OrderBy(e => e.CreatedAt)
                .ToList();
        }, ct);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> ListActiveForEventAsync(
        string eventType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return [];
        }

        return await Task.Run<IReadOnlyList<WebhookEndpoint>>(() =>
        {
            return Db.Set<WebhookEndpoint>().AsEnumerable()
                .Where(e => e.Active && !e.IsDeleted && e.SubscribesTo(eventType))
                .ToList();
        }, ct);
    }
}
