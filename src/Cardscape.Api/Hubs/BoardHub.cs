using Cardscape.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cardscape.Api.Hubs;

/// <summary>
/// Real-time hub for one board. Clients join the
/// <c>board:{boardId}</c> group on <see cref="JoinBoard"/>; the
/// <see cref="IBoardNotifier"/> (driven by the
/// <c>BoardEventBroadcaster</c> in the Application layer) pushes
/// the actual events to every group member.
/// </summary>
[Authorize]
public sealed class BoardHub : Hub<IBoardClient>
{
    public async Task JoinBoard(Guid boardId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));
    }

    public async Task LeaveBoard(Guid boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    private static string GroupName(Guid boardId) => $"board:{boardId:N}";
}

/// <summary>
/// Pure-SignalR <see cref="IBoardNotifier"/> implementation
/// (SignalR fan-out only; the MCP process is reached by the
/// <c>CompositeBoardNotifier</c> wrapper that the API
/// registers by default). Lives here so the test host that
/// wants to exercise SignalR alone can register it directly
/// without dragging in the MCP HTTP path.
/// </summary>
public sealed class BoardNotifier(IHubContext<BoardHub, IBoardClient> hub) : IBoardNotifier
{
    public async Task BroadcastAsync(
        Guid boardId,
        Func<IBoardClient, Task> dispatch,
        CancellationToken ct = default)
    {
        await dispatch(hub.Clients.Group($"board:{boardId:N}"));
    }
}
