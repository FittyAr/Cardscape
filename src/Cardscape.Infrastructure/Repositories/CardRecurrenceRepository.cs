using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;
using Cardscape.Infrastructure.Persistence;



namespace Cardscape.Infrastructure.Repositories;

public sealed class CardRecurrenceRepository(CardscapeDbContext db)
    : RepositoryBase<CardRecurrence, CardRecurrenceId>(db), ICardRecurrenceRepository
{
    public async Task<bool> ExistsForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        await foreach (var r in Db.Set<CardRecurrence>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (r.CardId.Value == cardIdValue)
            {
                return true;
            }
        }
        return false;
    }

    public async Task<CardRecurrence?> GetForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        await foreach (var r in Db.Set<CardRecurrence>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (r.CardId.Value == cardIdValue)
            {
                return r;
            }
        }
        return null;
    }

    public async Task<IReadOnlyList<CardRecurrence>> ListDueAsync(
        DateTimeOffset now, int limit, CancellationToken ct = default)
    {
        var rows = new List<CardRecurrence>();
        await foreach (var r in Db.Set<CardRecurrence>().AsAsyncEnumerable().WithCancellation(ct))
        {
            if (r.IsActive && r.NextOccurrenceAt <= now)
            {
                rows.Add(r);
            }
        }
        rows.Sort((a, b) => a.NextOccurrenceAt.CompareTo(b.NextOccurrenceAt));
        if (rows.Count > limit)
        {
            rows.RemoveRange(limit, rows.Count - limit);
        }
        return rows;
    }
}
