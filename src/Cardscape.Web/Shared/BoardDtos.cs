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
    int MemberCount,
    string? Color = null);

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

// ── Board members ───────────────────────────────────────
public sealed record BoardMemberDto(
    Guid UserId,
    string? DisplayName,
    string? Email,
    BoardMemberRole Role,
    DateTimeOffset JoinedAt);

public sealed record BoardMemberAccessDto(bool CanManageMembers, BoardMemberRole? Role);

public sealed record AddBoardMemberRequestDto(Guid UserId, BoardMemberRole Role);

public sealed record ChangeBoardMemberRoleRequestDto(BoardMemberRole Role);
