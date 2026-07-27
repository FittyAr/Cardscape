namespace Cardscape.Api.Hubs;

/// <summary>
/// Server-to-client events broadcast by <see cref="BoardHub"/> when
/// something changes on a board a client is subscribed to. Pure
/// data records so the wire shape stays stable.
/// </summary>
public interface IBoardClient
{
    Task CardCreated(CardEventPayload payload);
    Task CardUpdated(CardEventPayload payload);
    Task CardMoved(CardMovedPayload payload);
    Task CardCompleted(CardEventPayload payload);
    Task CardReopened(CardEventPayload payload);
    Task CardArchived(CardEventPayload payload);
    Task CardRestored(CardEventPayload payload);
    Task CardAssigned(CardAssignedPayload payload);
    Task CardUnassigned(CardAssignedPayload payload);
    Task CardLabelAttached(CardLabelPayload payload);
    Task CardLabelDetached(CardLabelPayload payload);
    Task ListCreated(ListEventPayload payload);
    Task ListRenamed(ListEventPayload payload);
    Task ListArchived(ListEventPayload payload);
    Task ListRestored(ListEventPayload payload);
    Task CommentAdded(CommentEventPayload payload);
    Task LabelCreated(LabelEventPayload payload);
    Task BoardRenamed(BoardEventPayload payload);
    Task BoardStarred(BoardEventPayload payload);
    Task BoardUnstarred(BoardEventPayload payload);
}

public sealed record CardEventPayload(
    Guid CardId,
    Guid BoardId,
    Guid ListId,
    string Title,
    DateTimeOffset At);

public sealed record CardMovedPayload(
    Guid CardId,
    Guid BoardId,
    Guid FromListId,
    Guid ToListId,
    double NewPosition,
    DateTimeOffset At);

public sealed record CardAssignedPayload(
    Guid CardId,
    Guid BoardId,
    Guid UserId,
    DateTimeOffset At);

public sealed record CardLabelPayload(
    Guid CardId,
    Guid BoardId,
    Guid LabelId,
    DateTimeOffset At);

public sealed record ListEventPayload(
    Guid ListId,
    Guid BoardId,
    string Name,
    DateTimeOffset At);

public sealed record CommentEventPayload(
    Guid CommentId,
    Guid CardId,
    Guid BoardId,
    Guid AuthorId,
    DateTimeOffset At);

public sealed record LabelEventPayload(
    Guid LabelId,
    Guid BoardId,
    string Name,
    string Color,
    DateTimeOffset At);

public sealed record BoardEventPayload(
    Guid BoardId,
    string Name,
    DateTimeOffset At);
