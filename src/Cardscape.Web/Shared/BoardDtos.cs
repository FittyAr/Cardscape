namespace Cardscape.Web.Shared;

// ── Boards ──────────────────────────────────────────────
public sealed record BoardDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Description,
    BoardVisibility Visibility,
    bool IsArchived,
    bool IsStarred,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record BoardSummaryDto(
    Guid Id,
    string Name,
    BoardVisibility Visibility,
    bool IsArchived,
    bool IsStarred,
    DateTimeOffset CreatedAt);

public sealed record CreateBoardRequestDto(
    Guid WorkspaceId,
    string Name,
    string? Description,
    BoardVisibility Visibility);
