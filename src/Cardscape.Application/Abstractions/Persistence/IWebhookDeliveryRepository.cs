using Cardscape.Domain.Webhooks;

namespace Cardscape.Application.Abstractions.Persistence;

/// <summary>Read/write repository for <see cref="WebhookDelivery"/>.</summary>
public interface IWebhookDeliveryRepository : IRepository<WebhookDelivery, WebhookDeliveryId>
{
    /// <summary>Lists recent deliveries for an endpoint, newest
    /// first. <paramref name="statusFilter"/> optionally narrows
    /// the result to a single status; <c>null</c> returns all.</summary>
    Task<IReadOnlyList<WebhookDelivery>> ListForEndpointAsync(
        WebhookEndpointId endpointId,
        WebhookDeliveryStatus? statusFilter,
        int skip,
        int take,
        CancellationToken ct = default);
}
