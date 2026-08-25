using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Recurrence;
using Wolverine;

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


