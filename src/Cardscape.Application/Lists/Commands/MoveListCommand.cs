using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;
using static Cardscape.Domain.Lists.Errors.ListErrors;

namespace Cardscape.Application.Lists.Commands;

public sealed record MoveListCommand(Guid ListId, double NewPosition) : IMessage;

public static class MoveListCommandHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        MoveListCommand command,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var guard = await MembershipGuards.EnsureCanMutateListAsync(
            lists, boards, currentUser.Id.Value, command.ListId, cancellationToken);
        if (guard.IsFailure)
        {
            return Result.Failure<BoardListDto>(guard.Error);
        }

        var list = guard.Value.List;

        // BUG-A4-003 — see test-results/beta/reports/A4-cards-lists.md.
        // `Move` was a no-op-against-collisions: when the UI sent a
        // discrete `position` (1, 2, 3) and the board already had a
        // list at that slot, both ended up with the same
        // `position` value and the sort-by-(position, createdAt)
        // tiebreaker decided the visual order — not the user.
        //
        // BUG-A4-006 — round 2 re-test
        // (test-results/beta/round-2/reports/A4-cards-lists.md).
        // The original fix shifted only the sibling at the exact
        // collision slot. If the board had lists at positions
        // [1, 2, 3] and the UI moved a list to slot 2, the list
        // that was at 2 was bumped to 3, but the list already at
        // 3 stayed at 3 — the cascade was missing and the
        // resulting collision at 3 was decided by the
        // (position, createdAt) tiebreaker. The fix below sorts
        // the colliding siblings descending and shifts each one
        // past the new high-water mark so the invariant "every
        // sibling has a unique (position, createdAt) pair" is
        // preserved without rebuilding the whole sequence.
        Position newPosition = Position.From(command.NewPosition);
        IReadOnlyList<BoardList> siblings = await lists.ListForBoardAsync(
            list.BoardId, includeArchived: false, cancellationToken);
        List<BoardList> colliding = siblings
            .Where(s => s.Id.Value != list.Id.Value
                        && !s.IsArchived
                        && Math.Abs(s.Position.Value - newPosition.Value) < double.Epsilon)
            .OrderBy(s => s.Position.Value)
            .ThenBy(s => s.CreatedAt)
            .ToList();
        // Shift in ascending order; the resulting sequence is
        // monotonically non-decreasing, so each shift can use
        // the new position of the previous sibling as its own
        // base.
        double cursor = newPosition.Value;
        foreach (BoardList sibling in colliding)
        {
            cursor = Math.Max(cursor + 1.0d, sibling.Position.Value + 1.0d);
            sibling.Move(Position.From(cursor), clock.UtcNow);
        }

        var moveResult = list.Move(newPosition, clock.UtcNow);
        if (moveResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(moveResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardListDto(
            list.Id.Value,
            list.BoardId.Value,
            list.Name.Value,
            list.Position.Value,
            list.IsArchived,
            list.CreatedAt,
            0));
    }
}
