namespace Cardscape.Domain.Workspaces;

/// <summary>Identifier of a workspace member join row.</summary>
public sealed record WorkspaceMemberId(Guid Value) : Common.GuidId<WorkspaceMemberId>(Value);
