using Cardscape.Domain.Cards;
using Cardscape.Domain.Common;

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

    public static readonly IReadOnlyList<string> All =
    [
        CardCreated,
        CardMoved,
        CardCompleted,
        CommentAdded
    ];

    /// <summary>Validation and matching rules for this catalog.</summary>
    public static readonly EventCatalog Catalog = new("github", "GitHub", All);

    /// <summary>True if <paramref name="eventType"/> is one of the
    /// v1-recognised event identifiers.</summary>
    public static bool IsKnown(string eventType) => Catalog.IsKnown(eventType);
}
