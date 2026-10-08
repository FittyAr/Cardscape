namespace Cardscape.Domain.Common;

/// <summary>
/// The event types an integration (webhooks, Slack, GitHub) can forward,
/// and the rules for the comma-separated subscription list each
/// integration stores: lower-case, de-duplicated, ordinal-sorted.
/// </summary>
/// <param name="ErrorPrefix">Error-code prefix, e.g. <c>slack</c> → <c>slack.event_unknown</c>.</param>
/// <param name="DisplayName">Integration name used in messages, e.g. <c>Slack</c>.</param>
/// <param name="All">Every supported event type, in the order the UI presents them.</param>
public sealed record EventCatalog(string ErrorPrefix, string DisplayName, IReadOnlyList<string> All)
{
    /// <summary>True if <paramref name="eventType"/> is in the catalog (case-insensitive).</summary>
    public bool IsKnown(string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && All.Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Validates <paramref name="events"/> and returns the canonical stored
    /// form. Blank entries are skipped; at least one known event is required.
    /// </summary>
    public Result<string> ToSubscription(IEnumerable<string>? events)
    {
        if (events is null)
        {
            return Result.Failure<string>(EventsRequired());
        }

        HashSet<string> normalised = new(StringComparer.OrdinalIgnoreCase);
        foreach (string e in events)
        {
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            string trimmed = e.Trim().ToLowerInvariant();
            if (!IsKnown(trimmed))
            {
                return Result.Failure<string>(DomainError.Validation(
                    $"{ErrorPrefix}.event_unknown",
                    $"Unknown {DisplayName} event type '{e}'. Allowed: {string.Join(", ", All)}"));
            }

            normalised.Add(trimmed);
        }

        return normalised.Count == 0
            ? Result.Failure<string>(EventsRequired())
            : Result.Success(string.Join(",", normalised.Order(StringComparer.Ordinal)));
    }

    /// <summary>True if the stored <paramref name="subscription"/> includes <paramref name="eventType"/>.</summary>
    public static bool Includes(string? subscription, string eventType) =>
        !string.IsNullOrWhiteSpace(eventType)
        && !string.IsNullOrEmpty(subscription)
        && subscription
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));

    private DomainError EventsRequired() =>
        DomainError.Validation($"{ErrorPrefix}.events_required", "At least one event type is required.");
}
