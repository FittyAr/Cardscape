using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Workspaces;
using MediatR;
using static Cardscape.Domain.Boards.Errors.BoardErrors;
using BoardEntity = Cardscape.Domain.Boards.Board;
using WorkspaceEntity = Cardscape.Domain.Workspaces.Workspace;

namespace Cardscape.Application.Boards.Commands;

public sealed record CreateBoardCommand(
    Guid WorkspaceId,
    string Name,
    string? Description,
    BoardVisibility Visibility) : IRequest<Result<BoardDto>>;

public sealed class CreateBoardCommandHandler(
    IBoardRepository boards,
    IRepository<WorkspaceEntity, WorkspaceId> workspaces,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        CreateBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var workspace = await workspaces.GetByIdAsync(new WorkspaceId(request.WorkspaceId), cancellationToken);
        if (workspace is null)
        {
            return Result.Failure<BoardDto>(DomainError.NotFound(
                "workspaces.not_found", "Workspace was not found."));
        }

        if (!workspace.HasMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var nameResult = BoardName.Create(request.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(nameResult.Error);
        }

        var descResult = BoardDescription.Create(request.Description);
        if (descResult.IsFailure)
        {
            return Result.Failure<BoardDto>(descResult.Error);
        }

        var boardResult = BoardEntity.Create(
            BoardId.New(),
            new WorkspaceId(request.WorkspaceId),
            nameResult.Value,
            descResult.Value,
            request.Visibility,
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

public sealed record RenameBoardCommand(Guid BoardId, string NewName) : IRequest<Result<BoardDto>>;

public sealed class RenameBoardCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RenameBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        RenameBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var nameResult = BoardName.Create(request.NewName);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(nameResult.Error);
        }

        var renameResult = board.Rename(nameResult.Value, clock.UtcNow);
        if (renameResult.IsFailure)
        {
            return Result.Failure<BoardDto>(renameResult.Error);
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

public sealed record ChangeBoardDescriptionCommand(Guid BoardId, string NewDescription)
    : IRequest<Result<BoardDto>>;

public sealed class ChangeBoardDescriptionCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ChangeBoardDescriptionCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        ChangeBoardDescriptionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var descResult = BoardDescription.Create(request.NewDescription);
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

public sealed record ChangeBoardVisibilityCommand(Guid BoardId, BoardVisibility NewVisibility)
    : IRequest<Result<BoardDto>>;

public sealed class ChangeBoardVisibilityCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ChangeBoardVisibilityCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        ChangeBoardVisibilityCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardDto>(NotMember);
        }

        var changeResult = board.ChangeVisibility(request.NewVisibility, clock.UtcNow);
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

public sealed record ArchiveBoardCommand(Guid BoardId) : IRequest<Result<BoardDto>>;

public sealed class ArchiveBoardCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ArchiveBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        ArchiveBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
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

public sealed record UnarchiveBoardCommand(Guid BoardId) : IRequest<Result<BoardDto>>;

public sealed class UnarchiveBoardCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<UnarchiveBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        UnarchiveBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
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

public sealed record StarBoardCommand(Guid BoardId) : IRequest<Result<BoardDto>>;

public sealed class StarBoardCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<StarBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        StarBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        var result = board.Star(currentUser.Id.Value, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<BoardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardDto(
            board.Id.Value,
            board.WorkspaceId.Value,
            board.Name.Value,
            board.Description.Value,
            board.Visibility,
            board.IsArchived,
            true,
            board.CreatedAt,
            board.Members.Count));
    }
}

public sealed record UnstarBoardCommand(Guid BoardId) : IRequest<Result<BoardDto>>;

public sealed class UnstarBoardCommandHandler(
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<UnstarBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        UnstarBoardCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

        var result = board.Unstar(currentUser.Id.Value, clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<BoardDto>(result.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new BoardDto(
            board.Id.Value,
            board.WorkspaceId.Value,
            board.Name.Value,
            board.Description.Value,
            board.Visibility,
            board.IsArchived,
            false,
            board.CreatedAt,
            board.Members.Count));
    }
}
