using System.Text.Json.Serialization;

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
    bool IsCompleted)
{
    /// <summary>Local start of the due day; month views render one all-day slot per card.</summary>
    [JsonIgnore]
    public DateTime DayStart => DueDate.LocalDateTime.Date;

    [JsonIgnore]
    public DateTime DayEnd => DayStart.AddDays(1).AddTicks(-1);

    [JsonIgnore]
    public bool IsOverdue => !IsCompleted && DueDate < DateTimeOffset.Now;
}
