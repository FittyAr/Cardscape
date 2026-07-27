namespace Cardscape.Web.Shared;

// ── Auth ────────────────────────────────────────────────
public sealed record RegisterRequestDto(string Email, string DisplayName, string Password);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record UserSummaryDto(Guid Id, string Email, string DisplayName);

public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    UserSummaryDto User);

// ── Workspaces ──────────────────────────────────────────
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    int Role,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequestDto(string Name);

// ── Boards ──────────────────────────────────────────────
public sealed record BoardDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Description,
    int Visibility,
    bool IsArchived,
    bool IsStarred,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record BoardSummaryDto(
    Guid Id,
    string Name,
    int Visibility,
    bool IsArchived,
    bool IsStarred,
    DateTimeOffset CreatedAt);

public sealed record CreateBoardRequestDto(
    Guid WorkspaceId,
    string Name,
    string? Description,
    int Visibility);

// ── Lists ───────────────────────────────────────────────
public sealed record BoardListDto(
    Guid Id,
    Guid BoardId,
    string Name,
    double Position,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int CardCount);

public sealed record CreateListRequestDto(Guid BoardId, string Name);

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
    int LabelCount);

public sealed record CardSummaryDto(
    Guid Id,
    Guid ListId,
    string Title,
    double Position,
    DateTimeOffset? DueDate,
    bool IsCompleted);

public sealed record CreateCardRequestDto(Guid ListId, string Title, string? Description);

public sealed record MoveCardRequestDto(Guid NewListId, double NewPosition);

public sealed record SetCardDueDateRequestDto(DateTimeOffset DueDate);

// ── Labels ──────────────────────────────────────────────
public sealed record LabelDto(
    Guid Id,
    Guid BoardId,
    string Name,
    string Color);

public sealed record CreateLabelRequestDto(string Name, string Color);

// ── Comments ────────────────────────────────────────────
public sealed record CommentDto(
    Guid Id,
    Guid CardId,
    Guid AuthorId,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record AddCommentRequestDto(string Body);

// ── Error envelope (matches Results.Problem shape) ──────
public sealed record ApiErrorDto(string? Title, string? Detail, int? Status);
