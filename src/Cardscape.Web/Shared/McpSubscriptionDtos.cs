namespace Cardscape.Web.Shared;

// ── MCP subscriptions (v1.2.0 follow-up) ──────────────────
// Mirror of the API's McpSubscriptionsSnapshot. The MCP runs
// in a separate process; the API proxies the snapshot over
// the internal /api/admin/mcp-subscriptions endpoint. The
// Web UI renders the snapshot on the /admin/mcp-subscriptions
// page (read-only).
public sealed record McpSubscriptionsSnapshotDto(
    Dictionary<string, IReadOnlyList<string>> Subscribers,
    IReadOnlyList<McpSubscriptionEventDto> Events,
    DateTimeOffset CapturedAt);

public sealed record McpSubscriptionEventDto(
    string EventKind,
    string Uri,
    string? SessionId,
    DateTimeOffset Timestamp,
    string Detail);
