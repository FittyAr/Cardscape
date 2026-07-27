namespace Cardscape.Web.Shared;

// ── SignalR payload types (mirror the API's IBoardClient shape) ────────
// Kept in the Web project (no project reference to the API). Both
// sides ship these records in lock-step; if the API grows an
// event, add the matching record here too.

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
