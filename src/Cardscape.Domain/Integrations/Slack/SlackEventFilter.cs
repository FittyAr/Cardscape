namespace Cardscape.Domain.Integrations.Slack;

/// <summary>
/// Catalogue of Slack event types the v1 Slack integration can
/// forward. The list is intentionally tiny to keep the surface
/// auditable; new events must be added here, the
/// <see cref="All"/> array, and the triggering code path in
/// one commit.
/// </summary>
public static class SlackEventTypes
{
    public const string CardCreated = "card.created";
    public const string CardMoved = "card.moved";
    public const string CardCompleted = "card.completed";
    public const string CommentAdded = "comment.added";

    /// <summary>The full list, frozen, in the order the UI presents them.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        CardCreated,
        CardMoved,
        CardCompleted,
        CommentAdded
    };

    /// <summary>True if <paramref name="eventType"/> is one of the
    /// v1-recognised event identifiers.</summary>
    public static bool IsKnown(string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && All.Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));
}
