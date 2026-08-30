using Cardscape.Domain.Common;

namespace Cardscape.Domain.Webhooks;

/// <summary>
/// Lifecycle of a single outbound webhook attempt. The dispatcher
/// creates a <see cref="WebhookDeliveryStatus.Pending"/> row when
/// it enqueues a job; the handler flips it through
/// <see cref="WebhookDeliveryStatus.Success"/>,
/// <see cref="WebhookDeliveryStatus.Failed"/>, or
/// <see cref="WebhookDeliveryStatus.DeadLettered"/> as the call
/// resolves. Every attempt bumps the delivery's attempt count and
/// stamps the last-attempt-at timestamp.
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>Created by the dispatcher; not yet attempted.</summary>
    Pending = 0,
    /// <summary>Endpoint returned 2xx.</summary>
    Success = 1,
    /// <summary>Most recent attempt failed but more retries remain.</summary>
    Failed = 2,
    /// <summary>Exhausted retries without a 2xx; parked for operator inspection.</summary>
    DeadLettered = 3
}

/// <summary>
/// One outbound attempt to deliver a webhook payload. The same
/// row is updated as the dispatcher retries, so the audit history
/// is a single row per (endpoint, event) pair.
/// </summary>
public sealed class WebhookDelivery : AggregateRoot<WebhookDeliveryId>
{
    public WebhookEndpointId EndpointId { get; private set; } = null!;

    /// <summary>Event type identifier (e.g. <c>"card.created"</c>).</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Pre-serialised JSON body sent to the endpoint. The
    /// canonical, signed form lives here verbatim; the handler
    /// does not re-serialise at delivery time.</summary>
    public string PayloadJson { get; private set; } = "{}";

    public WebhookDeliveryStatus Status { get; private set; } = WebhookDeliveryStatus.Pending;

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>Error message from the most recent failed attempt;
    /// cleared on a successful attempt.</summary>
    public string? LastError { get; private set; }

    // EF Core.
    private WebhookDelivery() { }

    private WebhookDelivery(
        WebhookDeliveryId id,
        WebhookEndpointId endpointId,
        string eventType,
        string payloadJson,
        DateTimeOffset at)
    {
        Id = id;
        EndpointId = endpointId;
        EventType = eventType;
        PayloadJson = payloadJson;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        CreatedAt = at;
    }

    public static Result<WebhookDelivery> Create(
        WebhookDeliveryId id,
        WebhookEndpointId endpointId,
        string eventType,
        string payloadJson,
        DateTimeOffset at)
    {
        if (id is null)
        {
            return Result.Failure<WebhookDelivery>(DomainError.Validation(
                "webhooks.delivery_required", "Delivery is required."));
        }

        if (endpointId is null)
        {
            return Result.Failure<WebhookDelivery>(DomainError.Validation(
                "webhooks.endpoint_required", "Endpoint is required."));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return Result.Failure<WebhookDelivery>(DomainError.Validation(
                "webhooks.event_type_required", "Event type is required."));
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Result.Failure<WebhookDelivery>(DomainError.Validation(
                "webhooks.payload_required", "Payload is required."));
        }

        if (payloadJson.Length > 16_000)
        {
            return Result.Failure<WebhookDelivery>(DomainError.Validation(
                "webhooks.payload_too_large", "Webhook payload must be 16KB or fewer."));
        }

        return Result.Success(new WebhookDelivery(
            id, endpointId, eventType, payloadJson, at));
    }

    /// <summary>Records a successful attempt.</summary>
    public void MarkSuccess(DateTimeOffset at)
    {
        AttemptCount++;
        LastAttemptAt = at;
        LastError = null;
        Status = WebhookDeliveryStatus.Success;
        UpdatedAt = at;
    }

    /// <summary>Records a failed attempt that still has retries left.</summary>
    public void MarkFailed(string error, DateTimeOffset at)
    {
        AttemptCount++;
        LastAttemptAt = at;
        LastError = Truncate(error, 4000);
        Status = WebhookDeliveryStatus.Failed;
        UpdatedAt = at;
    }

    /// <summary>Records a failed attempt that exhausted retries.</summary>
    public void MarkDeadLettered(string error, DateTimeOffset at)
    {
        AttemptCount++;
        LastAttemptAt = at;
        LastError = Truncate(error, 4000);
        Status = WebhookDeliveryStatus.DeadLettered;
        UpdatedAt = at;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
