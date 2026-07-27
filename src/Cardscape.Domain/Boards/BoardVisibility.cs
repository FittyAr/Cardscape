namespace Cardscape.Domain.Boards;

/// <summary>Visibility of a board inside its workspace.</summary>
public enum BoardVisibility
{
    /// <summary>Only workspace members can see the board.</summary>
    Private = 0,

    /// <summary>Visible to all workspace members; can be opened without explicit membership.</summary>
    Workspace = 1,

    /// <summary>Visible to any authenticated user with a link.</summary>
    Public = 2
}
