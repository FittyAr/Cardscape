using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Wolverine;

namespace Cardscape.Application.Lists.Commands;

public sealed record CreateListCommand(Guid BoardId, string Name) : IMessage;

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

        var boardId = new BoardId(command.BoardId);
        Position position = await lists.GetNextPositionAsync(boardId, cancellationToken);
        var listResult = BoardList.Create(
            BoardListId.New(),
            boardId,
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

        return Result.Success(BoardListDto.FromEntity(listResult.Value));
    }
}
