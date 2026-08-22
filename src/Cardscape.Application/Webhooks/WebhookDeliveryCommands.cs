using System.Security.Cryptography;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Webhooks;
using Wolverine;

namespace Cardscape.Application.Webhooks;

/// <summary>Command the dispatcher sends (or any application code
/// can send) to fan a single domain event out to every matching
/// active endpoint. The handler creates a delivery row and an
/// associated <c>BackgroundJob</c> for each endpoint, so retries
/// ride the same backoff infrastructure as everything else.</summary>
public sealed record EnqueueWebhookDeliveriesCommand(
    string EventType,
    Guid BoardId,
    object Data) : IMessage;

public static class EnqueueWebhookDeliveriesCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<Result<int>> Handle(
        EnqueueWebhookDeliveriesCommand command,
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IBackgroundJobScheduler scheduler,
        IClock clock,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        if (!WebhookEventTypes.IsKnown(command.EventType))
        {
            return Result.Failure<int>(DomainError.Validation(
                "webhooks.event_unknown", $"Unknown event type '{command.EventType}'."));
        }

        IReadOnlyList<WebhookEndpoint> targets =
            await endpoints.ListActiveForEventAsync(command.EventType, ct);
        if (targets.Count == 0)
        {
            return Result.Success(0);
        }

        DateTimeOffset now = clock.UtcNow;
        int queued = 0;

        foreach (WebhookEndpoint endpoint in targets)
        {
            if (endpoint.BoardId.Value != command.BoardId)
            {
                // Don't fan an event on board X to endpoints
                // registered on board Y. The repository may
                // eventually return all matching endpoints across
                // boards once we add cross-board fan-out.
                continue;
            }

            // Build the delivery row first so we can include the
            // canonical id in the signed payload. The handler
            // re-reads the row by id at dispatch time.
            var deliveryId = WebhookDeliveryId.New();
            var payload = new WebhookPayload(
                Event: command.EventType,
                BoardId: command.BoardId,
                OccurredAt: now,
                DeliveryId: deliveryId.Value.ToString(),
                Data: command.Data);

            string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

            var creation = WebhookDelivery.Create(
                endpoint.Id, command.EventType, payloadJson, now);
            if (creation.IsFailure)
            {
                continue;
            }

            await deliveries.AddAsync(creation.Value, ct);
            await unitOfWork.SaveChangesAsync(ct);

            var jobPayload = new WebhookDeliveryJobPayload(
                creation.Value.Id.Value,
                endpoint.Id.Value,
                command.EventType,
                payloadJson);
            await scheduler.EnqueueAsync(
                WebhookJobTypes.DeliverWebhook,
                jobPayload,
                scheduledFor: now,
                maxAttempts: 5,
                ct: ct);
            queued++;
        }

        return Result.Success(queued);
    }
}
