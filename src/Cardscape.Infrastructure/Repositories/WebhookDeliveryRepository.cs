using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Webhooks;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class WebhookDeliveryRepository(CardscapeDbContext db)
    : RepositoryBase<WebhookDelivery, WebhookDeliveryId>(db), IWebhookDeliveryRepository
{
    public async Task<IReadOnlyList<WebhookDelivery>> ListForEndpointAsync(
        WebhookEndpointId endpointId,
        WebhookDeliveryStatus? statusFilter,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var endpointValue = endpointId.Value;
        return await Task.Run<IReadOnlyList<WebhookDelivery>>(() =>
        {
            var rows = Db.Set<WebhookDelivery>().AsEnumerable()
                .Where(d => d.EndpointId.Value == endpointValue
                            && (statusFilter is null || d.Status == statusFilter.Value))
                .ToList();

            // SQLite cannot ORDER BY on DateTimeOffset, so sort
            // client-side. The list is bounded by the page size
            // (default 50, max 200) and the per-endpoint delivery
            // history, which is small in practice.
            rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
            if (skip >= rows.Count)
            {
                return new List<WebhookDelivery>();
            }

            int end = Math.Min(skip + take, rows.Count);
            return rows.GetRange(skip, end - skip);
        }, ct);
    }
}
