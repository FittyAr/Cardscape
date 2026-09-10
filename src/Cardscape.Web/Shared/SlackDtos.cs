namespace Cardscape.Web.Shared;

// ── Slack integration (v1.1.0 §3.7) ──────────────────────────
// Mirrors of the Application-layer DTOs. The bot token is never
// included in the projection.
public sealed record SlackWorkspaceDto(
    Guid Id,
    Guid WorkspaceId,
    string TeamId,
    string TeamName,
    DateTimeOffset? LastUsedAt,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record SlackChannelDto(
    Guid Id,
    Guid SlackWorkspaceId,
    Guid BoardId,
    string ChannelId,
    string ChannelName,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt);
