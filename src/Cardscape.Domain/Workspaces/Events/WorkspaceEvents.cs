using Cardscape.Domain.Common;

namespace Cardscape.Domain.Workspaces.Events;

/// <summary>Raised when a workspace is created.</summary>
public sealed record WorkspaceCreated(
    WorkspaceId WorkspaceId,
    Guid OwnerId,
    WorkspaceName Name,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a workspace is renamed.</summary>
public sealed record WorkspaceRenamed(
    WorkspaceId WorkspaceId,
    WorkspaceName NewName,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a workspace is archived.</summary>
public sealed record WorkspaceArchived(
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a workspace is unarchived (BETA-A2-001).</summary>
public sealed record WorkspaceUnarchived(
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a workspace is soft-deleted (BETA-R2-A2-009). The
/// aggregate hides itself from default queries and any subsequent
/// read returns 404; members and boards are left in place for audit.</summary>
public sealed record WorkspaceDeleted(
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a member is added to a workspace.</summary>
public sealed record WorkspaceMemberAdded(
    WorkspaceId WorkspaceId,
    Guid UserId,
    WorkspaceRole Role,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a member is removed from a workspace.</summary>
public sealed record WorkspaceMemberRemoved(
    WorkspaceId WorkspaceId,
    Guid UserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a member's role changes.</summary>
public sealed record WorkspaceMemberRoleChanged(
    WorkspaceId WorkspaceId,
    Guid UserId,
    WorkspaceRole NewRole,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when a workspace's data-residency region is changed
/// by its owner. Useful for audit trails (compliance teams usually
/// track every region change).</summary>
public sealed record WorkspaceRegionChanged(
    WorkspaceId WorkspaceId,
    Region NewRegion,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

/// <summary>Raised when the workspace's two-factor authentication
/// requirement is toggled by its owner. Compliance teams track
/// these as "policy changes" — flipping this flag on forces every
/// member of the workspace to enroll in TOTP before they can log
/// in (see the enforcement path in <c>LoginUserQuery</c>). The
/// <c>ActingUserId</c> is the owner who issued the change.</summary>
public sealed record WorkspaceTwoFactorRequirementChanged(
    WorkspaceId WorkspaceId,
    bool Required,
    Guid ActingUserId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
