namespace Cardscape.Domain.Integrations.Slack;

/// <summary>Identifier of a <see cref="SlackWorkspace"/>.</summary>
public sealed record SlackWorkspaceId(Guid Value) : Common.GuidId<SlackWorkspaceId>(Value);
