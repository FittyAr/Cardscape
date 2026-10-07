using Cardscape.Api.Logging;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Cardscape.Api.Hubs;

/// <summary>
/// Real-time hub for one board. Clients join the
/// <c>board:{boardId}</c> group on <see cref="JoinBoardAsync"/>; the
/// <see cref="IBoardNotifier"/> (driven by the
/// <c>BoardEventBroadcaster</c> in the Application layer) pushes
/// the actual events to every group member.
/// </summary>
[Authorize]
public sealed class BoardHub(
    IBoardRepository boards,
    ICurrentUser currentUser,
    ILogger<BoardHub> logger) : Hub<IBoardClient>
{
    public async Task JoinBoardAsync(Guid boardId)
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
        if (currentUser.Id is null)
        {
            throw new HubException("Authentication required to join a board group.");
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(boardId));
        if (board is null || !board.IsMember(currentUser.Id.Value))
        {
            logger.BoardHubJoinRejected(boardId, currentUser.Id.Value);
            // Generic message — we don't leak whether the
            // board exists or the user is just not a member.
            throw new HubException("You are not a member of that board.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));
    }

    public async Task LeaveBoardAsync(Guid boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));
    }

    /// <summary>SignalR group every member viewing <paramref name="boardId"/> joins.</summary>
    internal static string GroupName(Guid boardId) => $"board:{boardId:N}";
}
