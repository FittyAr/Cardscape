namespace Cardscape.Web.Shared;

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
