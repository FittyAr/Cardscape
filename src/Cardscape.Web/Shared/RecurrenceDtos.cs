namespace Cardscape.Web.Shared;

// ── Recurrence (v0.7.x) ────────────────────────────────────
public sealed record CardRecurrenceDto(
    Guid CardId,
    int IntervalDays,
    DateTimeOffset NextOccurrenceAt,
    bool IsActive);
