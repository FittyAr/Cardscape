using Cardscape.Domain.Cards;
using Cardscape.Domain.Recurrence;

namespace Cardscape.Application.Abstractions.Persistence;

public interface ICardRecurrenceRepository : IRepository<CardRecurrence, CardRecurrenceId>
{
    /// <summary>True if the card already has a recurrence rule.</summary>
    Task<bool> ExistsForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>Returns the active recurrence on the card, if any.</summary>
    Task<CardRecurrence?> GetForCardAsync(CardId cardId, CancellationToken ct = default);

    /// <summary>Active recurrences whose <c>NextOccurrenceAt</c> is
    /// at or before <paramref name="now"/>, used by the dispatcher
    /// to claim due work in a single batch.</summary>
    Task<IReadOnlyList<CardRecurrence>> ListDueAsync(
        DateTimeOffset now, int limit, CancellationToken ct = default);
}
