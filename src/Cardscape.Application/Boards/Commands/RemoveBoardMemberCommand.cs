using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Wolverine;

namespace Cardscape.Application.Boards.Commands;

/// <summary>
/// Removes a member from a board. Board-roster managers can remove
/// anyone; any member can remove themselves (leave the board). The
/// aggregate refuses to remove the last board Admin.
/// </summary>
public sealed record RemoveBoardMemberCommand(Guid BoardId, Guid UserId) : IMessage;

public static class RemoveBoardMemberCommandHandler
{
    public static async Task<Result> HandleAsync(
        RemoveBoardMemberCommand command,
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

        bool leaving = command.UserId == currentUser.Id.Value && board.IsMember(command.UserId);
        if (!leaving)
        {
            var workspace = await BoardMemberAccess.LoadWorkspaceAsync(board, workspaces, cancellationToken);
            if (!await BoardMemberAccess.CanManageMembersAsync(board, workspace, currentUser.Id, users, cancellationToken))
            {
                return Result.Failure(BoardErrors.Forbidden);
            }
        }

        var removeResult = board.RemoveMember(command.UserId, clock.UtcNow);
        if (removeResult.IsFailure)
        {
            return removeResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
