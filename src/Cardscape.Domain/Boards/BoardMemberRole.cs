namespace Cardscape.Domain.Boards;

/// <summary>Role of a workspace member inside a specific board.</summary>
public enum BoardMemberRole
{
    /// <summary>Full control of the board, including deletion.</summary>
    Admin = 0,

    /// <summary>Can add, edit, and remove cards and lists.</summary>
    Member = 1,

    /// <summary>Read-only access to the board.</summary>
    Observer = 2
}
