using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Activities;

/// <summary>
/// Append-only activity entry for a board. The activity log is
/// populated by handlers of other contexts' domain events.
/// </summary>
public sealed class Activity : Entity<ActivityId>
{
    public BoardId BoardId { get; private set; } = null!;
    public Guid? CardId { get; private set; }
    public Guid ActorId { get; private set; }
    public ActivityKind Kind { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }

    private Activity() { }

    private Activity(
        ActivityId id,
        BoardId boardId,
        Guid? cardId,
        Guid actorId,
        ActivityKind kind,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = id;
        BoardId = boardId;
        CardId = cardId;
        ActorId = actorId;
        Kind = kind;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        CreatedAt = occurredAt;
    }

    public static Activity Create(
        BoardId boardId,
        Guid? cardId,
        Guid actorId,
        ActivityKind kind,
        string payloadJson,
        DateTimeOffset occurredAt) =>
        new(ActivityId.New(), boardId, cardId, actorId, kind, payloadJson ?? "{}", occurredAt);
}
