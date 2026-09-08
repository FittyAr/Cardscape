using Cardscape.Api.Hubs;
using Cardscape.Application.Abstractions.Realtime;
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
/// notify is awaited. Transient failures propagate to the domain-event outbox
/// so this broadcaster delivery remains pending and is retried.
/// </summary>
public sealed class CompositeBoardNotifier : IBoardNotifier
{
    private readonly IHubContext<BoardHub, IBoardClient> _hub;
    private readonly HttpMcpResourceNotifier _mcpNotifier;

    public CompositeBoardNotifier(
        IHubContext<BoardHub, IBoardClient> hub,
        HttpMcpResourceNotifier mcpNotifier)
    {
        _hub = hub;
        _mcpNotifier = mcpNotifier;
    }

    public async Task BroadcastAsync(
        Guid boardId,
        Func<IBoardClient, Task> dispatch,
        CancellationToken ct = default)
    {
        await dispatch(_hub.Clients.Group($"board:{boardId:N}"));
        await _mcpNotifier.NotifyAsync(boardId, ct);
    }
}
