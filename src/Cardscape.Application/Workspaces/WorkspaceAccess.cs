using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Workspaces;

/// <summary>
/// Workspace-level authorization shared by the member and invitation
/// handlers. Workspace owners and workspace Admins manage members;
/// an active instance administrator can act on any workspace
/// (read live from the users table, like the AdminOnly policy).
/// </summary>
internal static class WorkspaceAccess
{
    public static async Task<bool> CanManageMembersAsync(
        Workspace workspace, UserId caller, IUserRepository users, CancellationToken cancellation) =>
        workspace.CanManageMembers(caller.Value) || await IsInstanceAdminAsync(caller, users, cancellation);

    public static async Task<bool> CanViewAsync(
        Workspace workspace, UserId caller, IUserRepository users, CancellationToken cancellation) =>
        workspace.HasMember(caller.Value) || await IsInstanceAdminAsync(caller, users, cancellation);

    private static async Task<bool> IsInstanceAdminAsync(
        UserId caller, IUserRepository users, CancellationToken cancellation)
    {
        User? user = await users.GetByIdAsync(caller, cancellation);
        return user is { IsAdmin: true, IsActive: true, IsDeleted: false, IsAnonymised: false };
    }
}
