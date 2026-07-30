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
    int Region,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    int Role,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequestDto(string Name, int? Region = null);
public sealed record SetWorkspaceRegionRequestDto(int Region);

// ── Google Calendar ──────────────────────────────────────
public sealed record GoogleCalendarConnectionDto(
    Guid Id,
    Guid UserId,
    Guid WorkspaceId,
    string GoogleEmail,
    string CalendarId,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? LastSyncErrorAt,
    string? LastSyncError,
    bool IsActive);

// ── SCIM ─────────────────────────────────────────────────
public sealed record ScimTokenDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string TokenPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked);

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

// ── API tokens (long-lived; for the MCP server) ───────────
public sealed record ApiTokenSummaryDto(
    Guid Id,
    string Name,
    string SecretPrefix,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    int RateLimitPerHour,
    int BurstSize);

public sealed record ApiTokenIssuanceDto(Guid Id, string CleartextSecret);

public sealed record IssueApiTokenRequestDto(
    string Name,
    IReadOnlyCollection<string> Scopes,
    DateTimeOffset? ExpiresAt,
    int? RateLimitPerHour = null,
    int? BurstSize = null);

public sealed record ApiTokenRateLimitStatusDto(
    Guid TokenId,
    int RateLimitPerHour,
    int BurstSize,
    double AvailableTokens,
    DateTimeOffset At);

public sealed record UpdateApiTokenRateLimitRequestDto(int RateLimitPerHour, int BurstSize);

// ── Workspace invitations (v0.5) ──────────────────────────
// Note: the API token issuance dropped the URL from the
// cleartext-only payload, but the invitation issuance still
// returns the bare cleartext (the email service has already been
// called server-side). We mirror the same shape here.
public sealed record WorkspaceInvitationDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    int Role,
    Guid InvitedBy,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    string TokenPrefix);

public sealed record WorkspaceInvitationIssuanceDto(
    Guid Id,
    Guid WorkspaceId,
    string CleartextToken);

public sealed record IssueWorkspaceInvitationRequestDto(
    string Email,
    int Role,
    TimeSpan? Lifetime = null);

public sealed record AcceptWorkspaceInvitationRequestDto(string Token);

// ── Automation (v0.6.3) ────────────────────────────────
// Trigger enum: 0 = CardMoved, 1 = CardCompleted, 2 = CardReopened, 3 = CardCreatedInList
// Action  enum: 0 = MoveCardToList, 1 = AssignUser, 2 = SetDueDate, 3 = MarkComplete
public sealed record BoardAutomationRuleDto(
    Guid Id,
    Guid BoardId,
    string Name,
    int Trigger,
    Guid? TriggerListId,
    int Action,
    string? ActionArgument,
    bool IsEnabled,
    int Position);

public sealed record CreateRuleRequestDto(
    string Name,
    int Trigger,
    Guid? TriggerListId,
    int Action,
    string? ActionArgument,
    int Position = 0);

// ── Calendar (v0.6.1) ───────────────────────────────────
public sealed record CalendarEntryDto(
    Guid CardId,
    Guid ListId,
    Guid BoardId,
    string BoardName,
    string Title,
    DateTimeOffset DueDate,
    bool IsCompleted);

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Kind,
    string PayloadJson,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record UnreadCountDto(int Count);

// ── Error envelope (matches Results.Problem shape) ──────
public sealed record ApiErrorDto(string? Title, string? Detail, int? Status);

// v0.6.4: Board extensions
// Kind enum: 0 = CustomFields, 1 = Voting, 2 = CardRepeater
public sealed record BoardExtensionDto(
    Guid Id,
    Guid BoardId,
    int Kind,
    string? ConfigJson,
    bool IsEnabled);

public sealed record EnableExtensionRequestDto(int Kind, string? ConfigJson);
public sealed record UpdateExtensionConfigRequestDto(string? ConfigJson);

// ── Custom fields (v0.7.1) ─────────────────────────────────
// Kind enum: 0 = Text, 1 = Number, 2 = Date, 3 = Dropdown, 4 = Checkbox
public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid BoardId,
    string Name,
    int Kind,
    string OptionsJson,
    int Position);

public sealed record CustomFieldValueDto(
    Guid FieldDefinitionId,
    Guid CardId,
    int Kind,
    string ValueJson);

public sealed record CreateCustomFieldRequestDto(
    string Name,
    int Kind,
    IReadOnlyList<string>? DropdownOptions,
    int Position = 0);

public sealed record RenameCustomFieldRequestDto(string NewName);

public sealed record SetCustomFieldValueRequestDto(string? ValueJson);

// ── Activity (v0.7.x) ────────────────────────────────────────
// Mirror of the application-layer ActivityDto. The timeline UI
// paged-loads via the `nextCursor` round-trip; see the
// `ActivityCursor` helper on the server.
public sealed record ActivityDto(
    Guid Id,
    Guid BoardId,
    Guid? CardId,
    Guid ActorId,
    int Kind,
    string KindName,
    string PayloadJson,
    DateTimeOffset OccurredAt);

public sealed record ActivityPageDto(
    IReadOnlyList<ActivityDto> Items,
    string? NextCursor);

// ── Voting (v0.7.x) ─────────────────────────────────────────
public sealed record CardVoteStateDto(
    Guid CardId,
    int VoteCount,
    bool CurrentUserHasVoted);

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

// ── Recurrence (v0.7.x) ────────────────────────────────────
public sealed record CardRecurrenceDto(
    Guid CardId,
    int IntervalDays,
    DateTimeOffset NextOccurrenceAt,
    bool IsActive);
