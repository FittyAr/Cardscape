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
