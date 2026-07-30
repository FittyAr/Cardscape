using Cardscape.Domain.Common;

namespace Cardscape.Domain.Workspaces.Errors;

/// <summary>Common errors raised by the <c>Workspaces</c> bounded context.</summary>
public static class WorkspaceErrors
{
    public static readonly DomainError NotFound =
        DomainError.NotFound("workspaces.not_found", "Workspace was not found.");

    public static readonly DomainError NotMember =
        DomainError.Forbidden("workspaces.not_member", "You are not a member of this workspace.");

    public static readonly DomainError InsufficientPermissions =
        DomainError.Forbidden("workspaces.forbidden", "You do not have permission to perform this action.");

    public static readonly DomainError AlreadyMember =
        DomainError.Conflict("workspaces.already_member", "User is already a member of this workspace.");

    public static readonly DomainError CannotRemoveOwner =
        DomainError.Conflict("workspaces.cannot_remove_owner", "The workspace owner cannot be removed.");

    public static readonly DomainError LastAdmin =
        DomainError.Conflict("workspaces.last_admin", "The workspace must have at least one admin.");

    /// <summary>Returned by <see cref="Cardscape.Domain.Workspaces.Workspace.GuardRegion"/>
    /// when the workspace's region does not match the deployment's
    /// configured region. Surfaced as a 422 by the API layer.</summary>
    public static readonly DomainError RegionMismatch =
        DomainError.Validation(
            "workspaces.region_mismatch",
            "This workspace is pinned to a different region than the deployment accepts.");

    /// <summary>Returned by <see cref="Cardscape.Domain.Workspaces.Workspace.SetRegion"/>
    /// when a non-owner tries to change the workspace's region.</summary>
    public static readonly DomainError CannotChangeRegion =
        DomainError.Forbidden(
            "workspaces.region_change_forbidden",
            "Only the workspace owner can change the workspace's region.");
}
