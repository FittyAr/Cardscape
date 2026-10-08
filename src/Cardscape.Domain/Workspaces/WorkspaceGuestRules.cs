using Cardscape.Domain.Boards;

namespace Cardscape.Domain.Workspaces;

/// <summary>
/// Rules that keep a <see cref="WorkspaceRole.Guest"/> confined to the
/// boards they were explicitly added to. Shared by the board membership
/// handlers and by every "which boards can this user read" query.
/// </summary>
public static class WorkspaceGuestRules
{
    /// <summary>The highest board role a guest may hold.</summary>
    public const BoardMemberRole MaxBoardRole = BoardMemberRole.Member;

    /// <summary>True when a member with <paramref name="workspaceRole"/>
    /// may hold <paramref name="boardRole"/> on a board of that workspace.</summary>
    public static bool AllowsBoardRole(WorkspaceRole? workspaceRole, BoardMemberRole boardRole) =>
        workspaceRole != WorkspaceRole.Guest || boardRole != BoardMemberRole.Admin;

    /// <summary>
    /// Whether <paramref name="userId"/> can open <paramref name="board"/>
    /// given their role in its workspace (<c>null</c> when they are not a
    /// member): board members always can, anyone can open a public
    /// board, and a workspace-visible board is open to the workspace's
    /// full members but not to its guests. Private boards need board
    /// membership.
    /// </summary>
    public static bool CanOpenBoard(Board board, Guid userId, WorkspaceRole? workspaceRole) =>
        board.IsMember(userId)
        || board.Visibility == BoardVisibility.Public
        || (board.Visibility == BoardVisibility.Workspace
            && workspaceRole is { } role
            && role != WorkspaceRole.Guest);
}
