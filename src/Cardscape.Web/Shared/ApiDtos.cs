namespace Cardscape.Web.Shared;

// ── Attachments (BUG-A5-002) ────────────────────────────
public sealed record AttachmentDto(
    Guid Id,
    Guid CardId,
    string FileName,
    string MimeType,
    long SizeBytes,
    Guid UploaderId,
    DateTimeOffset CreatedAt);

// ── Auth ────────────────────────────────────────────────
public sealed record RegisterRequestDto(string Email, string DisplayName, string Password);

public sealed record LoginRequestDto(string Email, string Password);

/// <summary>Second-step body for a 2FA-protected login.</summary>
public sealed record LoginWithTotpRequestDto(string PendingTotpToken, string Code);

public sealed record UserSummaryDto(Guid Id, string Email, string DisplayName);

/// <summary>
/// Mirrors the server-side <c>AuthResponse</c>. The access token is
/// <c>null</c> when <see cref="RequiresTotp"/> is <c>true</c>:
/// the caller must POST the <see cref="PendingTotpToken"/> + a
/// 6-digit code to <c>api/auth/login/totp</c>.
/// </summary>
public sealed record AuthResponseDto(
    string? AccessToken,
    UserSummaryDto User,
    bool RequiresTotp = false,
    string? PendingTotpToken = null);

// ── Workspaces ──────────────────────────────────────────
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    Region Region,
    bool IsArchived,
    bool RequireTwoFactor,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequestDto(string Name, Region? Region = null);
public sealed record SetWorkspaceRegionRequestDto(Region Region);
public sealed record SetWorkspaceRequireTwoFactorRequestDto(bool Require);
public sealed record AddWorkspaceMemberRequestDto(Guid UserId, WorkspaceRole Role);
public sealed record ChangeWorkspaceMemberRoleRequestDto(WorkspaceRole Role);

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

// ── Dashboards (P3.5) ───────────────────────────────────
public sealed record DashcardDto(
    Guid Id,
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string ConfigurationJson,
    int Position);

public sealed record CreateDashcardRequest(
    Guid BoardId,
    DashcardKind Kind,
    string Title,
    string ConfigurationJson,
    int Position);

// ── SAML ─────────────────────────────────────────────────
public sealed record SamlConnectionDto(
    Guid Id,
    Guid WorkspaceId,
    string Slug,
    string DisplayName,
    string IdpEntityId,
    string IdpMetadataUrl,
    string? IdpMetadataXml,
    string SpEntityId,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

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
    string? AuthorDisplayName,
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
    WorkspaceRole Role,
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
    WorkspaceRole Role,
    TimeSpan? Lifetime = null);

public sealed record AcceptWorkspaceInvitationRequestDto(string Token);

// ── Automation (v0.6.3) ────────────────────────────────
public sealed record BoardAutomationRuleDto(
    Guid Id,
    Guid BoardId,
    string Name,
    AutomationTrigger Trigger,
    Guid? TriggerListId,
    AutomationAction Action,
    string? ActionArgument,
    bool IsEnabled,
    int Position);

public sealed record CreateRuleRequestDto(
    string Name,
    AutomationTrigger Trigger,
    Guid? TriggerListId,
    AutomationAction Action,
    string? ActionArgument,
    int Position = 0);

