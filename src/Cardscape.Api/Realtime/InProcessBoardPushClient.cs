using Cardscape.Application.Realtime;
using Cardscape.Mcp.Realtime;

namespace Cardscape.Api.Realtime;

/// <summary>
/// <see cref="IBoardPushClient"/> for the MCP server hosted inside the API.
/// The standalone MCP process pushes tool changes to the API's SignalR hub
/// over HTTP; in-process the same change already raised a domain event that
/// the API's board broadcaster fans out to SignalR (and to subscribed MCP
/// sessions), so pushing again would deliver every event twice.
/// </summary>
public sealed class InProcessBoardPushClient : IBoardPushClient
{
    public Task PushCardCreatedAsync(CardEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushCardUpdatedAsync(CardEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushCardMovedAsync(CardMovedPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushCardCompletedAsync(CardEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushCardReopenedAsync(CardEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushListCreatedAsync(ListEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushCommentAddedAsync(CommentEventPayload payload, CancellationToken ct = default) => Task.CompletedTask;
}
