using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Realtime;
using Cardscape.Domain.Boards;
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
    private readonly IBoardRepository _boards;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<BoardHub> _logger;

    public BoardHub(
        IBoardRepository boards,
        ICurrentUser currentUser,
        ILogger<BoardHub> logger)
    {
        _boards = boards;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task JoinBoard(Guid boardId)
    {
        // SECURITY: a logged-in user is not, by default, a
        // member of every board. The hub MUST check board
        // membership before adding the connection to the
        // group; otherwise a non-member can subscribe to
        // real-time updates (cardCreated, cardMoved, comments,
        // etc.) for any board whose Guid they can guess. The
        // Guid space makes blind enumeration impractical,
        // but a leaked Guid (e.g. via a search response, a
        // shared link, or a notification payload) was enough
        // for a real-time IDOR.
        if (_currentUser.Id is null)
        {
            throw new HubException("Authentication required to join a board group.");
        }

        Board? board = await _boards.GetWithMembersAsync(new BoardId(boardId));
        if (board is null || !board.IsMember(_currentUser.Id.Value))
        {
            _logger.LogWarning(
                "Rejected JoinBoard for board {BoardId}: user {UserId} is not a member.",
                boardId, _currentUser.Id.Value);
            // Generic message — we don't leak whether the
            // board exists or the user is just not a member.
            throw new HubException("You are not a member of that board.");
        }

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
