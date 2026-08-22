namespace Cardscape.Application.Abstractions.Realtime;

/// <summary>
/// Pushes board events to subscribed clients without exposing a concrete
/// realtime transport to Application.
/// </summary>
public interface IBoardNotifier
{
    /// <summary>Broadcasts through the client group associated with a board.</summary>
    Task BroadcastAsync(
        Guid boardId,
        Func<IBoardClient, Task> dispatch,
        CancellationToken ct = default);
}
