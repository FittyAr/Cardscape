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

/// <summary>JSON shape of a webhook payload as posted to subscribers.</summary>
public sealed record WebhookPayload(
    string Event,
    Guid BoardId,
    DateTimeOffset OccurredAt,
    string DeliveryId,
    object Data);

/// <summary>Compact projection of an endpoint for the API/MCP/Web layers.</summary>
public sealed record WebhookEndpointDto(
    Guid Id,
    Guid BoardId,
    string Url,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static WebhookEndpointDto FromEntity(WebhookEndpoint e) => new(
        e.Id.Value,
        e.BoardId.Value,
        e.Url,
        Events: e.Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList(),
        Active: e.Active,
        CreatedAt: e.CreatedAt);
}

/// <summary>Result of a create call: the new endpoint plus the
/// cleartext secret (returned once, never persisted).</summary>
public sealed record WebhookEndpointIssuance(WebhookEndpointDto Endpoint, string CleartextSecret);

/// <summary>Compact projection of a delivery row for the API/MCP/Web layers.</summary>
public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    string EventType,
    int Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt)
{
    public static WebhookDeliveryDto FromEntity(WebhookDelivery d) => new(
        d.Id.Value,
        d.EndpointId.Value,
        d.EventType,
        (int)d.Status,
        d.AttemptCount,
        d.LastAttemptAt,
        d.LastError,
        d.CreatedAt);
}

// ── Commands & queries ──────────────────────────────────────────
