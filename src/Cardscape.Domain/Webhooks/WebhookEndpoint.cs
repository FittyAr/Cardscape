using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Webhooks;

/// <summary>
/// A board-scoped destination for outbound webhook calls. Each
/// endpoint is subscribed to one or more event types
/// (<see cref="WebhookEventTypes"/>) and signs its payloads with
/// the HMAC-SHA256 of <see cref="SecretHash"/>. Soft-deleted
/// endpoints stay in the table so past <see cref="WebhookDelivery"/>
/// rows keep their foreign-key target, but new events skip them.
/// </summary>
public sealed class WebhookEndpoint : AggregateRoot<WebhookEndpointId>
{
    public BoardId BoardId { get; private set; } = null!;

    /// <summary>Fully-qualified destination URL the handler will POST to.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>
    /// Hex-encoded HMAC-SHA256 of the cleartext secret. The cleartext
    /// is given to the caller once at creation and never persisted.
    /// </summary>
    public string SecretHash { get; private set; } = string.Empty;

    /// <summary>
    /// Comma-joined list of subscribed event type identifiers
    /// (e.g. <c>"card.created,card.moved"</c>). Stored as a single
    /// TEXT column for portability across the three providers.
    /// </summary>
    public string Events { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    // EF Core.
    private WebhookEndpoint() { }

    private WebhookEndpoint(
        WebhookEndpointId id,
        BoardId boardId,
        string url,
        string secretHash,
        string events,
        bool active,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Url = url;
        SecretHash = secretHash;
        Events = events;
        Active = active;
        CreatedAt = at;
    }

    public static Result<WebhookEndpoint> Create(
        WebhookEndpointId id,
        BoardId boardId,
        string url,
        string secretHash,
        string events,
        DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.url_required", "Webhook URL is required."));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.url_invalid", "Webhook URL must be an absolute http or https URL."));
        }

        if (url.Length > 500)
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.url_too_long", "Webhook URL must be 500 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(secretHash))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.secret_required", "Webhook secret is required."));
        }

        if (secretHash.Length != 64)
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.secret_hash_invalid", "Webhook secret hash must be 64 hex characters (SHA-256)."));
        }

        if (string.IsNullOrWhiteSpace(events))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.events_required", "At least one event type is required."));
        }

        return Result.Success(new WebhookEndpoint(
            id, boardId, parsed.ToString(), secretHash, events, active: true, at));
    }

    /// <summary>Updates the destination URL. Validates the same way as the constructor.</summary>
    public Result ChangeUrl(string newUrl)
    {
        if (string.IsNullOrWhiteSpace(newUrl)
            || !Uri.TryCreate(newUrl, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.url_invalid", "Webhook URL must be an absolute http or https URL."));
        }

        if (parsed.ToString() == Url)
        {
            return Result.Success();
        }

        Url = parsed.ToString();
        return Result.Success();
    }

    /// <summary>Replaces the subscribed event list. The list is
    /// canonicalised (lowercase, deduped, sorted) before storing.</summary>
    public Result ChangeEvents(IEnumerable<string> newEvents)
    {
        if (newEvents is null)
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.events_required", "At least one event type is required."));
        }

        HashSet<string> normalised = new(StringComparer.OrdinalIgnoreCase);
        foreach (string e in newEvents)
        {
            if (string.IsNullOrWhiteSpace(e))
            {
                continue;
            }

            string trimmed = e.Trim().ToLowerInvariant();
            if (!WebhookEventTypes.IsKnown(trimmed))
            {
                return Result.Failure(DomainError.Validation(
                    "webhooks.event_unknown",
                    $"Unknown webhook event type '{e}'. Allowed: "
                    + string.Join(", ", WebhookEventTypes.All)));
            }

            normalised.Add(trimmed);
        }

        if (normalised.Count == 0)
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.events_required", "At least one event type is required."));
        }

        Events = string.Join(",", normalised.OrderBy(s => s, StringComparer.Ordinal));
        return Result.Success();
    }

    /// <summary>Replaces the stored secret hash (hex SHA-256).</summary>
    public Result RotateSecret(string newSecretHash)
    {
        if (string.IsNullOrWhiteSpace(newSecretHash) || newSecretHash.Length != 64)
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.secret_hash_invalid", "Webhook secret hash must be 64 hex characters (SHA-256)."));
        }

        if (string.Equals(newSecretHash, SecretHash, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        SecretHash = newSecretHash;
        return Result.Success();
    }

    /// <summary>Enables the endpoint. Idempotent.</summary>
    public void Activate(DateTimeOffset at)
    {
        if (Active)
        {
            return;
        }

        Active = true;
        UpdatedAt = at;
    }

    /// <summary>Disables the endpoint without deleting it. Idempotent.</summary>
    public void Deactivate(DateTimeOffset at)
    {
        if (!Active)
        {
            return;
        }

        Active = false;
        UpdatedAt = at;
    }

    /// <summary>True if the endpoint subscribes to the given event.</summary>
    public bool SubscribesTo(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrEmpty(Events))
        {
            return false;
        }

        return Events
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(e => string.Equals(e, eventType, StringComparison.OrdinalIgnoreCase));
    }
}
