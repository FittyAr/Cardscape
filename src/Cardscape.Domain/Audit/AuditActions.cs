namespace Cardscape.Domain.Audit;

/// <summary>
/// Stable action codes of the administration audit log. They are stored
/// in the database and sent to clients, so never rename one; add new
/// codes instead. The prefix (<c>user.</c>, <c>workspace.</c>,
/// <c>board.</c>) is what the action filter matches.
/// </summary>
public static class AuditActions
{
    public const string UserRegistered = "user.registered";
    public const string UserCreatedByAdmin = "user.created_by_admin";
    public const string UserAdminGranted = "user.admin_granted";
    public const string UserAdminRevoked = "user.admin_revoked";
    public const string UserDeactivated = "user.deactivated";
    public const string UserReactivated = "user.reactivated";
    public const string UserDeleted = "user.deleted";
    public const string UserRestored = "user.restored";
    public const string UserAnonymised = "user.anonymised";
    public const string UserRestricted = "user.restricted";
    public const string UserUnrestricted = "user.unrestricted";
    public const string UserEmailVerified = "user.email_verified";
    public const string UserPasswordResetByAdmin = "user.password_reset_by_admin";

    public const string WorkspaceMemberAdded = "workspace.member_added";
    public const string WorkspaceMemberRoleChanged = "workspace.member_role_changed";
    public const string WorkspaceMemberRemoved = "workspace.member_removed";
    public const string WorkspaceInvitationIssued = "workspace.invitation_issued";
    public const string WorkspaceInvitationRevoked = "workspace.invitation_revoked";
    public const string WorkspaceInvitationAccepted = "workspace.invitation_accepted";
    public const string WorkspaceOwnershipTransferred = "workspace.ownership_transferred";

    public const string BoardMemberAdded = "board.member_added";
    public const string BoardMemberRoleChanged = "board.member_role_changed";
    public const string BoardMemberRemoved = "board.member_removed";

    /// <summary>Every code, in the order the UI lists them.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        UserRegistered, UserCreatedByAdmin, UserAdminGranted, UserAdminRevoked,
        UserDeactivated, UserReactivated, UserDeleted, UserRestored, UserAnonymised,
        UserRestricted, UserUnrestricted, UserEmailVerified, UserPasswordResetByAdmin,
        WorkspaceMemberAdded, WorkspaceMemberRoleChanged, WorkspaceMemberRemoved,
        WorkspaceInvitationIssued, WorkspaceInvitationRevoked, WorkspaceInvitationAccepted,
        WorkspaceOwnershipTransferred,
        BoardMemberAdded, BoardMemberRoleChanged, BoardMemberRemoved
    ];
}

/// <summary>What an audit entry's target id points at.</summary>
public static class AuditTargetTypes
{
    public const string User = "user";
    public const string Invitation = "invitation";
}
