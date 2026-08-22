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

public sealed record ArchiveBoardCommand(Guid BoardId) : IMessage;

public static class ArchiveBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        ArchiveBoardCommand command,
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

        board.Archive(clock.UtcNow);
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

public sealed record UnarchiveBoardCommand(Guid BoardId) : IMessage;

public static class UnarchiveBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        UnarchiveBoardCommand command,
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

        board.Unarchive(clock.UtcNow);
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

public sealed record DeleteBoardCommand(Guid BoardId) : IMessage;

public static class DeleteBoardCommandHandler
{
    public static async Task<Result> Handle(
        DeleteBoardCommand command,
        IBoardRepository boards,
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

        Board? board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        // Membership required (Kanban-style — board admin /
        // member can delete; non-member cannot).
        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure(NotMember);
        }

        var delete = board.Delete(currentUser.Id.Value, clock.UtcNow);
        if (delete.IsFailure)
        {
            return delete;
        }

        boards.Remove(board);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
