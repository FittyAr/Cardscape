using Cardscape.Domain.Boards;

namespace Cardscape.Application.Boards.DTOs;

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

public sealed record BoardMemberDto(
    Guid UserId,
    string? DisplayName,
    string? Email,
    BoardMemberRole Role,
    DateTimeOffset JoinedAt,
    bool IsWorkspaceGuest = false);

/// <summary>What the caller may do with a board's roster.
/// <paramref name="CanInviteGuests"/> is true for the people who may
/// issue workspace invitations (workspace owner / Admins, instance
/// admins), who can invite someone as a guest from the board.</summary>
public sealed record BoardMemberAccessDto(
    bool CanManageMembers,
    BoardMemberRole? Role,
    bool CanInviteGuests = false);
