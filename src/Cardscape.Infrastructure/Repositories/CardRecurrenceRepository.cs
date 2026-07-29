using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;
using Cardscape.Infrastructure.Persistence;

namespace Cardscape.Infrastructure.Repositories;

public sealed class CardRecurrenceRepository(CardscapeDbContext db)
    : RepositoryBase<CardRecurrence, CardRecurrenceId>(db), ICardRecurrenceRepository
{
    public Task<bool> ExistsForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        return Task.Run(() =>
        {
            return Db.Set<CardRecurrence>().AsEnumerable()
                .Any(r => r.CardId.Value == cardIdValue);
        }, ct);
    }

    public Task<CardRecurrence?> GetForCardAsync(CardId cardId, CancellationToken ct = default)
    {
        var cardIdValue = cardId.Value;
        return Task.Run<CardRecurrence?>(() =>
        {
            return Db.Set<CardRecurrence>().AsEnumerable()
                .FirstOrDefault(r => r.CardId.Value == cardIdValue);
        }, ct);
    }

    public Task<IReadOnlyList<CardRecurrence>> ListDueAsync(
        DateTimeOffset now, int limit, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<CardRecurrence>>(() =>
        {
            return Db.Set<CardRecurrence>().AsEnumerable()
                .Where(r => r.IsActive && r.NextOccurrenceAt <= now)
                .OrderBy(r => r.NextOccurrenceAt)
                .Take(limit)
                .ToList();
        }, ct);
    }
}
