using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;
using Cardscape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardRecurrenceRepository(CardscapeDbContext db)
    : RepositoryBase<CardRecurrence, CardRecurrenceId>(db), ICardRecurrenceRepository
{
    public async Task<bool> ExistsForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        return await Db.Set<CardRecurrence>().AnyAsync(recurrence => recurrence.CardId == cardId, ct);
    }

    public async Task<CardRecurrence?> GetForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        return await Db.Set<CardRecurrence>()
            .FirstOrDefaultAsync(recurrence => recurrence.CardId == cardId, ct);
    }

    public async Task<IReadOnlyList<CardRecurrence>> ListDueAsync(
        DateTimeOffset now, int limit, CancellationToken ct = default)
    {
        IQueryable<CardRecurrence> active = Db.Set<CardRecurrence>().Where(recurrence => recurrence.IsActive);
        if (!Db.Database.IsSqlite())
        {
            return await active
                .Where(recurrence => recurrence.NextOccurrenceAt <= now)
                .OrderBy(recurrence => recurrence.NextOccurrenceAt)
                .Take(limit)
                .ToListAsync(ct);
        }

        // SQLite cannot translate DateTimeOffset comparison/order.
        var rows = new List<CardRecurrence>();
        await foreach (CardRecurrence recurrence in active.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (recurrence.NextOccurrenceAt <= now) rows.Add(recurrence);
        }
        rows.Sort((a, b) => a.NextOccurrenceAt.CompareTo(b.NextOccurrenceAt));
        if (rows.Count > limit)
        {
            rows.RemoveRange(limit, rows.Count - limit);
        }
        return rows;
    }
}
