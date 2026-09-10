namespace Cardscape.Web.Shared;

// ── Webhooks ─────────────────────────────────────────────
public sealed record WebhookEndpointDto(
    Guid Id,
    Guid BoardId,
    string Url,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record WebhookEndpointIssuance(
    WebhookEndpointDto Endpoint,
    string CleartextSecret);

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    string EventType,
    int Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookRequestDto(
    string Url,
    string? Secret,
    IReadOnlyList<string> Events);
