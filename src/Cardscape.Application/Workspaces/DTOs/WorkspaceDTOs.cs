using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces.DTOs;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    Region Region,
    bool IsArchived,
    bool RequireTwoFactor,
    DateTimeOffset CreatedAt,
    int MemberCount,
    WorkspaceRole? CallerRole = null)
{
    public static WorkspaceDto FromEntity(Workspace workspace) => new(
        workspace.Id.Value,
        workspace.Name.Value,
        workspace.OwnerId,
        workspace.Region,
        workspace.IsArchived,
        workspace.RequireTwoFactor,
        workspace.CreatedAt,
        workspace.Members.Count);

    /// <summary>Same as <see cref="FromEntity(Workspace)"/> plus the
    /// caller's own role, so clients can hide what a guest cannot use.</summary>
    public static WorkspaceDto FromEntity(Workspace workspace, Guid callerId) =>
        FromEntity(workspace) with { CallerRole = workspace.RoleOf(callerId) };
}

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequest(string Name, Region? Region = null);
public sealed record RenameWorkspaceRequest(string Name);
public sealed record AddWorkspaceMemberRequest(Guid UserId, WorkspaceRole Role);
public sealed record ChangeWorkspaceMemberRoleRequest(WorkspaceRole Role);
public sealed record TransferWorkspaceOwnershipRequest(Guid UserId);
public sealed record SetWorkspaceRegionRequest(Region Region);
public sealed record SetWorkspaceRequireTwoFactorRequest(bool Require);
