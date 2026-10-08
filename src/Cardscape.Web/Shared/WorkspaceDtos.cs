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
    int MemberCount,
    WorkspaceRole? CallerRole = null);

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceRole Role,
    DateTimeOffset JoinedAt);

/// <summary>Guest helpers kept off the DTOs so the records still mirror
/// the wire contract property for property.</summary>
public static class WorkspaceGuestExtensions
{
    extension(WorkspaceDto workspace)
    {
        /// <summary>True when the caller is a workspace guest: they only
        /// see their own boards and cannot create boards or manage people.</summary>
        public bool CallerIsGuest => workspace.CallerRole == WorkspaceRole.Guest;
    }

    extension(WorkspaceMemberDto member)
    {
        public bool IsGuest => member.Role == WorkspaceRole.Guest;
    }
}

public sealed record CreateWorkspaceRequestDto(string Name, Region? Region = null);
public sealed record SetWorkspaceRegionRequestDto(Region Region);
public sealed record SetWorkspaceRequireTwoFactorRequestDto(bool Require);
public sealed record AddWorkspaceMemberRequestDto(Guid UserId, WorkspaceRole Role);
public sealed record ChangeWorkspaceMemberRoleRequestDto(WorkspaceRole Role);
public sealed record TransferWorkspaceOwnershipRequestDto(Guid UserId);
