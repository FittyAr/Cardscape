using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class BoardRepository(CardscapeDbContext db) : RepositoryBase<Board, BoardId>(db), IBoardRepository
{
    public async Task<IReadOnlyList<Board>> ListForWorkspaceAsync(WorkspaceId workspaceId, CancellationToken ct = default)
    {
        var wsId = workspaceId.Value;
        // Strongly-typed id LINQ translation isn't reliable with the
        // current EF Core 10 + HasConversion combination; load the
        // table and filter in memory. Bounded by the number of boards
        // a workspace contains in practice.
        var rows = new List<Board>();
        await foreach (var b in Db.Set<Board>().Include(b => b.Stars).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (b.WorkspaceId.Value != wsId || b.IsDeleted)
            {
                continue;
            }

            rows.Add(b);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    public async Task<IReadOnlyList<Board>> ListStarredByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = new List<Board>();
        await foreach (var b in Db.Set<Board>().Include(b => b.Stars).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (b.IsDeleted || !b.Stars.Any(s => s.UserId == userId))
            {
                continue;
            }

            rows.Add(b);
        }

        rows.Sort((a, b) => string.Compare(a.Name.Value, b.Name.Value, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    public async Task<Board?> GetWithMembersAsync(BoardId id, CancellationToken ct = default)
    {
        // EF Core 10 + HasConversion: EF.Property<Guid>(b, "Id") trips
        // the converter pipeline (InvalidCastException: Object must
        // implement IConvertible). b.Id == id is the safe form.
        return await Db.Set<Board>()
            .Include(b => b.Members)
            .Include(b => b.Stars)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    // BETA-3-#3 — see test-results/BETA-TEST-REPORT.md.
    //
    // The previous star / unstar flow went through
    // board.Star(userId) / board.Unstar(userId) on the Board
    // aggregate, which mutated the in-memory _stars
    // collection and called SaveChangesAsync. Two
    // concurrent calls both loaded the same Board (same
    // RowVersion), both added/removed the same star, both
    // saved — the second hit DbUpdateConcurrencyException
    // (now caught as 409 by GlobalExceptionMiddleware, but
    // the state was still inconsistent: 25 toggles + 25
    // untoggles should net to "not starred" but ended up
    // "starred" because the failed attempt left the side
    // effect in place).
    //
    // The new path bypasses the Board's RowVersion entirely:
    // a direct INSERT (with a try/catch on the unique-index
    // violation) or DELETE on the board_stars table, scoped
    // to (BoardId, UserId). Both ops are idempotent at the
    // SQL level — a re-tried POST /star with the same
    // (boardId, userId) is a no-op, the response is "200
    // starred" either way, and no aggregate RowVersion is
    // touched.

    public async Task<bool> AddStarIfMissingAsync(
        BoardId boardId, Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be non-empty.", nameof(userId));
        }

        // AsAsyncEnumerable so the existence probe yields rows
        // lazily without blocking a thread-pool worker (the
        // previous Task.Run + AsEnumerable().Any form would park
        // a worker for the duration of the SQL round-trip).
        bool exists = false;
        await foreach (var s in Db.Set<BoardStar>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (s.BoardId.Value == boardId.Value && s.UserId == userId)
            {
                exists = true;
                break;
            }
        }
        if (exists)
        {
            return false;
        }

        var star = BoardStar.Create(boardId, userId, at);
        try
        {
            await Db.Set<BoardStar>().AddAsync(star, ct);
            await Db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent INSERT: the
            // unique (BoardId, UserId) index rejected our
            // row. The other caller's row is now in place,
            // which is exactly the state we wanted. Detach
            // our entity so the DbContext stays clean for
            // the next call.
            Db.Entry(star).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> RemoveStarIfPresentAsync(
        BoardId boardId, Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be non-empty.", nameof(userId));
        }

        // Same shape as AddStarIfMissingAsync: true async stream
        // scan with early-exit on the first match.
        BoardStar? existing = null;
        await foreach (var s in Db.Set<BoardStar>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (s.BoardId.Value == boardId.Value && s.UserId == userId)
            {
                existing = s;
                break;
            }
        }
        if (existing is null)
        {
            return false;
        }

        Db.Set<BoardStar>().Remove(existing);
        await Db.SaveChangesAsync(ct);
        return true;
    }
}
