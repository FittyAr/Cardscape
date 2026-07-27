namespace Cardscape.Application.Realtime;

/// <summary>
/// Wire-shape records for the server-to-client events on
/// <c>Cardscape.Api.Hubs.IBoardClient</c>. They live in the
/// Application layer so both the API (which produces them via
/// <c>IBoardNotifier</c>) and the MCP (which produces them by
/// calling the internal broadcast endpoint over HTTP) can share
/// one canonical shape without duplicating records.
/// </summary>
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
