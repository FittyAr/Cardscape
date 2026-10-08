using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Workspaces;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardRepository(CardscapeDbContext db) : RepositoryBase<Card, CardId>(db), ICardRepository
{
    public async Task<IReadOnlyList<Card>> ListForBoardAsync(BoardId boardId, bool includeArchived, CancellationToken ct = default)
    {
        IQueryable<Card> query =
            from card in Set.AsNoTracking()
            join list in Db.Set<BoardList>().AsNoTracking() on card.ListId equals list.Id
            where list.BoardId == boardId
            select card;
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.Position).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default)
    {
        IQueryable<Card> query = Set
            .AsNoTracking()
            .Where(c => c.ListId == listId);
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query.OrderBy(c => c.Position).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CalendarCardReadModel>> ListCalendarEntriesAsync(
        Guid userId,
        BoardId? boardId,
        DateTimeOffset from,
        DateTimeOffset rangeEnd,
        CancellationToken ct = default)
    {
        if (!Db.Database.IsSqlite())
        {
            return await QueryCalendarRange(Db, userId, boardId, from, rangeEnd).ToListAsync(ct);
        }

        // SQLite cannot translate ordering or range comparisons over
        // DateTimeOffset. Membership, tenant and relational filters
        // still execute in SQL; only the provider limitation stays local.
        List<CalendarCardReadModel> rows = await QueryCalendarCandidates(Db, userId, boardId)
            .Select(row => ToReadModel(row.Card, row.List, row.Board))
            .AsAsyncEnumerable()
            .Where(row => row.DueDate >= from && row.DueDate < rangeEnd)
            .ToListAsync(ct);
        rows.Sort(static (a, b) => a.DueDate.CompareTo(b.DueDate));
        return rows;
    }

    /// <summary>
    /// Fully server-translated calendar query for providers with native
    /// <see cref="DateTimeOffset"/> support. The range filter and ordering
    /// run against the mapped column BEFORE projecting: EF Core cannot
    /// translate member access on a constructor projection.
    /// </summary>
    internal static IQueryable<CalendarCardReadModel> QueryCalendarRange(
        DbContext db, Guid userId, BoardId? boardId, DateTimeOffset from, DateTimeOffset rangeEnd) =>
        QueryCalendarCandidates(db, userId, boardId)
            .Where(row => row.Card.DueDate >= from && row.Card.DueDate < rangeEnd)
            .OrderBy(row => row.Card.DueDate)
            .Select(row => ToReadModel(row.Card, row.List, row.Board));

    private static IQueryable<CalendarCandidate> QueryCalendarCandidates(
        DbContext db, Guid userId, BoardId? boardId) =>
        from card in db.Set<Card>().AsNoTracking()
        join list in db.Set<BoardList>().AsNoTracking() on card.ListId equals list.Id
        join board in db.Set<Board>().AsNoTracking() on list.BoardId equals board.Id
        join workspace in db.Set<Workspace>().AsNoTracking() on board.WorkspaceId equals workspace.Id
        where card.DueDate != null
            && !card.IsArchived
            && !board.IsDeleted
            && !workspace.IsDeleted
            && (boardId != null
                ? board.Id == boardId
                // Full members see every board of their workspaces;
                // guests only the boards they were explicitly added to.
                : workspace.Members.Any(member => member.UserId == userId
                    && (member.Role != WorkspaceRole.Guest
                        || board.Members.Any(boardMember => boardMember.UserId == userId))))
        select new CalendarCandidate { Card = card, List = list, Board = board };

    private static CalendarCardReadModel ToReadModel(Card card, BoardList list, Board board) => new(
        card.Id.Value,
        list.Id.Value,
        list.Name.Value,
        board.Id.Value,
        board.Name.Value,
        card.Title.Value,
        card.DueDate!.Value,
        card.IsCompleted);

    private sealed class CalendarCandidate
    {
        public required Card Card { get; init; }

        public required BoardList List { get; init; }

        public required Board Board { get; init; }
    }

    public async Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default)
    {
        return await Set
            .Include(c => c.Members)
            .Include(c => c.CardLabels)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }
}
