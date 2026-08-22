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
        IQueryable<WebhookDelivery> query = Db.Set<WebhookDelivery>()
            .AsNoTracking()
            .Where(delivery => delivery.EndpointId == endpointId);
        if (statusFilter is not null)
        {
            query = query.Where(delivery => delivery.Status == statusFilter.Value);
        }

        if (!Db.Database.IsSqlite())
        {
            return await query
                .OrderByDescending(delivery => delivery.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);
        }

        // SQLite cannot order DateTimeOffset. The indexed endpoint/status
        // filters still run in SQL; only ordering and page slicing remain local.
        var rows = await query.ToListAsync(ct);
        rows.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
        if (skip >= rows.Count)
        {
            return new List<WebhookDelivery>();
        }

        int end = Math.Min(skip + take, rows.Count);
        return rows.GetRange(skip, end - skip);
    }
}
