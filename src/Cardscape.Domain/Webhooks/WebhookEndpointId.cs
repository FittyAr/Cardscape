namespace Cardscape.Domain.Webhooks;

/// <summary>Identifier of a <see cref="WebhookEndpoint"/>.</summary>
public sealed record WebhookEndpointId(Guid Value) : Common.GuidId<WebhookEndpointId>(Value);
