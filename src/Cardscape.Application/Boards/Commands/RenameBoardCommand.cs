using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Boards.Mapping;
using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using Wolverine;
using static Cardscape.Domain.Boards.Errors.BoardErrors;
using BoardEntity = Cardscape.Domain.Boards.Board;
using WorkspaceEntity = Cardscape.Domain.Workspaces.Workspace;

namespace Cardscape.Application.Boards.Commands;

public sealed record RenameBoardCommand(Guid BoardId, string NewName) : IMessage;

public static class RenameBoardCommandHandler
{
    public static async Task<Result<BoardDto>> HandleAsync(
        RenameBoardCommand command,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock,
        IActivityRepository activities,
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

        var nameResult = BoardName.Create(command.NewName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(nameResult.Error);
        }

        bool renamed = board.Name.Value != nameResult.Value.Value;
        var renameResult = board.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(renameResult.Error);
        }

        if (renamed)
        {
            await activities.AddAsync(Activity.Record(
                board.Id,
                null,
                currentUser.Id.Value,
                ActivityKind.BoardRenamed,
                clock.UtcNow,
                new { name = board.Name.Value }), cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(board.ToDto(board.IsStarredBy(currentUser.Id.Value)));
    }
}

