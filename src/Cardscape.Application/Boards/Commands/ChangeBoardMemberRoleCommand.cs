using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Boards.Commands;

/// <summary>
/// Changes a board member's role. Only board-roster managers may do
/// it, and the aggregate keeps at least one board Admin. A workspace
/// guest cannot be promoted to board Admin.
/// </summary>
public sealed record ChangeBoardMemberRoleCommand(
    Guid BoardId,
    Guid UserId,
    BoardMemberRole Role) : IMessage;

public static class ChangeBoardMemberRoleCommandHandler
{
    public static async Task<Result> HandleAsync(
        ChangeBoardMemberRoleCommand command,
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        Board? board = await boards.GetWithMembersAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure(BoardErrors.NotFound);
        }

        var workspace = await BoardMemberAccess.LoadWorkspaceAsync(board, workspaces, cancellationToken);
        if (!await BoardMemberAccess.CanManageMembersAsync(board, workspace, currentUser.Id, users, cancellationToken))
        {
            return Result.Failure(BoardErrors.Forbidden);
        }

        if (!WorkspaceGuestRules.AllowsBoardRole(workspace?.RoleOf(command.UserId), command.Role))
        {
            return Result.Failure(BoardErrors.GuestCannotBeAdmin);
        }

        var changeResult = board.ChangeMemberRole(command.UserId, command.Role, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return changeResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
