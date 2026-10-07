using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Lists.Commands;

public sealed record RenameListCommand(Guid ListId, string NewName) : IMessage;

public static class RenameListCommandHandler
{
    public static async Task<Result<BoardListDto>> HandleAsync(
        RenameListCommand command,
        IBoardListRepository lists,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        var nameResult = ListName.Create(command.NewName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(nameResult.Error);
        }

        BoardList list = guard.Value.List;
        bool renamed = list.Name.Value != nameResult.Value.Value;
        var renameResult = list.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(renameResult.Error);
        }

        if (renamed)
        {
            await activities.AddAsync(Activity.Record(
                list.BoardId,
                null,
                currentUser.Id.Value,
                ActivityKind.ListRenamed,
                clock.UtcNow,
                new { listId = list.Id.Value, name = list.Name.Value }), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(BoardListDto.FromEntity(list));
    }
}
