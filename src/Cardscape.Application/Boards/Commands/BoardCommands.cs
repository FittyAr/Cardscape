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

        // BETA-2-#4 — see test-results/BETA-TEST-REPORT.md.
        //
        // The JSON binding accepts any int for the
        // BoardVisibility field because the
        // JsonStringEnumConverter is configured with
        // `allowIntegerValues: true` (so the Blazor WASM
        // client can keep sending ints). That same
        // permissiveness lets the caller pass
        // `{"visibility": 99}` and have a board created
        // with an out-of-range enum value. Reject unknown
        // values explicitly so the storage layer never
        // sees a Visibility the domain doesn't recognise.
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

public sealed record RenameBoardCommand(Guid BoardId, string NewName) : IMessage;

public static class RenameBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        RenameBoardCommand command,
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

        var nameResult = BoardName.Create(command.NewName);
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

public sealed record ChangeBoardVisibilityCommand(Guid BoardId, BoardVisibility NewVisibility)
    : IMessage;

public static class ChangeBoardVisibilityCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        ChangeBoardVisibilityCommand command,
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

        // BETA-2-#4 — see test-results/BETA-TEST-REPORT.md.
        // Same range check as the create path; the
        // JsonStringEnumConverter is permissive about
        // integer values, so an out-of-range Visibility
        // would otherwise land on the entity.
        if (!Enum.IsDefined(command.NewVisibility))
        {
            return Result.Failure<BoardDto>(DomainError.Validation(
                "boards.visibility_invalid",
                $"Visibility must be one of: {string.Join(", ", Enum.GetNames<BoardVisibility>())}."));
        }

        var changeResult = board.ChangeVisibility(command.NewVisibility, clock.UtcNow);
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

public sealed record StarBoardCommand(Guid BoardId) : IMessage;

public static class StarBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        StarBoardCommand command,
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

        // BETA-3-#3 — see test-results/BETA-TEST-REPORT.md.
        //
        // Bypass the Board aggregate's RowVersion. The previous
        // "load board → mutate _stars via board.Star() → SaveChanges"
        // pattern violated the optimistic-concurrency token when two
        // tabs toggled at once: both calls loaded the same RowVersion,
        // both tried to save, the second hit DbUpdateConcurrencyException
        // (now 409, but the side effect from the first save was already
        // persisted so the state went out of sync with what the user
        // saw on screen).
        //
        // AddStarIfMissingAsync is a direct INSERT on board_stars
        // that swallows the unique-index violation when the row is
        // already there — the side effect becomes idempotent, no
        // RowVersion is touched, and a re-tried POST /star is a
        // 200-still-starred no-op.
        await boards.AddStarIfMissingAsync(
            new BoardId(command.BoardId), currentUser.Id.Value, clock.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

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

public sealed record UnstarBoardCommand(Guid BoardId) : IMessage;

public static class UnstarBoardCommandHandler
{
    public static async Task<Result<BoardDto>> Handle(
        UnstarBoardCommand command,
        IBoardRepository boards,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is null)
        {
            return Result.Failure<BoardDto>(DomainError.Unauthenticated(
                "auth.required", "Authentication is required."));
        }

        // BETA-3-#3 — symmetric with StarBoardCommandHandler.
        // Direct DELETE on board_stars; missing row is a 200-still-unstarred
        // no-op.
        await boards.RemoveStarIfPresentAsync(
            new BoardId(command.BoardId), currentUser.Id.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var board = await boards.GetByIdAsync(new BoardId(command.BoardId), cancellationToken);
        if (board is null)
        {
            return Result.Failure<BoardDto>(NotFound);
        }

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
