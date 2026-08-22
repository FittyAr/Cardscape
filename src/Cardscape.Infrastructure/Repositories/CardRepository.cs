using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Lists;
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

    public async Task<IReadOnlyList<Card>> ListDueInRangeForBoardAsync(
        BoardId boardId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        IQueryable<Card> candidates =
            from card in Db.Set<Card>().AsNoTracking()
            join list in Db.Set<BoardList>().AsNoTracking() on card.ListId equals list.Id
            where list.BoardId == boardId && card.DueDate != null
            select card;

        if (!Db.Database.IsSqlite())
        {
            return await candidates
                .Where(card => card.DueDate >= from && card.DueDate < to)
                .OrderBy(card => card.DueDate)
                .ToListAsync(ct);
        }

        // SQLite cannot translate ordering or range comparisons over
        // DateTimeOffset. Keep only that provider limitation on the
        // client; board/list filtering and null elimination remain in SQL.
        var rows = new List<Card>();
        await foreach (Card card in candidates.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (card.DueDate < from || card.DueDate >= to)
            {
                continue;
            }

            rows.Add(card);
        }

        rows.Sort((a, b) => a.DueDate!.Value.CompareTo(b.DueDate!.Value));
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
