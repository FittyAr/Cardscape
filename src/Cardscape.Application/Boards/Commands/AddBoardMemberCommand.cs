using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;
using Wolverine;

namespace Cardscape.Application.Boards.Commands;

/// <summary>
/// Adds a member of the board's workspace to the board with the given
/// role. Only people who can manage the board's roster (board Admins,
/// the workspace owner / workspace Admins, active instance admins)
/// may add members; the added user must already belong to the
/// board's workspace. Workspace guests can be added as board Members
/// or Observers, never as board Admins.
/// </summary>
public sealed record AddBoardMemberCommand(
    Guid BoardId,
    Guid UserId,
    BoardMemberRole Role) : IMessage;

public static class AddBoardMemberCommandHandler
{
    public static async Task<Result> HandleAsync(
        AddBoardMemberCommand command,
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

        var user = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (user is null || user.IsDeleted)
        {
            return Result.Failure(DomainError.NotFound(
                "users.not_found", "User was not found."));
        }

        if (workspace is null || !workspace.HasMember(command.UserId))
        {
            return Result.Failure(BoardErrors.MemberNotInWorkspace);
        }

        if (!WorkspaceGuestRules.AllowsBoardRole(workspace.RoleOf(command.UserId), command.Role))
        {
            return Result.Failure(BoardErrors.GuestCannotBeAdmin);
        }

        var addResult = board.AddMember(command.UserId, command.Role, clock.UtcNow);
        if (addResult.IsFailure)
        {
            return Result.Failure(addResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
