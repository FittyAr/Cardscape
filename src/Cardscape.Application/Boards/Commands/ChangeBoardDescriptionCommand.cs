using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;
using BoardEntity = Cardscape.Domain.Boards.Board;
using WorkspaceEntity = Cardscape.Domain.Workspaces.Workspace;

namespace Cardscape.Application.Boards.Commands;

public sealed record ChangeBoardDescriptionCommand(Guid BoardId, string NewDescription)
    : IMessage;

public static class ChangeBoardDescriptionCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        ChangeBoardDescriptionCommand command,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var descResult = BoardDescription.Create(command.NewDescription);
        if (descResult.IsFailure)
        {
            return Result.Failure<BoardDto>(descResult.Error);
        }

        var changeResult = board.ChangeDescription(descResult.Value, clock.UtcNow);
        if (changeResult.IsFailure)
        {
            return Result.Failure<BoardDto>(changeResult.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardDto(
            board.Id.Value,
            board.WorkspaceId.Value,
            board.Name.Value,
            board.Description.Value,
            board.Visibility,
            board.IsArchived,
            board.IsStarredBy(currentUser.Id.Value),
            board.CreatedAt,
            board.Members.Count));
    }
}


