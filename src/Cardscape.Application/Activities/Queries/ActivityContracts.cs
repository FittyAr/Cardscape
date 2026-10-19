using Cardscape.Domain.Activities;

namespace Cardscape.Application.Activities.Queries;

/// <param name="AgentId">Set when the actor's AI agent made the change (ADR 0015).</param>
/// <param name="AgentName">The agent's name when <paramref name="AgentId"/> is set.</param>
public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    string? ActorDisplayName,
    ActivityKind Kind,
    string PayloadJson,
    DateTimeOffset OccurredAt,
    Guid? AgentId = null,
    string? AgentName = null)
{
    public static ActivityDto FromEntity(Activity activity, IReadOnlyDictionary<Guid, string> actorDisplayNames) => new(
        activity.Id.Value,
        activity.BoardId.Value,
        activity.CardId,
        activity.ActorId,
        actorDisplayNames.GetValueOrDefault(activity.ActorId, string.Empty),
        activity.Kind,
        activity.PayloadJson,
        activity.OccurredAt,
        activity.AgentId,
        activity.AgentName);
}

/// <summary>
/// One page of the activity timeline. <see cref="NextCursor"/> is <c>null</c>
/// when there are no more items after this page.
/// </summary>
public sealed record ActivityPage(
    IReadOnlyList<ActivityDto> Items,
    string? NextCursor);
