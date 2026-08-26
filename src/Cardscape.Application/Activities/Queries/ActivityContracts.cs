using Cardscape.Domain.Activities;

namespace Cardscape.Application.Activities.Queries;

public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    string? ActorDisplayName,
    ActivityKind Kind,
    string PayloadJson,
    DateTimeOffset OccurredAt)
{
    public static ActivityDto FromEntity(Activity activity, IReadOnlyDictionary<Guid, string> actorDisplayNames) => new(
        activity.Id.Value,
        activity.BoardId.Value,
        activity.CardId,
        activity.ActorId,
        actorDisplayNames.GetValueOrDefault(activity.ActorId, string.Empty),
        activity.Kind,
        activity.PayloadJson,
        activity.OccurredAt);
}

/// <summary>
/// One page of the activity timeline. <see cref="NextCursor"/> is <c>null</c>
/// when there are no more items after this page.
/// </summary>
public sealed record ActivityPage(
    IReadOnlyList<ActivityDto> Items,
    string? NextCursor);
