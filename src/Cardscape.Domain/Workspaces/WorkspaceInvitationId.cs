namespace Cardscape.Domain.Workspaces;

/// <summary>Identifier of a <see cref="WorkspaceInvitation"/>.</summary>
public sealed record WorkspaceInvitationId(Guid Value) : Common.GuidId<WorkspaceInvitationId>(Value);
