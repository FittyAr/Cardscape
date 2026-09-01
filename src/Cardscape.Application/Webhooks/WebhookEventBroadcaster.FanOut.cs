using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cardscape.Application.Webhooks;

public sealed partial class WebhookEventBroadcaster
{
    private async Task FanOutAsync(
        string eventType,
        BoardId boardId,
        object data,
        CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        IWebhookEndpointRepository endpoints = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        IWebhookDeliveryRepository deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        IBackgroundJobScheduler scheduler = scope.ServiceProvider.GetRequiredService<IBackgroundJobScheduler>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        IReadOnlyList<WebhookEndpoint> targets =
            await endpoints.ListActiveForEventAsync(boardId, eventType, ct);
        if (targets.Count == 0)
        {
            return;
        }

        DateTimeOffset now = clock.UtcNow;
        foreach (WebhookEndpoint endpoint in targets)
        {
            var deliveryId = WebhookDeliveryId.New();
            var payload = new WebhookPayload(
                Event: eventType,
                BoardId: boardId.Value,
                OccurredAt: now,
                DeliveryId: deliveryId.Value.ToString(),
                Data: data);
            string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            var creation = WebhookDelivery.Create(
                deliveryId,
                endpoint.Id,
                eventType,
                payloadJson,
                now);
            if (creation.IsFailure)
            {
                throw new InvalidOperationException(creation.Error.Message);
            }

            await deliveries.AddAsync(creation.Value, ct);
            var jobPayload = new WebhookDeliveryJobPayload(
                creation.Value.Id.Value,
                endpoint.Id.Value,
                eventType,
                payloadJson);
            var enqueue = await scheduler.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                jobPayload,
                scheduledFor: now,
                maxAttempts: 5,
                ct: ct);
            if (enqueue.IsFailure)
            {
                throw new InvalidOperationException(enqueue.Error.Message);
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Queued {Count} webhook delivery job(s) for {EventType} on board {BoardId}",
                targets.Count,
                eventType,
                boardId);
        }
    }
}
