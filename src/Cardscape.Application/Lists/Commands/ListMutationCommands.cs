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
