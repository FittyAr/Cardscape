namespace Cardscape.Application.Cards.DTOs;

public sealed record CardDto(
    Guid Id,
    Guid ListId,
    string Title,
    string Description,
    double Position,
    DateTimeOffset? DueDate,
    bool IsArchived,
    bool IsCompleted,
    string? CoverColor,
    DateTimeOffset CreatedAt,
    int MemberCount,
    int LabelCount,
    bool IsSnoozed = false,
    DateTimeOffset? SnoozeUntil = null);

public sealed record CardSummaryDto(
    Guid Id,
    Guid ListId,
    string Title,
    double Position,
    DateTimeOffset? DueDate,
    bool IsCompleted,
    DateTimeOffset UpdatedAt,
    bool IsSnoozed = false,
    DateTimeOffset? SnoozeUntil = null);

/// <summary>
/// Per-card snooze projection. The <see cref="IsSnoozed"/> flag
/// is derived from <see cref="Until"/> vs. the snapshot's
/// <see cref="Now"/> so a stale row reads as not-snoozed
/// without the caller needing to do the math.
/// </summary>
public sealed record CardSnoozeDto(
    Guid CardId,
    DateTimeOffset Until,
    Guid SnoozedBy,
    DateTimeOffset SnoozedAt,
    DateTimeOffset Now)
{
    public bool IsSnoozed => Until > Now;
}
