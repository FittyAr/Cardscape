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

        if (events is null)
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.events_required", "At least one event type is required."));
        }

        HashSet<string> normalised = new(StringComparer.OrdinalIgnoreCase);
        foreach (string e in events)
        {
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            string trimmed = e.Trim().ToLowerInvariant();
            if (!SlackEventTypes.IsKnown(trimmed))
            {
                return Result.Failure<SlackChannel>(DomainError.Validation(
                    "slack.event_unknown",
                    $"Unknown Slack event type '{e}'. Allowed: "
                    + string.Join(", ", SlackEventTypes.All)));
            }

            normalised.Add(trimmed);
        }

        if (normalised.Count == 0)
        {
            return Result.Failure<SlackChannel>(DomainError.Validation(
                "slack.events_required", "At least one event type is required."));
        }

        return Result.Success(new SlackChannel(
            id, slackWorkspaceId, boardId,
            channelId.Trim(), channelName.Trim().TrimStart('#'),
            string.Join(",", normalised.OrderBy(s => s, StringComparer.Ordinal)),
            at));
    }

    /// <summary>Replaces the subscribed event list. The list is
    /// canonicalised (lowercase, deduped, sorted) before storing.</summary>
    public Result ChangeEvents(IEnumerable<string> newEvents)
    {
        if (newEvents is null)
        {
            return Result.Failure(DomainError.Validation(
                "slack.events_required", "At least one event type is required."));
        }

        HashSet<string> normalised = new(StringComparer.OrdinalIgnoreCase);
        foreach (string e in newEvents)
        {
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            string trimmed = e.Trim().ToLowerInvariant();
            if (!SlackEventTypes.IsKnown(trimmed))
            {
                return Result.Failure(DomainError.Validation(
                    "slack.event_unknown",
                    $"Unknown Slack event type '{e}'. Allowed: "
                    + string.Join(", ", SlackEventTypes.All)));
            }

            normalised.Add(trimmed);
        }

        if (normalised.Count == 0)
        {
            return Result.Failure(DomainError.Validation(
                "slack.events_required", "At least one event type is required."));
        }

        Events = string.Join(",", normalised.OrderBy(s => s, StringComparer.Ordinal));
        return Result.Success();
    }

    /// <summary>Renames the channel. The id stays the same; the
    /// display name is updated in the local store only (the caller
    /// is responsible for renaming the channel in Slack itself).</summary>
    public Result Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Failure(DomainError.Validation(
                "slack.channel_name_required", "Slack channel name is required."));
        }

        if (newName.Length > 200)
        {
            return Result.Failure(DomainError.Validation(
                "slack.channel_name_too_long",
                "Slack channel name must be 200 characters or fewer."));
        }

        string trimmed = newName.Trim().TrimStart('#');
        if (trimmed == ChannelName)
        {
            return Result.Success();
        }

        ChannelName = trimmed;
        return Result.Success();
    }

    /// <summary>True if this mapping subscribes to the given event.</summary>
    public bool SubscribesTo(string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && !string.IsNullOrEmpty(Events)
        && Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));

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
