using Cardscape.Domain.Boards;
using Cardscape.Domain.Boards.Errors;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces.Events;

namespace Cardscape.Domain.Workspaces;

/// <summary>
/// A pending invitation to join a workspace. The cleartext
/// token (32 random bytes, base64url) is delivered to the
/// invitee out-of-band; the server only persists the SHA-256
/// hash. The prefix (first 10 chars of the cleartext) is kept
/// in the cleartext for the invite UI so the user can tell
/// tokens apart after the fact.
///
/// Invitations have a 14-day expiry. <c>AcceptedAt</c> and
/// <c>RevokedAt</c> are mutually exclusive; setting either
/// marks the invitation as terminal.
/// </summary>
public sealed class WorkspaceInvitation : AggregateRoot<WorkspaceInvitationId>
{
    public const int DefaultExpiryDays = 14;
    public const int MaxExpiryDays = 60;

    public WorkspaceId WorkspaceId { get; private set; } = null!;
    public string Email { get; private set; } = string.Empty;
    public WorkspaceRole Role { get; private set; }
    public Guid InvitedBy { get; private set; }
    public DateTimeOffset InvitedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;
    public string TokenPrefix { get; private set; } = string.Empty;

    public DateTimeOffset? AcceptedAt { get; private set; }
    public Guid? AcceptedBy { get; private set; }

    /// <summary>Optional board the invitee joins together with the
    /// workspace (Trello's "invite to this board"), with <see cref="BoardRole"/>.</summary>
    public Guid? BoardId { get; private set; }

    public BoardMemberRole? BoardRole { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }

    // EF Core.
    private WorkspaceInvitation() { }

    private WorkspaceInvitation(
        WorkspaceInvitationId id,
        WorkspaceId workspaceId,
        string email,
        WorkspaceRole role,
        Guid invitedBy,
        string tokenHash,
        string tokenPrefix,
        DateTimeOffset invitedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Email = email;
        Role = role;
        InvitedBy = invitedBy;
        InvitedAt = invitedAt;
        ExpiresAt = expiresAt;
        TokenHash = tokenHash;
        TokenPrefix = tokenPrefix;
        CreatedAt = invitedAt;
    }

    /// <summary>
    /// Factory: mint a new pending invitation. The caller has
    /// already generated and hashed the token; the aggregate
    /// just stores it.
    /// </summary>
    public static Result<WorkspaceInvitation> Issue(
        WorkspaceId workspaceId,
        string email,
        WorkspaceRole role,
        Guid invitedBy,
        string tokenHash,
        string tokenPrefix,
        DateTimeOffset at,
        TimeSpan? lifetime = null,
        Guid? boardId = null,
        BoardMemberRole? boardRole = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<WorkspaceInvitation>(DomainError.Validation(
                "workspaces.invitation.email_required",
                "Invite email is required."));
        }

        if (tokenHash is null || tokenHash.Length != 64)
        {
            return Result.Failure<WorkspaceInvitation>(DomainError.Validation(
                "workspaces.invitation.token_hash_invalid",
                "Token hash must be a 64-character SHA-256 hex string."));
        }

        if (string.IsNullOrEmpty(tokenPrefix) || tokenPrefix.Length > InvitationToken.PrefixLength)
        {
            return Result.Failure<WorkspaceInvitation>(DomainError.Validation(
                "workspaces.invitation.token_prefix_invalid",
                $"Token prefix must be 1..{InvitationToken.PrefixLength} chars."));
        }

        if (boardId is null && boardRole is not null)
        {
            return Result.Failure<WorkspaceInvitation>(DomainError.Validation(
                "workspaces.invitation.board_role_without_board",
                "A board role needs a board."));
        }

        BoardMemberRole? effectiveBoardRole = boardId is null ? null : boardRole ?? BoardMemberRole.Member;
        if (effectiveBoardRole is { } requested && !Enum.IsDefined(requested))
        {
            return Result.Failure<WorkspaceInvitation>(BoardErrors.InvalidMemberRole);
        }

        // A guest can be a board Member at most, as when added directly.
        if (role == WorkspaceRole.Guest && effectiveBoardRole == BoardMemberRole.Admin)
        {
            return Result.Failure<WorkspaceInvitation>(BoardErrors.GuestCannotBeAdmin);
        }

        TimeSpan ttl = lifetime ?? TimeSpan.FromDays(DefaultExpiryDays);
        if (ttl <= TimeSpan.Zero || ttl.TotalDays > MaxExpiryDays)
        {
            return Result.Failure<WorkspaceInvitation>(DomainError.Validation(
                "workspaces.invitation.lifetime_invalid",
                $"Invitation lifetime must be between 1 hour and {MaxExpiryDays} days."));
        }

        var invitation = new WorkspaceInvitation(
            id: WorkspaceInvitationId.New(),
            workspaceId: workspaceId,
            email: EmailAddress.Normalize(email),
            role: role,
            invitedBy: invitedBy,
            tokenHash: tokenHash,
            tokenPrefix: tokenPrefix,
            invitedAt: at,
            expiresAt: at.Add(ttl))
        {
            BoardId = boardId,
            BoardRole = effectiveBoardRole,
        };

        invitation.AddDomainEvent(new WorkspaceInvitationIssued(
            invitation.Id, workspaceId, email, at));
        return Result.Success(invitation);
    }

    /// <summary>True if the invitation can still be redeemed at the given moment.</summary>
    public bool IsActive(DateTimeOffset now) =>
        AcceptedAt is null && RevokedAt is null && ExpiresAt > now;

    /// <summary>
    /// Succeeds when the invitation can still be redeemed at
    /// <paramref name="at"/>; otherwise explains why not (already
    /// accepted, revoked, or expired — checked in that order).
    /// </summary>
    public Result EnsureRedeemable(DateTimeOffset at)
    {
        if (AcceptedAt is not null)
        {
            return Result.Failure(DomainError.Conflict(
                "workspaces.invitation.already_accepted",
                "Invitation has already been accepted."));
        }

        if (RevokedAt is not null)
        {
            return Result.Failure(DomainError.Conflict(
                "workspaces.invitation.revoked",
                "Invitation has been revoked."));
        }

        return ExpiresAt <= at
            ? Result.Failure(DomainError.Forbidden("workspaces.invitation.expired", "Invitation has expired."))
            : Result.Success();
    }

    /// <summary>
    /// Redeem the invitation. Idempotent: redeeming a
    /// non-active invitation returns a <c>NotActive</c> error.
    /// </summary>
    public Result Accept(Guid userId, DateTimeOffset at)
    {
        Result redeemable = EnsureRedeemable(at);
        if (redeemable.IsFailure)
        {
            return redeemable;
        }

        AcceptedAt = at;
        AcceptedBy = userId;
        AddDomainEvent(new WorkspaceInvitationAccepted(Id, WorkspaceId, userId, at));
        return Result.Success();
    }

    /// <summary>Revoke the invitation. Idempotent: revoking a non-active invitation returns a failure.</summary>
    public Result Revoke(Guid by, DateTimeOffset at)
    {
        if (AcceptedAt is not null)
        {
            return Result.Failure(DomainError.Conflict(
                "workspaces.invitation.already_accepted",
                "Invitation has already been accepted; revoke it from the workspace members view instead."));
        }

        if (RevokedAt is not null)
        {
            return Result.Failure(DomainError.Conflict(
                "workspaces.invitation.already_revoked",
                "Invitation has already been revoked."));
        }

        RevokedAt = at;
        RevokedBy = by;
        AddDomainEvent(new WorkspaceInvitationRevoked(Id, WorkspaceId, at));
        return Result.Success();
    }
}
