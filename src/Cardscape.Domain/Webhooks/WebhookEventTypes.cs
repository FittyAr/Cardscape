using Cardscape.Domain.Common;

namespace Cardscape.Domain.Webhooks;

/// <summary>
/// Catalogue of webhook event types supported in v1. The list is
/// intentionally tiny to keep the surface auditable; new events
/// must be added here, the <see cref="All"/> array, and the
/// triggering code path in one commit.
/// </summary>
public static class WebhookEventTypes
{
    public const string CardCreated = "card.created";
    public const string CardMoved = "card.moved";
    public const string CardCompleted = "card.completed";
    public const string CommentAdded = "comment.added";

    /// <summary>The full list, frozen, in the order the UI presents them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        CardCreated,
        CardMoved,
        CardCompleted,
        CommentAdded
    ];

    /// <summary>Validation and matching rules for this catalog.</summary>
    public static readonly EventCatalog Catalog = new("webhooks", "webhook", All);

    /// <summary>True if <paramref name="eventType"/> is one of the
    /// v1-recognised event identifiers.</summary>
    public static bool IsKnown(string eventType) => Catalog.IsKnown(eventType);
}
