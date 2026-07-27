using Cardscape.Domain.Common;

namespace Cardscape.Domain.Workspaces.Events;

public sealed record WorkspaceInvitationIssued(
    WorkspaceInvitationId InvitationId,
    WorkspaceId WorkspaceId,
    string Email,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

public sealed record WorkspaceInvitationAccepted(
    WorkspaceInvitationId InvitationId,
    WorkspaceId WorkspaceId,
    Guid AcceptedBy,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);

public sealed record WorkspaceInvitationRevoked(
    WorkspaceInvitationId InvitationId,
    WorkspaceId WorkspaceId,
    DateTimeOffset OccurredAt) : DomainEventBase(OccurredAt);
