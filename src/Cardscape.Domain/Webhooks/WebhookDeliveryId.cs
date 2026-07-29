namespace Cardscape.Domain.Webhooks;

/// <summary>Identifier of a <see cref="WebhookDelivery"/>.</summary>
public sealed record WebhookDeliveryId(Guid Value) : Common.GuidId<WebhookDeliveryId>(Value);
