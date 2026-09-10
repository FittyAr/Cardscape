namespace Cardscape.Web.Shared;

// ── Calendar (v0.6.1) ───────────────────────────────────
public sealed record CalendarEntryDto(
    Guid CardId,
    Guid ListId,
    string ListName,
    Guid BoardId,
    string BoardName,
    string Title,
    DateTimeOffset DueDate,
    bool IsCompleted);
