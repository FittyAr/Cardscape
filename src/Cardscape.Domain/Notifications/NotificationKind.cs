namespace Cardscape.Domain.Notifications;

/// <summary>Categories of in-app notification.</summary>
public enum NotificationKind
{
    /// <summary>You were added as a member to a board or workspace.</summary>
    AddedAsMember = 0,

    /// <summary>You were assigned to a card.</summary>
    AssignedToCard = 1,

    /// <summary>You were mentioned in a comment or card description.</summary>
    Mentioned = 2,

    /// <summary>A card you are watching is due soon.</summary>
    DueSoon = 3,

    /// <summary>A card you are watching is overdue.</summary>
    Overdue = 4
}