// ── Calendar (v0.6.1) ───────────────────────────────────
public sealed record CalendarEntryDto(
    Guid CardId,
    Guid ListId,
    string ListName,
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

// ── Error envelopes ─────────────────────────────────────
// The API has three different error shapes in the wild
// (a real refactor would unify them; the Web's parser
// supports all three so every error renders as a single
// user-facing string):
//
//   1. RFC 7807 ProblemDetails (from Results.Problem(...))
//      ── { "title": "...", "detail": "...", "status": ... }
//      ── Auth, ExternalLogin, Totp, Integrations
//      ── read by ApiErrorDto
//
//   2. Flat projection (from `new { error.Code, error.Message }`)
//      ── { "code": "...", "message": "..." }
//      ── Activity, Ai, Slack, Automation, McpSubscriptions, UserDsr
//      ── read by ApiErrorBody
//
//   3. Wrapped envelope (from `new { error = new { code, message } }`)
//      ── { "error": { "code": "...", "message": "..." } }
//      ── Workspaces
//      ── read by ApiErrorEnvelope
//
// The comment in AuthService.ExtractErrorAsync originally said
// the API returns shape #1; that was wrong, and is the bug
// that surfaced as "raw JSON in the alert" in dev/prod.
public sealed record ApiErrorDto(string? Title, string? Detail, int? Status);
public sealed record ApiErrorBody(string? Code, string? Message);
public sealed record ApiErrorEnvelope(ApiErrorBody? Error);

// v0.6.4: Board extensions
public sealed record BoardExtensionDto(
    Guid Id,
    Guid BoardId,
    BoardExtensionKind Kind,
    string? ConfigJson,
    bool IsEnabled);

public sealed record EnableExtensionRequestDto(BoardExtensionKind Kind, string? ConfigJson);
public sealed record UpdateExtensionConfigRequestDto(string? ConfigJson);

// ── Custom fields (v0.7.1) ─────────────────────────────────
public sealed record CustomFieldDefinitionDto(
    Guid Id,
    Guid BoardId,
    string Name,
    CustomFieldKind Kind,
    string OptionsJson,
    int Position);

public sealed record CustomFieldValueDto(
    Guid FieldDefinitionId,
    Guid CardId,
    CustomFieldKind Kind,
    string ValueJson);



public sealed record WebhookEndpointDto(
    Guid Id,
    Guid BoardId,
    string Url,
    string SecretPrefix,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record WebhookEndpointIssuance(
    WebhookEndpointDto Endpoint,
    string CleartextSecret);

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid EndpointId,
    string EventType,
    int Status,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookRequestDto(
    string Url,
    string? Secret,
    IReadOnlyList<string> Events);
public sealed record CreateCustomFieldRequestDto(
    string Name,
    CustomFieldKind Kind,
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
    string? ActorDisplayName,
    ActivityKind Kind,
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

// ── Slack integration (v1.1.0 §3.7) ──────────────────────────
// Mirrors of the Application-layer DTOs. The bot token is never
// included in the projection — only its first 8 hex chars as a
// stable identifier the UI can display.
public sealed record SlackWorkspaceDto(
    Guid Id,
    Guid WorkspaceId,
    string TeamId,
    string TeamName,
    string BotTokenPrefix,
    DateTimeOffset? LastUsedAt,
    bool Active,
    DateTimeOffset CreatedAt);

public sealed record SlackChannelDto(
    Guid Id,
    Guid SlackWorkspaceId,
    Guid BoardId,
    string ChannelId,
    string ChannelName,
    IReadOnlyList<string> Events,
    bool Active,
    DateTimeOffset CreatedAt);

// ── Google Drive integration (v1.1.0 §3.8) ─────────────────
public sealed record GoogleDriveConnectionDto(
    Guid Id,
    Guid UserId,
    string GoogleEmail,
    DateTimeOffset? LastUsedAt,
    bool Active,
    DateTimeOffset CreatedAt);

// ── GitHub integration (v1.1.0 §3.9) ───────────────────────
public sealed record GitHubPullRequestDto(
    int Number,
    string Title,
    string State,
    string? Url,
    string? HeadRef,
    string? BaseRef,
    DateTimeOffset? CreatedAt);

public sealed record GitHubIssueDto(
    int Number,
    string Title,
    string State,
    string? Url,
    IReadOnlyList<string> Labels,
    DateTimeOffset? CreatedAt);

public sealed record GitHubPullRequestLinkDto(
    Guid Id,
    Guid CardId,
    string RepoFullName,
    int PullRequestNumber,
    string? PullRequestUrl,
    DateTimeOffset CreatedAt);

// ── Email-to-board (v1.1.0 §3.10) ──────────────────────────
public sealed record InboundEmailAddressDto(
    Guid Id,
    Guid WorkspaceId,
    string EmailAddress,
    Guid TargetListId,
    string Label,
    bool Active,
    DateTimeOffset CreatedAt);

// ── MCP subscriptions (v1.2.0 follow-up) ──────────────────
// Mirror of the API's McpSubscriptionsSnapshot. The MCP runs
// in a separate process; the API proxies the snapshot over
// the internal /api/admin/mcp-subscriptions endpoint. The
// Web UI renders the snapshot on the /admin/mcp-subscriptions
// page (read-only).
public sealed record McpSubscriptionsSnapshotDto(
    Dictionary<string, IReadOnlyList<string>> Subscribers,
    IReadOnlyList<McpSubscriptionEventDto> Events,
    DateTimeOffset CapturedAt);

public sealed record McpSubscriptionEventDto(
    string EventKind,
    string Uri,
    string? SessionId,
    DateTimeOffset Timestamp,
    string Detail);
