using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Workspaces;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Members;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Boards;

/// <summary>
/// Board-level membership authorization shared by the board member
/// handlers. A board's roster is managed by its board Admins, by the
/// owner and Admins of the board's workspace, and by any active
/// instance administrator (same override as <see cref="WorkspaceAccess"/>).
/// </summary>
internal static class BoardMemberAccess
{
    public static async Task<bool> CanManageMembersAsync(
        Board board,
        Workspace? workspace,
        UserId caller,
        IUserRepository users,
        CancellationToken cancellation) =>
        board.IsAdmin(caller.Value)
        || (workspace is { IsDeleted: false } && workspace.CanManageMembers(caller.Value))
        || await WorkspaceAccess.IsInstanceAdminAsync(caller, users, cancellation);

    public static Task<Workspace?> LoadWorkspaceAsync(
        Board board, IWorkspaceRepository workspaces, CancellationToken cancellation) =>
        workspaces.GetWithMembersAsync(board.WorkspaceId, cancellation);
}
