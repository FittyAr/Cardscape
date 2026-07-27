using Cardscape.Application.Realtime;

namespace Cardscape.Api.Hubs;

/// <summary>
/// Server-to-client events broadcast by <see cref="BoardHub"/> when
/// something changes on a board a client is subscribed to. The
/// payload records themselves live in the Application layer so
/// the MCP project can serialise the same shape over its
/// internal broadcast webhook.
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
