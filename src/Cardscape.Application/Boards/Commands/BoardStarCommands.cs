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

// BETA-A3-R2-001 — see test-results/beta/round-2/reports/A3-boards.md.
// The board lifecycle was missing a delete endpoint. The
// round-2 destructive-test plan needs it, and the previous
// round-1 deferred the surface. Delete is hard (the
// per-board content is user-owned and the user authorized
// destructive test runs in this round). Lists, cards, and
// attachments cascade via the EF Core cascade rules in
// `CardscapeDbContext.OnModelCreating`.
