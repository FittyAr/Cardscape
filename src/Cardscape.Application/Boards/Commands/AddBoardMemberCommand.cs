using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Wolverine;

namespace Cardscape.Application.Boards.Commands;

/// <summary>
/// BETA-5-#12 — see test-results/BETA-TEST-REPORT.md.
///
/// The <see cref="Board"/> aggregate has had an
/// <see cref="Board.AddMember"/> method since v0.1 but the
/// application layer never wrapped it in a command and the
/// API had no HTTP endpoint. Workspace members couldn't be
/// promoted to board members, so any write on a board
/// (create card, comment, vote, etc.) required the actor
/// to be a board member — and there was no way to become
/// one. This command + handler + endpoint closes the loop.
/// </summary>
public sealed record AddBoardMemberCommand(
    Guid BoardId,
    Guid UserId,
    BoardMemberRole Role) : IMessage;

public static class AddBoardMemberCommandHandler
{
    public static async Task<Result> Handle(
        AddBoardMemberCommand command,
        IBoardRepository boards,
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

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        // The actor must already be a board member to add
        // someone else. The workspace is the natural higher
        // level for invites, but the board owner / a board
        // admin is the only one who can adjust the board's
        // own membership roster.
        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(DomainError.Forbidden(
                "boards.forbidden", "You are not a member of this board."));
        }

        var user = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(DomainError.NotFound(
                "users.not_found", "User was not found."));
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
