using Cardscape.Domain.Boards;
using Cardscape.Domain.Common;

namespace Cardscape.Domain.Webhooks;

/// <summary>
/// A board-scoped destination for outbound webhook calls. Each
/// endpoint is subscribed to one or more event types
/// (<see cref="WebhookEventTypes"/>) and signs its payloads with
/// a protected shared secret. Soft-deleted
/// endpoints stay in the table so past <see cref="WebhookDelivery"/>
/// rows keep their foreign-key target, but new events skip them.
/// </summary>
public sealed class WebhookEndpoint : AggregateRoot<WebhookEndpointId>
{
    public BoardId BoardId { get; private set; } = null!;

    /// <summary>Fully-qualified destination URL the handler will POST to.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>
    /// Data Protection ciphertext of the shared signing secret.
    /// Cleartext is returned once and never persisted.
    /// </summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    /// <summary>
    /// Comma-joined list of subscribed event type identifiers
    /// (e.g. <c>"card.created,card.moved"</c>). Stored as a single
    /// SQLite TEXT column.
    /// </summary>
    public string Events { get; private set; } = string.Empty;

    public bool Active { get; private set; } = true;

    // EF Core.
    private WebhookEndpoint() { }

    private WebhookEndpoint(
        WebhookEndpointId id,
        BoardId boardId,
        string url,
        string protectedSecret,
        string events,
        bool active,
        DateTimeOffset at)
    {
        Id = id;
        BoardId = boardId;
        Url = url;
        ProtectedSecret = protectedSecret;
        Events = events;
        Active = active;
        CreatedAt = at;
    }

    public static Result<WebhookEndpoint> Create(
        WebhookEndpointId id,
        BoardId boardId,
        string url,
        string protectedSecret,
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

        // SSRF guard: reject URLs that resolve to private,
        // loopback, link-local, multicast, or otherwise non-
        // routable addresses. The application layer can
        // override this list (e.g. for an on-prem
        // installation that legitimately needs to talk to
        // an internal service), but the default is
        // conservative. The check runs at creation AND on
        // every URL change so a misconfigured endpoint
        // can't be retrofitted after the fact.
        Result ssrfCheck = ValidateNotInternalHost(parsed);
        if (ssrfCheck.IsFailure)
        {
            return Result.Failure<WebhookEndpoint>(ssrfCheck.Error);
        }

        if (url.Length > 500)
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.url_too_long", "Webhook URL must be 500 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.secret_required", "Webhook secret is required."));
        }

        if (protectedSecret.Length > 2048)
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.secret_protected_invalid", "Protected webhook secret is too large."));
        }

        if (string.IsNullOrWhiteSpace(events))
        {
            return Result.Failure<WebhookEndpoint>(DomainError.Validation(
                "webhooks.events_required", "At least one event type is required."));
        }

        return Result.Success(new WebhookEndpoint(
            id, boardId, parsed.ToString(), protectedSecret, events, active: true, at));
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

        // Same SSRF guard as the factory — see comment in Create.
        Result ssrfCheck = ValidateNotInternalHost(parsed);
        if (ssrfCheck.IsFailure)
        {
            return ssrfCheck;
        }

        if (parsed.ToString() == Url)
        {
            return Result.Success();
        }

        Url = parsed.ToString();
        return Result.Success();
    }

    /// <summary>
    /// Rejects hostnames that resolve to loopback, link-local,
    /// site-local (private), multicast, or otherwise non-routable
    /// addresses. The handler is intentionally a single DNS
    /// lookup — a second lookup at delivery time would race with
    /// DNS rebinding; the recommendation is to pair this with
    /// outbound IP pinning at the HttpClient level (the
    /// infrastructure layer controls the SocketsHttpHandler).
    /// Cloud metadata endpoints (169.254.169.254, fd00:ec2::254)
    /// are in the block-list.
    /// </summary>
    private static Result ValidateNotInternalHost(Uri parsed) =>
        WebhookUrlValidator.ValidateNotInternalHost(parsed);

    private static bool IsPrivateIPv4(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsPrivateIPv4(ip);

    private static bool IsLinkLocalIPv4(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsLinkLocalIPv4(ip);

    private static bool IsMulticastIPv4(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsMulticastIPv4(ip);

    private static bool IsPrivateIPv6(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsPrivateIPv6(ip);

    private static bool IsLinkLocalIPv6(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsLinkLocalIPv6(ip);

    private static bool IsMulticastIPv6(System.Net.IPAddress ip) =>
        WebhookUrlValidator.IsMulticastIPv6(ip);

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

    /// <summary>Replaces the protected shared secret.</summary>
    public Result RotateProtectedSecret(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret) || protectedSecret.Length > 2048)
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.secret_protected_invalid", "Protected webhook secret is invalid."));
        }

        if (string.Equals(protectedSecret, ProtectedSecret, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        ProtectedSecret = protectedSecret;
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
