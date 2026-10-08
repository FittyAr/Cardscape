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

public sealed record CreateCardRequestDto(Guid ListId, string Title, string? Description);


public sealed record SetCardDueDateRequestDto(DateTimeOffset DueDate);

public sealed record SnoozeCardRequestDto(DateTimeOffset Until);

public sealed record SetCardCoverRequestDto(string Color);

/// <summary>
/// P3.3 / G6c — result of <c>POST /api/cards/{id}/mirror</c>.
/// Mirrors the application-layer
/// <c>Cardscape.Application.Cards.CardscapeExtensions.MirrorCardResult</c>.
/// The new card id is the only thing the Web needs to show a
/// success notification; the full card detail is fetched on
/// demand when the user opens the mirrored card.
/// </summary>
public sealed record MirrorCardResultDto(Guid MirrorCardId);
