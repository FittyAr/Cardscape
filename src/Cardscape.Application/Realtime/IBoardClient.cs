namespace Cardscape.Application.Realtime;

/// <summary>
/// Server-to-client events broadcast to every SignalR client
/// subscribed to a board. The interface lives in the Application
/// layer so the Application-side domain event dispatcher can
/// express the fan-out contract without taking a dependency
/// on ASP.NET Core. The API hosts the SignalR <c>BoardHub</c>
/// and provides the implementation; the MCP project consumes
/// the same payload shape through its own broadcast webhook
/// (see <c>McpResourceBroadcaster</c>).
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
