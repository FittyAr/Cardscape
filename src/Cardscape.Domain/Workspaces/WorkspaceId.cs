namespace Cardscape.Domain.Workspaces;

/// <summary>Identifier of a workspace.</summary>
public sealed record WorkspaceId(Guid Value) : Common.GuidId<WorkspaceId>(Value);
