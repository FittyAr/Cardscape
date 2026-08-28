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
            from card in Db.Set<Card>().AsNoTracking()
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
        IQueryable<Card> query = Db.Set<Card>()
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
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        IQueryable<CalendarCardReadModel> candidates =
            from card in Db.Set<Card>().AsNoTracking()
            join list in Db.Set<BoardList>().AsNoTracking() on card.ListId equals list.Id
            join board in Db.Set<Board>().AsNoTracking() on list.BoardId equals board.Id
            join workspace in Db.Set<Workspace>().AsNoTracking() on board.WorkspaceId equals workspace.Id
            where card.DueDate != null
                && !board.IsDeleted
                && !workspace.IsDeleted
                && (boardId != null
                    ? board.Id == boardId
                    : workspace.Members.Any(member => member.UserId == userId))
            select new CalendarCardReadModel(
                card.Id.Value,
                list.Id.Value,
                list.Name.Value,
                board.Id.Value,
                board.Name.Value,
                card.Title.Value,
                card.DueDate!.Value,
                card.IsCompleted);

        List<CalendarCardReadModel> rows;
        if (!Db.Database.IsSqlite())
        {
            rows = await candidates
                .Where(row => row.DueDate >= from && row.DueDate < to)
                .OrderBy(row => row.DueDate)
                .ToListAsync(ct);
        }
        else
        {
            // SQLite cannot translate ordering or range comparisons over
            // DateTimeOffset. Membership, tenant and relational filters
            // still execute in SQL; only the provider limitation stays local.
            rows = [];
            await foreach (var row in candidates.AsAsyncEnumerable().WithCancellation(ct))
            {
                if (row.DueDate < from || row.DueDate >= to)
                {
                    continue;
                }

                rows.Add(row);
            }
        }

        rows.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));
        return rows;
    }

    public async Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default)
    {
        return await Db.Set<Card>()
            .Include(c => c.Members)
            .Include(c => c.CardLabels)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }
}
