using Cardscape.Application.Realtime;

namespace Cardscape.Application.Abstractions.Realtime;

/// <summary>
/// Transport-neutral client contract for board subscription notifications.
/// Presentation hosts adapt this port to SignalR, MCP, or another transport.
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
