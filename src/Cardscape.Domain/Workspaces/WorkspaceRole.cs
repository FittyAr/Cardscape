namespace Cardscape.Domain.Workspaces;

/// <summary>Role of a user inside a workspace.</summary>
public enum WorkspaceRole
{
    /// <summary>Full administrative access inside the workspace.</summary>
    Admin = 0,

    /// <summary>Standard member: can create boards and cards.</summary>
    Member = 1,

    /// <summary>Read-only access.</summary>
    Observer = 2,

    /// <summary>
    /// Trello-style guest: belongs to the workspace only through the
    /// boards they were explicitly added to. A guest does not see
    /// workspace-visible boards, cannot create boards, invite or manage
    /// people, sees only the people on their shared boards, and holds
    /// at most the <see cref="Boards.BoardMemberRole.Member"/> role on a
    /// board (see <see cref="WorkspaceGuestRules"/>).
    /// </summary>
    Guest = 3
}
