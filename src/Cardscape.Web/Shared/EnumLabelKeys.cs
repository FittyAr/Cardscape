namespace Cardscape.Web.Shared;

/// <summary>Resource keys for the enum values the UI names in more than one page.</summary>
public static class EnumLabelKeys
{
    extension(WorkspaceRole role)
    {
        public string LabelKey => role switch
        {
            WorkspaceRole.Admin => "WorkspaceRoleAdmin",
            WorkspaceRole.Member => "WorkspaceRoleMember",
            WorkspaceRole.Observer => "WorkspaceRoleObserver",
            _ => "CommonUnknown",
        };
    }

    extension(BoardMemberRole role)
    {
        public string LabelKey => role switch
        {
            BoardMemberRole.Admin => "BoardRoleAdmin",
            BoardMemberRole.Member => "BoardRoleMember",
            BoardMemberRole.Observer => "BoardRoleObserver",
            _ => "CommonUnknown",
        };
    }

    extension(BoardVisibility visibility)
    {
        public string LabelKey => visibility switch
        {
            BoardVisibility.Private => "BoardsVisibilityPrivate",
            BoardVisibility.Workspace => "BoardsVisibilityWorkspace",
            _ => "BoardsVisibilityPublic",
        };
    }
}
