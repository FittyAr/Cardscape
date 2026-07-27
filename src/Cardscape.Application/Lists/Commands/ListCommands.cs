using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using MediatR;
using static Cardscape.Domain.Lists.Errors.ListErrors;

namespace Cardscape.Application.Lists.Commands;

public sealed record CreateListCommand(Guid BoardId, string Name)
    : IRequest<Result<BoardListDto>>;

public sealed class CreateListCommandHandler(
    IBoardListRepository lists,
    IBoardRepository boards,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<CreateListCommand, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        CreateListCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var board = await boards.GetByIdAsync(new BoardId(request.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardListDto>(DomainError.NotFound(
                "boards.not_found", "Board was not found."));
        }

        if (!board.IsMember(currentUser.Id.Value))
        {
            return Result.Failure<BoardListDto>(DomainError.Forbidden(
                "boards.not_member", "You are not a member of this board."));
        }

        var nameResult = ListName.Create(request.Name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<BoardListDto>(nameResult.Error);
        }

        var listResult = BoardList.Create(
            BoardListId.New(),
            new BoardId(request.BoardId),
            nameResult.Value,
            Position.Start(),
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

public sealed record RenameListCommand(Guid ListId, string NewName) : IRequest<Result<BoardListDto>>;

public sealed class RenameListCommandHandler(
    IBoardListRepository lists,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RenameListCommand, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        RenameListCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<BoardListDto>(NotFound);
        }

        var nameResult = ListName.Create(request.NewName);
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

public sealed record MoveListCommand(Guid ListId, double NewPosition) : IRequest<Result<BoardListDto>>;

public sealed class MoveListCommandHandler(
    IBoardListRepository lists,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<MoveListCommand, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        MoveListCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<BoardListDto>(NotFound);
        }

        var moveResult = list.Move(Position.From(request.NewPosition), clock.UtcNow);
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

public sealed record ArchiveListCommand(Guid ListId) : IRequest<Result<BoardListDto>>;

public sealed class ArchiveListCommandHandler(
    IBoardListRepository lists,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<ArchiveListCommand, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        ArchiveListCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<BoardListDto>(NotFound);
        }

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

public sealed record RestoreListCommand(Guid ListId) : IRequest<Result<BoardListDto>>;

public sealed class RestoreListCommandHandler(
    IBoardListRepository lists,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock) : IRequestHandler<RestoreListCommand, Result<BoardListDto>>
{
    public async Task<Result<BoardListDto>> Handle(
        RestoreListCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardListDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        var list = await lists.GetByIdAsync(new BoardListId(request.ListId), cancellationToken);
        if (list is null)
        {
            return Result.Failure<BoardListDto>(NotFound);
        }

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
