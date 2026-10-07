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
    BoardMemberRole Role,
    DateTimeOffset JoinedAt);
