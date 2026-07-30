using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces.DTOs;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    Region Region,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    int MemberCount);

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
public sealed record SetWorkspaceRegionRequest(Region Region);
