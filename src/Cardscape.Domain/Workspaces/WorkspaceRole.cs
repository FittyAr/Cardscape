namespace Cardscape.Domain.Workspaces;

/// <summary>Role of a user inside a workspace.</summary>
public enum WorkspaceRole
{
    /// <summary>Full administrative access inside the workspace.</summary>
    Admin = 0,

    /// <summary>Standard member: can create boards and cards.</summary>
    Member = 1,

    /// <summary>Read-only access.</summary>
    Observer = 2
}
