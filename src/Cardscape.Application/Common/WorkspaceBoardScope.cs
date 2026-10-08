using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;

namespace Cardscape.Application.Common;

/// <summary>
/// Single place that answers "which of a workspace's boards does this
/// user see in workspace-wide views" (board grids, search, calendar,
/// starred boards). Full members keep the existing rule (every live
/// board of their workspaces); a <see cref="WorkspaceRole.Guest"/> only
/// sees the boards they were explicitly added to.
/// </summary>
public static class WorkspaceBoardScope
{
    /// <summary>True when <paramref name="board"/> belongs in a
    /// workspace-wide listing for a user with <paramref name="workspaceRole"/>.</summary>
    public static bool IsListed(Board board, Guid userId, WorkspaceRole? workspaceRole) =>
        workspaceRole != WorkspaceRole.Guest || board.IsMember(userId);

    /// <summary>
    /// Ids of every board <paramref name="userId"/> may see across the
    /// workspaces they belong to: all live boards where they are a full
    /// member, only their own boards where they are a guest.
    /// </summary>
    public static async Task<HashSet<Guid>> CollectVisibleBoardIdsAsync(
        IBoardRepository boards,
        IWorkspaceRepository workspaces,
        Guid userId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workspace> memberships = await workspaces.ListForUserAsync(userId, cancellationToken);
        List<WorkspaceId> full = memberships
            .Where(workspace => workspace.HasFullMembership(userId))
            .Select(workspace => workspace.Id)
            .ToList();
        List<WorkspaceId> asGuest = memberships
            .Where(workspace => workspace.IsGuest(userId))
            .Select(workspace => workspace.Id)
            .ToList();

        HashSet<Guid> ids = (await boards.ListIdsForWorkspacesAsync(full, cancellationToken))
            .Select(boardId => boardId.Value)
            .ToHashSet();
        if (asGuest.Count > 0)
        {
            ids.UnionWith((await boards.ListIdsForMemberAsync(asGuest, userId, cancellationToken))
                .Select(boardId => boardId.Value));
        }

        return ids;
    }

    /// <summary>Ids of the workspaces in which <paramref name="userId"/> is a guest.</summary>
    public static async Task<HashSet<WorkspaceId>> GuestWorkspaceIdsAsync(
        IWorkspaceRepository workspaces, Guid userId, CancellationToken cancellationToken) =>
        (await workspaces.ListForUserAsync(userId, cancellationToken))
            .Where(workspace => workspace.IsGuest(userId))
            .Select(workspace => workspace.Id)
            .ToHashSet();
}
