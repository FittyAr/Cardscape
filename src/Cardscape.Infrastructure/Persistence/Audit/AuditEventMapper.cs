using Cardscape.Domain.Audit;
using Cardscape.Domain.Boards.Events;
using Cardscape.Domain.Common;
using Cardscape.Domain.Members.Events;
using Cardscape.Domain.Workspaces.Events;

namespace Cardscape.Infrastructure.Persistence.Audit;

/// <summary>
/// Facts the event payload does not carry but the change tracker knows:
/// the role a member had before the change, and the invitation an
/// invitation event came from.
/// </summary>
internal sealed record AuditEventFacts(
    string? PreviousRole = null,
    string? InvitationEmail = null,
    string? InvitationRole = null,
    string? WorkspaceName = null,
    string? PreviousName = null);

/// <summary>
/// An audit entry before names are resolved. <see cref="NamedUsers"/>
/// lists other people the entry mentions (e.g. the previous owner):
/// the writer stores each as an <c>&lt;key&gt;Id</c>/<c>&lt;key&gt;Name</c>
/// pair in the details.
/// </summary>
internal sealed record AuditDraft(
    DateTimeOffset OccurredAt,
    string Action,
    string TargetType,
    Guid TargetId,
    string? TargetName = null,
    Guid? WorkspaceId = null,
    Guid? BoardId = null,
    IReadOnlyDictionary<string, string>? Details = null,
    IReadOnlyDictionary<string, Guid>? NamedUsers = null,
    Guid? FallbackActorId = null);

