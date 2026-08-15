using Cardscape.Domain.Integrations.Slack;

namespace Cardscape.Application.Integrations.Slack.DTOs;

/// <summary>Compact projection of a <see cref="SlackWorkspace"/>
/// for the API / MCP / Web layers. The bot token is never
/// included in the projection.</summary>
public sealed record SlackWorkspaceDto(
    Guid Id,
    Guid WorkspaceId,
    string TeamId,
    string TeamName,
    DateTimeOffset? LastUsedAt,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static SlackWorkspaceDto FromEntity(SlackWorkspace w) => new(
        w.Id.Value,
        w.WorkspaceId.Value,
        w.TeamId,
        w.TeamName,
        LastUsedAt: w.LastUsedAt,
        Active: w.Active,
        CreatedAt: w.CreatedAt);
}

/// <summary>Compact projection of a <see cref="SlackChannel"/>
/// mapping for the API / MCP / Web layers.</summary>
public sealed record SlackChannelDto(
    Guid Id,
    Guid SlackWorkspaceId,
    Guid BoardId,
    string ChannelId,
    string ChannelName,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt)
{
    public static SlackChannelDto FromEntity(SlackChannel c) => new(
        c.Id.Value,
        c.SlackWorkspaceId.Value,
        c.BoardId.Value,
        c.ChannelId,
        c.ChannelName,
        Events: c.Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList(),
        Active: c.Active,
        CreatedAt: c.CreatedAt);
}
