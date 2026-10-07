using Cardscape.Domain.Recurrence;

namespace Cardscape.Application.Recurrence;

public sealed record CardRecurrenceDto(
    Guid CardId,
    int IntervalDays,
    DateTimeOffset NextOccurrenceAt,
    bool IsActive)
{
    public static CardRecurrenceDto FromEntity(CardRecurrence r) => new(
        r.CardId.Value,
        r.IntervalDays,
        r.NextOccurrenceAt,
        r.IsActive);
}

