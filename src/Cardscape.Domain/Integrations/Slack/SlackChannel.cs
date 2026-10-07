using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Integrations.Slack;

/// <summary>
/// A board-to-Slack-channel mapping. The channel lives on the
/// team identified by <see cref="SlackWorkspace.TeamId"/>;
/// <see cref="ChannelId"/> is the Slack channel id (<c>C…</c>).
/// The <see cref="Events"/> field is the canonicalised comma-joined
/// list of subscribed Slack event types from
/// <see cref="SlackEventTypes"/>. Soft-deleted mappings stay in
/// the table so the audit history is preserved.
/// </summary>
public sealed class SlackChannel : AggregateRoot<SlackChannelId>
{
    public SlackWorkspaceId SlackWorkspaceId { get; private set; } = null!;

    public BoardId BoardId { get; private set; } = null!;

    /// <summary>Slack channel id (e.g. <c>C01ABCD2EFG</c>).</summary>
    public string ChannelId { get; private set; } = string.Empty;

    /// <summary>Human-readable channel name (without leading <c>#</c>).</summary>
    public string ChannelName { get; private set; } = string.Empty;

    /// <summary>Comma-joined list of subscribed event types
    /// (e.g. <c>"card.created,card.moved"</c>).</summary>
    public string Events { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    // EF Core.
    private SlackChannel() { }

    private SlackChannel(
        SlackChannelId id,
        SlackWorkspaceId slackWorkspaceId,
        BoardId boardId,
        string channelId,
        string channelName,
        string events,
        DateTimeOffset at)
    {
        Id = id;
        SlackWorkspaceId = slackWorkspaceId;
        BoardId = boardId;
        ChannelId = channelId;
        ChannelName = channelName;
        Events = events;
        Active = true;
        CreatedAt = at;
    }

    public static Result<SlackChannel> Link(
        SlackChannelId id,
        SlackWorkspaceId slackWorkspaceId,
        BoardId boardId,
        string channelId,
        string channelName,
        IEnumerable<string> events,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.channel_id_required", "Slack channel id is required."));
        }

        if (channelId.Length > 32)
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.channel_id_too_long", "Slack channel id must be 32 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(channelName))
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.channel_name_required", "Slack channel name is required."));
        }

        if (channelName.Length > 200)
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.channel_name_too_long",
                "Slack channel name must be 200 characters or fewer."));
        }

        Result<string> subscription = SlackEventTypes.Catalog.ToSubscription(events);
        if (subscription.IsFailure)
        {
            return Result.Failure<SlackChannel>(subscription.Error);
        }

        return Result.Success(new SlackChannel(
            id, slackWorkspaceId, boardId,
            channelId.Trim(), channelName.Trim().TrimStart('#'),
            subscription.Value,
            at));
    }

    /// <summary>True if this mapping subscribes to the given event.</summary>
    public bool SubscribesTo(string eventType) =>
        EventCatalog.Includes(Events, eventType);

    /// <summary>Disables the mapping without deleting it. Idempotent.</summary>
    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }
}
