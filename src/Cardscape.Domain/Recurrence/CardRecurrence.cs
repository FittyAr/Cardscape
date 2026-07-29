using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Recurrence;

/// <summary>Recurrence rule attached to a card. When the dispatcher
/// ticks, it finds every active recurrence whose
/// <see cref="NextOccurrenceAt"/> has passed and schedules a
/// background job that clones the card onto the same list.</summary>
public sealed class CardRecurrence : Entity<CardRecurrenceId>
{
    public CardId CardId { get; private set; } = null!;
    public int IntervalDays { get; private set; }
    public DateTimeOffset NextOccurrenceAt { get; private set; }
    public bool IsActive { get; private set; }

    private CardRecurrence() { }

    private CardRecurrence(
        CardRecurrenceId id,
        CardId cardId,
        int intervalDays,
        DateTimeOffset nextOccurrenceAt,
        Guid createdBy,
        DateTimeOffset at)
    {
        Id = id;
        CardId = cardId;
        IntervalDays = intervalDays;
        NextOccurrenceAt = nextOccurrenceAt;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = at;
    }

    public static Result<CardRecurrence> Create(
        CardRecurrenceId id,
        CardId cardId,
        int intervalDays,
        DateTimeOffset nextOccurrenceAt,
        Guid createdBy,
        DateTimeOffset at)
    {
        if (intervalDays < 1)
        {
            return Result.Failure<CardRecurrence>(DomainError.Validation(
                "recurrence.interval_invalid",
                "Recurrence interval must be at least one day."));
        }

        if (intervalDays > 365)
        {
            return Result.Failure<CardRecurrence>(DomainError.Validation(
                "recurrence.interval_too_long",
                "Recurrence interval must be at most 365 days."));
        }

        if (createdBy == Guid.Empty)
        {
            return Result.Failure<CardRecurrence>(DomainError.Validation(
                "recurrence.creator_required", "Recurrence creator is required."));
        }

        return Result.Success(new CardRecurrence(
            id, cardId, intervalDays, nextOccurrenceAt, createdBy, at));
    }

    public Result Update(
        int newIntervalDays, DateTimeOffset newNextOccurrenceAt, DateTimeOffset at)
    {
        if (newIntervalDays < 1 || newIntervalDays > 365)
        {
            return Result.Failure(DomainError.Validation(
                "recurrence.interval_invalid",
                "Recurrence interval must be between 1 and 365 days."));
        }

        IntervalDays = newIntervalDays;
        NextOccurrenceAt = newNextOccurrenceAt;
        UpdatedAt = at;
        return Result.Success();
    }

    public void Reschedule(DateTimeOffset at)
    {
        NextOccurrenceAt = at;
    }

    public void Deactivate(DateTimeOffset at)
    {
        IsActive = false;
        UpdatedAt = at;
    }
}
