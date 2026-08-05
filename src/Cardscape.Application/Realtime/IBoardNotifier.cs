namespace Cardscape.Application.Realtime;

/// <summary>
/// Server-side entry point for pushing events to subscribed
/// clients. The API provides two implementations:
/// <list type="bullet">
///   <item><c>BoardNotifier</c> — pure SignalR fan-out
///     (used when the Web client and the API live in the
///     same process, e.g. the development host).</item>
///   <item><c>CompositeBoardNotifier</c> — SignalR + the
///     MCP process. The MCP-side fan-out is HTTP through
///     the shared <c>Cardscape.Mcp</c> named client;
///     AI clients subscribed to a board's resource get
///     a <c>notifications/resources/updated</c> push.</item>
/// </list>
/// </summary>
public interface IBoardNotifier
{
    /// <summary>
    /// Invokes the supplied dispatch lambda against every
    /// client subscribed to the board. The lambda receives
    /// the <see cref="IBoardClient"/> proxy for the
    /// <c>board:{boardId}</c> SignalR group; the broadcast
    /// implementation chooses the transport (SignalR,
    /// HTTP, both).
    /// </summary>
    Task BroadcastAsync(
        Guid boardId,
        Func<IBoardClient, Task> dispatch,
        CancellationToken ct = default);
}
