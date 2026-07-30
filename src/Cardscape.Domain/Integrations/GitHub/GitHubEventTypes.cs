using Cardscape.Domain.Cards;

namespace Cardscape.Domain.Integrations.GitHub;

/// <summary>
/// Catalogue of GitHub event types the v1 GitHub integration can
/// forward. The list mirrors the Slack event catalogue because
/// the same <see cref="Card"/>-level signals drive both.
/// </summary>
public static class GitHubEventTypes
{
    public const string CardCreated = "card.created";
    public const string CardMoved = "card.moved";
    public const string CardCompleted = "card.completed";
    public const string CommentAdded = "comment.added";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CardCreated,
        CardMoved,
        CardCompleted,
        CommentAdded
    };

    public static bool IsKnown(string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && All.Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));
}
