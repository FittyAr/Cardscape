namespace Cardscape.Domain.Integrations.Slack;

/// <summary>Identifier of a <see cref="SlackChannel"/> mapping.</summary>
public sealed record SlackChannelId(Guid Value) : Common.GuidId<SlackChannelId>(Value);
