using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Webhooks;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        IQueryable<WebhookDelivery> query = Set
            .AsNoTracking()
            .Where(delivery => delivery.EndpointId == endpointId);
        if (statusFilter is not null)
        {
            query = query.Where(delivery => delivery.Status == statusFilter.Value);
        }

        return await query.ToListOrderedAsync(
            Db, delivery => delivery.CreatedAt, descending: true, skip, take, ct);
    }
}
