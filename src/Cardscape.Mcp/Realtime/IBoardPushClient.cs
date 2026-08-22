using Cardscape.Application.Realtime;
using Cardscape.Application.Abstractions.Realtime;

namespace Cardscape.Mcp.Realtime;

/// <summary>
/// Pushes the same SignalR events the API's own
/// <c>DomainEventBroadcaster</c> emits. The MCP runs in a
/// separate process, so its Wolverine handlers do not trigger
/// the API's <c>IBoardNotifier</c> directly. The MCP HTTP-calls
/// the API's internal <c>/api/internal/broadcast</c> webhook
/// after every successful mutating tool, and the API fans the
/// event out to the matching <c>board:{boardId}</c> SignalR
/// group.
///
/// Auth is a shared secret (<c>Cardscape:Internal:Secret</c> in
/// the MCP config; <c>Internal:Secret</c> in the API config). The
/// MCP <see cref="HttpBoardPushClient"/> forwards it in the
/// <c>X-Internal-Secret</c> header.
/// </summary>
public interface IBoardPushClient
{
    Task PushCardCreatedAsync(CardEventPayload payload, CancellationToken ct = default);
    Task PushCardUpdatedAsync(CardEventPayload payload, CancellationToken ct = default);
    Task PushCardMovedAsync(CardMovedPayload payload, CancellationToken ct = default);
    Task PushCardCompletedAsync(CardEventPayload payload, CancellationToken ct = default);
    Task PushCardReopenedAsync(CardEventPayload payload, CancellationToken ct = default);
    Task PushListCreatedAsync(ListEventPayload payload, CancellationToken ct = default);
    Task PushCommentAddedAsync(CommentEventPayload payload, CancellationToken ct = default);
}
