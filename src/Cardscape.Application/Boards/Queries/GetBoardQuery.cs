using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Mapping;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;

namespace Cardscape.Application.Boards.Queries;

public sealed record GetBoardQuery(Guid BoardId) : IMessage;

public static class GetBoardQueryHandler
{
    public static async Task<Result<BoardDto>> HandleAsync(
        GetBoardQuery query,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        // Workspace-visible boards are open to the workspace's full
        // members only: guests (and outsiders) need explicit membership.
        Guid userId = currentUser.Id.Value;
        if (!board.IsMember(userId) && board.Visibility == BoardVisibility.Workspace)
        {
            Workspace? workspace = await workspaces.GetWithMembersAsync(board.WorkspaceId, cancellationToken);
            if (!WorkspaceGuestRules.CanOpenBoard(board, userId, workspace?.RoleOf(userId)))
            {
                return Result.Failure<BoardDto>(NotMember);
            }
        }
        else if (!WorkspaceGuestRules.CanOpenBoard(board, userId, workspaceRole: null))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        return Result.Success(board.ToDto(board.IsStarredBy(currentUser.Id.Value)));
    }
}
