using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Boards.Queries;

/// <summary>
/// Tells the caller whether they can manage the board's roster and
/// which role (if any) they hold on it, so clients can hide the
/// controls they cannot use.
/// </summary>
public sealed record GetBoardMemberAccessQuery(Guid BoardId) : IMessage;

public static class GetBoardMemberAccessQueryHandler
{
    public static async Task<Result<BoardMemberAccessDto>> HandleAsync(
        GetBoardMemberAccessQuery query,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardMemberAccessDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(query.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardMemberAccessDto>(BoardErrors.NotFound);
        }

        var workspace = await BoardMemberAccess.LoadWorkspaceAsync(board, workspaces, cancellationToken);
        bool canManage = await BoardMemberAccess.CanManageMembersAsync(
            board, workspace, currentUser.Id, users, cancellationToken);
        BoardMemberRole? role = board.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id.Value)?.Role;

        if (role is null && !canManage)
        {
            return Result.Failure<BoardMemberAccessDto>(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        // Whoever manages the board's roster may invite guests to it by
        // email (the invitation joins them to the workspace as Guest and
        // to this board in one step).
        bool canInviteGuests = canManage && workspace is { IsDeleted: false };
        return Result.Success(new BoardMemberAccessDto(canManage, role, canInviteGuests));
    }
}
