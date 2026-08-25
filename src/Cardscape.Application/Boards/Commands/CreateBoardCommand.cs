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

public sealed record CreateBoardCommand(
    Guid WorkspaceId,
    string Name,
    string? Description,
    BoardVisibility Visibility) : IMessage;

public static class CreateBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        CreateBoardCommand command,
        IBoardRepository boards,
        IRepository<WorkspaceEntity, WorkspaceId> workspaces,
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

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(command.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<BoardDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var nameResult = BoardName.Create(command.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(nameResult.Error);
        }

        var descResult = BoardDescription.Create(command.Description);
        if (descResult.IsFailure)
        {
            return Result.Failure<BoardDto>(descResult.Error);
        }

        // Internal callers can construct an out-of-range enum even though
        // the HTTP contract rejects numeric values, so keep the domain guard.
        if (!Enum.IsDefined(command.Visibility))
        {
            return Result.Failure<BoardDto>(DomainError.Validation(
                "boards.visibility_invalid",
                $"Visibility must be one of: {string.Join(", ", Enum.GetNames<BoardVisibility>())}."));
        }

        var boardResult = BoardEntity.Create(
            BoardId.New(),
            new WorkspaceId(command.WorkspaceId),
            nameResult.Value,
            descResult.Value,
            command.Visibility,
            currentUser.Id.Value,
            clock.UtcNow);

        if (boardResult.IsFailure)
        {
            return Result.Failure<BoardDto>(boardResult.Error);
        }

        await boards.AddAsync(boardResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardDto(
            boardResult.Value.Id.Value,
            boardResult.Value.WorkspaceId.Value,
            boardResult.Value.Name.Value,
            boardResult.Value.Description.Value,
            boardResult.Value.Visibility,
            boardResult.Value.IsArchived,
            false,
            boardResult.Value.CreatedAt,
            boardResult.Value.Members.Count));
    }
}


