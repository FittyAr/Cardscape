using Cardscape.Domain.Common;

namespace Cardscape.Domain.Cards;

/// <summary>
/// Per-card snooze state. A snoozed card is hidden from the
/// default board view until the snooze expires. The user
/// can always see snoozed cards via the "show snoozed"
/// toggle.
///
/// Stored as a separate row keyed by <see cref="CardId"/>.
/// </summary>
public sealed class CardSnooze : Entity<CardId>
{
    public DateTimeOffset Until { get; private set; }
    public Guid SnoozedBy { get; private set; }
    public DateTimeOffset SnoozedAt { get; private set; }

    private CardSnooze() { }

    private CardSnooze(CardId cardId, DateTimeOffset until, Guid snoozedBy, DateTimeOffset at)
    {
        Id = cardId;
        Until = until;
        SnoozedBy = snoozedBy;
        SnoozedAt = at;
    }

    public static Result<CardSnooze> Create(
        CardId cardId, DateTimeOffset until, Guid snoozedBy, DateTimeOffset at)
    {
        if (until <= at)
        {
            return Result.Failure<CardSnooze>(DomainError.Validation(
                "card_snooze.until_in_past",
                "Snooze 'until' must be in the future."));
        }
        return Result.Success(new CardSnooze(cardId, until, snoozedBy, at));
    }

    public bool IsActive(DateTimeOffset now) => Until > now;
}
