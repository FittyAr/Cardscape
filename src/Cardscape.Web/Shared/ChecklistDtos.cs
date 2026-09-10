namespace Cardscape.Web.Shared;

// ── Checklists (v0.7.x) ────────────────────────────────────
public sealed record ChecklistItemDto(
    Guid Id,
    Guid ChecklistId,
    string Text,
    bool IsCompleted,
    int Position,
    Guid? AssignedTo);

public sealed record ChecklistDto(
    Guid Id,
    Guid CardId,
    string Title,
    IReadOnlyList<ChecklistItemDto> Items,
    int CompletedCount,
    int TotalCount);
