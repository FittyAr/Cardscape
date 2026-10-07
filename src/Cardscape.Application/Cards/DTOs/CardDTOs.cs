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
    int CommentCount = 0,
    int AttachmentCount = 0,
    int ChecklistCount = 0,
    bool IsSnoozed = false,
    DateTimeOffset? SnoozeUntil = null,
    Guid? MirrorOfCardId = null,
    IReadOnlyList<Guid>? LabelIds = null,
    IReadOnlyList<Guid>? MemberIds = null);

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
    Guid? MirrorOfCardId = null,
    IReadOnlyList<CardSummaryLabelDto>? Labels = null,
    IReadOnlyList<CardSummaryMemberDto>? Members = null,
    int ChecklistCompleted = 0,
    int ChecklistTotal = 0,
    int CommentCount = 0,
    string? CoverColor = null,
    bool HasDescription = false,
    int AttachmentCount = 0,
    bool IsArchived = false);

/// <summary>Label attached to a card, as shown on the kanban card front.</summary>
public sealed record CardSummaryLabelDto(Guid Id, string Name, string Color);

/// <summary>Card assignee with the display name used for avatar initials.</summary>
public sealed record CardSummaryMemberDto(Guid UserId, string DisplayName);

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