/// <summary>
/// Pure mapping from the domain events the audit log cares about to
/// <see cref="AuditDraft"/>s. Events outside the catalogue map to null.
/// </summary>
internal static class AuditEventMapper
{
    public static AuditDraft? Map(IDomainEvent domainEvent, AuditEventFacts? facts = null)
    {
        facts ??= new AuditEventFacts();
        return domainEvent switch
        {
            // A self-service sign-up (or an IdP provisioning the account):
            // with nobody signed in, the new user is the actor.
            UserRegistered e => ForUser(e, e.UserId.Value, AuditActions.UserRegistered, fallbackActor: e.UserId.Value),
            UserCreatedByAdmin e => ForUser(e, e.UserId.Value, AuditActions.UserCreatedByAdmin),
            UserGrantedAdmin e => ForUser(e, e.UserId.Value, AuditActions.UserAdminGranted),
            UserRevokedAdmin e => ForUser(e, e.UserId.Value, AuditActions.UserAdminRevoked),
            UserDeactivated e => ForUser(e, e.UserId.Value, AuditActions.UserDeactivated),
            UserReactivated e => ForUser(e, e.UserId.Value, AuditActions.UserReactivated),
            UserSoftDeleted e => ForUser(e, e.UserId.Value, AuditActions.UserDeleted),
            UserRestored e => ForUser(e, e.UserId.Value, AuditActions.UserRestored),
            UserAnonymised e => ForUser(e, e.UserId.Value, AuditActions.UserAnonymised),
            UserRestricted e => ForUser(e, e.UserId.Value, AuditActions.UserRestricted),
            UserUnrestricted e => ForUser(e, e.UserId.Value, AuditActions.UserUnrestricted),
            // Clicking the verification link proves the user's own address.
            UserEmailVerified e => ForUser(e, e.UserId.Value, AuditActions.UserEmailVerified, fallbackActor: e.UserId.Value),
            UserPasswordResetByAdmin e => ForUser(e, e.UserId.Value, AuditActions.UserPasswordResetByAdmin),

            WorkspaceMemberAdded e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceMemberAdded, AuditTargetTypes.User, e.UserId,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("role", e.Role.ToString()))),
            WorkspaceMemberRoleChanged e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceMemberRoleChanged, AuditTargetTypes.User, e.UserId,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("from", facts.PreviousRole), ("to", e.NewRole.ToString()))),
            WorkspaceMemberRemoved e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceMemberRemoved, AuditTargetTypes.User, e.UserId,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("role", facts.PreviousRole))),
            WorkspaceInvitationIssued e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceInvitationIssued, AuditTargetTypes.Invitation, e.InvitationId.Value,
                TargetName: facts.InvitationEmail ?? e.Email,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("role", facts.InvitationRole))),
            WorkspaceInvitationRevoked e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceInvitationRevoked, AuditTargetTypes.Invitation, e.InvitationId.Value,
                TargetName: facts.InvitationEmail ?? string.Empty,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("role", facts.InvitationRole))),
            WorkspaceInvitationAccepted e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceInvitationAccepted, AuditTargetTypes.User, e.AcceptedBy,
                WorkspaceId: e.WorkspaceId.Value,
                Details: Facts(("role", facts.InvitationRole), ("invitationId", e.InvitationId.Value.ToString())),
                FallbackActorId: e.AcceptedBy),
            WorkspaceOwnershipTransferred e => new AuditDraft(
                e.OccurredAt, AuditActions.WorkspaceOwnershipTransferred, AuditTargetTypes.User, e.NewOwnerId,
                WorkspaceId: e.WorkspaceId.Value,
                NamedUsers: new Dictionary<string, Guid>(StringComparer.Ordinal) { ["previousOwner"] = e.PreviousOwnerId },
                FallbackActorId: e.ActorId),

            // The workspace itself. Its name is snapshotted from the entity
            // being saved: a new workspace is not in the database yet.
            WorkspaceCreated e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceCreated, e.Name.Value),
            WorkspaceRenamed e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceRenamed, e.NewName.Value,
                Facts(("previousName", facts.PreviousName))),
            WorkspaceArchived e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceArchived, facts.WorkspaceName),
            WorkspaceUnarchived e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceUnarchived, facts.WorkspaceName),
            WorkspaceDeleted e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceDeleted, facts.WorkspaceName),
            WorkspaceRegionChanged e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceRegionChanged, facts.WorkspaceName,
                Facts(("value", e.NewRegion.ToString()))),
            WorkspaceTwoFactorRequirementChanged e => ForWorkspace(e, e.WorkspaceId, AuditActions.WorkspaceTwoFactorChanged,
                facts.WorkspaceName, Facts(("value", e.Required ? "on" : "off")), fallbackActor: e.ActingUserId),

            BoardMemberAdded e => new AuditDraft(
                e.OccurredAt, AuditActions.BoardMemberAdded, AuditTargetTypes.User, e.UserId,
                BoardId: e.BoardId.Value,
                Details: Facts(("role", e.Role.ToString()))),
            BoardMemberRoleChanged e => new AuditDraft(
                e.OccurredAt, AuditActions.BoardMemberRoleChanged, AuditTargetTypes.User, e.UserId,
                BoardId: e.BoardId.Value,
                Details: Facts(("from", facts.PreviousRole), ("to", e.Role.ToString()))),
            BoardMemberRemoved e => new AuditDraft(
                e.OccurredAt, AuditActions.BoardMemberRemoved, AuditTargetTypes.User, e.UserId,
                BoardId: e.BoardId.Value,
                Details: Facts(("role", facts.PreviousRole))),

            _ => null
        };
    }

    private static AuditDraft ForWorkspace(
        IDomainEvent e, Domain.Workspaces.WorkspaceId workspaceId, string action, string? name,
        IReadOnlyDictionary<string, string>? details = null, Guid? fallbackActor = null) =>
        new(e.OccurredAt, action, AuditTargetTypes.Workspace, workspaceId.Value,
            TargetName: name, WorkspaceId: workspaceId.Value, Details: details, FallbackActorId: fallbackActor);

    /// <summary>
    /// Drops entries that only restate another entry of the same save, so
    /// one administrator action reads as one line: an account created by
    /// an administrator is verified and carries a temporary password by
    /// construction, a sign-up that proves the address in the same step
    /// needs no separate "verified" line (nor does the address an accepted
    /// invitation link proves), and joining through an
    /// invitation is the "accepted" line rather than a second "added".
    /// </summary>
    public static IReadOnlyList<AuditDraft> Collapse(IReadOnlyList<AuditDraft> drafts)
    {
        HashSet<Guid> createdByAdmin = Targets(drafts, AuditActions.UserCreatedByAdmin);
        HashSet<Guid> registered = Targets(drafts, AuditActions.UserRegistered);
        HashSet<(Guid, Guid?)> accepted = drafts
            .Where(d => d.Action == AuditActions.WorkspaceInvitationAccepted)
            .Select(d => (d.TargetId, d.WorkspaceId))
            .ToHashSet();
        // Creating a workspace adds its owner as the first member.
        HashSet<Guid?> createdWorkspaces = drafts
            .Where(d => d.Action == AuditActions.WorkspaceCreated)
            .Select(d => d.WorkspaceId)
            .ToHashSet();

        // An administrator-created account is not also a self-service
        // registration.
        return drafts
            .Where(d => !(d.Action == AuditActions.UserRegistered && createdByAdmin.Contains(d.TargetId)))
            .Where(d => !(d.Action is AuditActions.UserEmailVerified or AuditActions.UserPasswordResetByAdmin
                && createdByAdmin.Contains(d.TargetId)))
            .Where(d => !(d.Action == AuditActions.UserEmailVerified
                && (registered.Contains(d.TargetId) || accepted.Any(a => a.Item1 == d.TargetId))))
            .Where(d => !(d.Action == AuditActions.WorkspaceMemberAdded && accepted.Contains((d.TargetId, d.WorkspaceId))))
            .Where(d => !(d.Action == AuditActions.WorkspaceMemberAdded && createdWorkspaces.Contains(d.WorkspaceId)))
            .ToList();
    }

    private static HashSet<Guid> Targets(IEnumerable<AuditDraft> drafts, string action) =>
        drafts.Where(d => d.Action == action).Select(d => d.TargetId).ToHashSet();

    private static AuditDraft ForUser(IDomainEvent e, Guid userId, string action, Guid? fallbackActor = null) =>
        new(e.OccurredAt, action, AuditTargetTypes.User, userId, FallbackActorId: fallbackActor);

    private static Dictionary<string, string>? Facts(params (string Key, string? Value)[] pairs)
    {
        Dictionary<string, string> facts = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in pairs)
        {
            if (!string.IsNullOrEmpty(value))
            {
                facts[key] = value;
            }
        }

        return facts.Count == 0 ? null : facts;
    }
}
