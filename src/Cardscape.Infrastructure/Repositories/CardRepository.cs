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
        var idValue = boardId.Value;
        var rows = new List<Card>();
        var lists = new Dictionary<Guid, BoardList>();
        await foreach (var l in Db.Set<BoardList>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.BoardId.Value == idValue)
            {
                lists[l.Id.Value] = l;
            }
        }

        await foreach (var c in Db.Set<Card>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!lists.ContainsKey(c.ListId.Value))
            {
                continue;
            }

            if (!includeArchived && c.IsArchived)
            {
                continue;
            }

            rows.Add(c);
        }

        rows.Sort((a, b) => a.Position.Value.CompareTo(b.Position.Value));
        return rows;
    }

    public async Task<IReadOnlyList<Card>> ListForListAsync(BoardListId listId, bool includeArchived, CancellationToken ct = default)
    {
        var idValue = listId.Value;
        var rows = new List<Card>();
        await foreach (var c in Db.Set<Card>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (c.ListId.Value != idValue)
            {
                continue;
            }

            if (!includeArchived && c.IsArchived)
            {
                continue;
            }

            rows.Add(c);
        }

        rows.Sort((a, b) => a.Position.Value.CompareTo(b.Position.Value));
        return rows;
    }

    public async Task<IReadOnlyList<Card>> ListDueInRangeForBoardAsync(
        BoardId boardId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var boardValue = boardId.Value;
        // Collect the lists under this board (streamed because the
        // list-id value-object can't be translated by EF).
        var listIds = new HashSet<Guid>();
        await foreach (var l in Db.Set<BoardList>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (l.BoardId.Value == boardValue)
            {
                listIds.Add(l.Id.Value);
            }
        }

        if (listIds.Count == 0)
        {
            return [];
        }

        // Cards with a due date in [from, to) on one of the board's
        // lists. We use a ListId Guid round-trip to keep the LINQ
        // expression translatable.
        var rows = new List<Card>();
        await foreach (var c in Db.Set<Card>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (!listIds.Contains(c.ListId.Value))
            {
                continue;
            }

            DateTimeOffset? due = c.DueDate;
            if (due is null)
            {
                continue;
            }

            if (due.Value < from || due.Value >= to)
            {
                continue;
            }

            rows.Add(c);
        }

        rows.Sort((a, b) => a.DueDate!.Value.CompareTo(b.DueDate!.Value));
        return rows;
    }

    public async Task<Card?> GetWithDetailsAsync(CardId id, CancellationToken ct = default)
    {
        var idValue = id.Value;
        return await Db.Set<Card>()
            .Include(c => c.Members)
            .Include(c => c.CardLabels)
            .FirstOrDefaultAsync(c => EF.Property<Guid>(c, "Id") == idValue, ct);
    }
}
