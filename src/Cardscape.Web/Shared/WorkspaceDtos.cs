namespace Cardscape.Web.Shared;

// ── Workspaces ──────────────────────────────────────────
public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    Region Region,
    bool IsArchived,
    bool RequireTwoFactor,
    DateTimeOffset CreatedAt,
    int MemberCount);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt);

public sealed record CreateWorkspaceRequestDto(string Name, Region? Region = null);
public sealed record SetWorkspaceRegionRequestDto(Region Region);
public sealed record SetWorkspaceRequireTwoFactorRequestDto(bool Require);
public sealed record AddWorkspaceMemberRequestDto(Guid UserId, WorkspaceRole Role);
public sealed record ChangeWorkspaceMemberRoleRequestDto(WorkspaceRole Role);
