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

public sealed record CreateListCommand(Guid BoardId, string Name)
    : IMessage;

public static class CreateListCommandHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        CreateListCommand command,
        IBoardRepository boards,
        IBoardListRepository lists,
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

        var boardGuard = await MembershipGuards.EnsureCanMutateBoardAsync(
            boards, currentUser.Id.Value, command.BoardId, cancellationToken);
        if (boardGuard.IsFailure)
        {
            return Result.Failure<BoardListDto>(boardGuard.Error);
        }

        var nameResult = ListName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(nameResult.Error);
        }

        // BETA-7-#9 — see test-results/BETA-TEST-REPORT.md.
        // Two new lists in a row used to both get
        // `position: 1` (the `Position.Start()` value), so
        // the second list sorted to the top of the board
        // when the user reloaded. Compute the next position
        // by looking at the existing lists on the board and
        // appending one Position unit past the max.
        IReadOnlyList<BoardList> existing = await lists.ListForBoardAsync(
            new BoardId(command.BoardId), includeArchived: true, cancellationToken);
        // Position is a `readonly record struct` wrapping a double;
        // `Max(l => l.Position)` would have to compare the struct, which
        // is not IComparable. Project to the underlying double so
        // `Enumerable.Max` can compare.
        Position position = existing.Count == 0
            ? Position.Start()
            : Position.From(existing.Max(l => l.Position.Value) + 1.0d);

        var listResult = BoardList.Create(
            BoardListId.New(),
            new BoardId(command.BoardId),
            nameResult.Value,
            position,
            currentUser.Id.Value,
            clock.UtcNow);

        if (listResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(listResult.Error);
        }

        await lists.AddAsync(listResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardListDto(
            listResult.Value.Id.Value,
            listResult.Value.BoardId.Value,
            listResult.Value.Name.Value,
            listResult.Value.Position.Value,
            listResult.Value.IsArchived,
            listResult.Value.CreatedAt,
            0));
    }
}

public sealed record RenameListCommand(Guid ListId, string NewName) : IMessage;

public static class RenameListCommandHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        RenameListCommand command,
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

        var nameResult = ListName.Create(command.NewName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(nameResult.Error);
        }

        var renameResult = list.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(renameResult.Error);
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

public sealed record ArchiveListCommand(Guid ListId) : IMessage;

public static class ArchiveListCommandHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        ArchiveListCommand command,
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

        list.Archive(clock.UtcNow);
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

public sealed record RestoreListCommand(Guid ListId) : IMessage;

public static class RestoreListCommandHandler
{
    public static async Task<Result<BoardListDto>> Handle(
        RestoreListCommand command,
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

        list.Restore(clock.UtcNow);
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
