namespace Cardscape.Application.Webhooks;

/// <summary>
/// Background-job type identifiers for the webhook delivery
/// pipeline. The string values are the discriminator stored in
/// <c>background_jobs.type</c> that the
/// <c>Cardscape.Infrastructure.BackgroundJobs.WebhookDeliveryHandler</c>
/// responds to.
/// </summary>
public static class WebhookJobTypes
{
    public const string DeliverWebhook = "webhook.deliver";
}

/// <summary>Payload of a single delivery job. The handler reloads
/// the endpoint + delivery rows by id before signing and posting,
/// so this payload is just identifiers + the already-serialised
/// JSON body.</summary>
public sealed record WebhookDeliveryJobPayload(
    Guid DeliveryId,
    Guid EndpointId,
    string EventType,
    string PayloadJson);
