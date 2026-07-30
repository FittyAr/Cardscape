namespace Cardscape.Application.Realtime;

/// <summary>
/// Pushes a board-level "something changed" signal from the
/// API to the MCP process. The MCP fans the signal out to
/// every AI client that has called <c>resources/subscribe</c>
/// on the matching <c>board://{id}</c> URI by emitting
/// <c>notifications/resources/updated</c>.
///
/// The default implementation in the API is an HTTP client
/// that POSTs to the MCP's <c>/api/internal/board-event</c>
/// endpoint with the same shared secret the MCP uses when
/// it calls the API's <c>/api/internal/broadcast</c>. The
/// interface lives in the Application layer so the API's
/// <c>DomainEventBroadcaster</c> can depend on it without
/// referencing the MCP process.
/// </summary>
public interface IMcpResourceNotifier
{
    Task NotifyAsync(Guid boardId, CancellationToken ct = default);
}
