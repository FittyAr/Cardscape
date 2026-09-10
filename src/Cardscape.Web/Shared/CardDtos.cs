namespace Cardscape.Web.Shared;

// ── Cards ───────────────────────────────────────────────
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
    int CommentCount = 0,
    int AttachmentCount = 0,
    int ChecklistCount = 0,
    bool IsSnoozed = false,
    DateTimeOffset? SnoozeUntil = null,
    Guid? MirrorOfCardId = null);

public sealed record CardSummaryDto(
    Guid Id,
    Guid ListId,
    string Title,
    double Position,
    DateTimeOffset? DueDate,
    bool IsCompleted,
    DateTimeOffset UpdatedAt,
    bool IsSnoozed = false,
    DateTimeOffset? SnoozeUntil = null,
    Guid? MirrorOfCardId = null);

/// <summary>
/// Per-card snooze projection. Mirrors the Application-layer
/// <c>CardSnoozeDto</c>. <see cref="IsSnoozed"/> is derived from
/// <see cref="Until"/> vs. <see cref="Now"/> so a stale row
/// reads as not-snoozed without the caller doing the math.
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

public sealed record CreateCardRequestDto(Guid ListId, string Title, string? Description);


public sealed record SetCardDueDateRequestDto(DateTimeOffset DueDate);

public sealed record SnoozeCardRequestDto(DateTimeOffset Until);

/// <summary>
/// P3.3 / G6c — result of <c>POST /api/cards/{id}/mirror</c>.
/// Mirrors the application-layer
/// <c>Cardscape.Application.Cards.CardscapeExtensions.MirrorCardResult</c>.
/// The new card id is the only thing the Web needs to show a
/// success notification; the full card detail is fetched on
/// demand when the user opens the mirrored card.
/// </summary>
public sealed record MirrorCardResultDto(Guid MirrorCardId);
