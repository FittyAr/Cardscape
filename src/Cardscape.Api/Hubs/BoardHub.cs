using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cardscape.Api.Hubs;

/// <summary>
/// Real-time hub for one board. Clients join the
/// <c>board:{boardId}</c> group on <see cref="JoinBoard"/>; the
/// <see cref="BoardNotifier"/> (driven by Wolverine domain-event
/// handlers) pushes the actual events to every group member.
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
/// Server-side entry point for pushing events to subscribed clients.
/// Resolved from DI; called by the Wolverine domain-event handlers
/// that fire on every command.
/// </summary>
public interface IBoardNotifier
{
    Task BroadcastAsync(Guid boardId, Func<IBoardClient, Task> dispatch, CancellationToken ct = default);
}

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
