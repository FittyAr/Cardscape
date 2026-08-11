using Cardscape.Api.Hubs;
using Cardscape.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Cardscape.Api.Realtime;

/// <summary>
/// The default <see cref="IBoardNotifier"/> in the API: fans
/// a board event out to both the SignalR group (Web clients)
/// and the MCP process (so subscribed AI clients receive a
/// <c>notifications/resources/updated</c> push). The SignalR
/// dispatch is awaited synchronously so the local Web clients
/// see the change in the same request that caused it; the MCP
/// notify is fire-and-forget — it is best-effort and the
/// <see cref="HttpMcpResourceNotifier"/> swallows transient
/// failures itself.
/// </summary>
public sealed class CompositeBoardNotifier : IBoardNotifier
{
    private readonly IHubContext<BoardHub, IBoardClient> hub;
    private readonly HttpMcpResourceNotifier mcpNotifier;

    public CompositeBoardNotifier(
        IHubContext<BoardHub, IBoardClient> hub,
        HttpMcpResourceNotifier mcpNotifier)
    {
        this.hub = hub;
        this.mcpNotifier = mcpNotifier;
    }

    public async Task BroadcastAsync(
        Guid boardId,
        Func<IBoardClient, Task> dispatch,
        CancellationToken ct = default)
    {
        await dispatch(hub.Clients.Group($"board:{boardId:N}"));
        await mcpNotifier.NotifyAsync(boardId, ct);
    }
}
