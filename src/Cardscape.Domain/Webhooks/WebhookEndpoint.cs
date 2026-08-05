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
    private static Result ValidateNotInternalHost(Uri parsed)
    {
        string host = parsed.Host;
        if (string.IsNullOrEmpty(host))
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.url_host_required", "Webhook URL must have a hostname."));
        }

        // Cheap textual checks first (no DNS). Hostnames the
        // platform conventionally treats as local.
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(DomainError.Validation(
                "webhooks.url_host_internal",
                "Webhook URL host resolves to a local or internal address."));
        }

        // IP literal? Reject anything non-global.
        if (System.Net.IPAddress.TryParse(host, out System.Net.IPAddress? ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)
                || ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                   && (IsPrivateIPv4(ip) || IsLinkLocalIPv4(ip) || IsMulticastIPv4(ip))
                || ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                   && (IsPrivateIPv6(ip) || IsLinkLocalIPv6(ip) || IsMulticastIPv6(ip)))
            {
                return Result.Failure(DomainError.Validation(
                    "webhooks.url_host_internal",
                    "Webhook URL host resolves to a local or internal address."));
            }
        }
        else
        {
            // DNS resolution check. An attacker can't bypass this
            // by pointing at a public hostname that resolves to
            // 127.0.0.1 or to a metadata IP; both literal IPs
            // AND resolved IPs are blocked.
            try
            {
                System.Net.IPAddress[] addresses = System.Net.Dns.GetHostAddresses(host);
                foreach (System.Net.IPAddress resolved in addresses)
                {
                    if (System.Net.IPAddress.IsLoopback(resolved)
                        || resolved.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                           && (IsPrivateIPv4(resolved) || IsLinkLocalIPv4(resolved) || IsMulticastIPv4(resolved))
                        || resolved.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                           && (IsPrivateIPv6(resolved) || IsLinkLocalIPv6(resolved) || IsMulticastIPv6(resolved)))
                    {
                        return Result.Failure(DomainError.Validation(
                            "webhooks.url_host_internal",
                            "Webhook URL host resolves to a local or internal address."));
                    }
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                return Result.Failure(DomainError.Validation(
                    "webhooks.url_host_unresolvable",
                    "Webhook URL host could not be resolved."));
            }
        }

        return Result.Success();
    }

    private static bool IsPrivateIPv4(System.Net.IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        // 10.0.0.0/8
        if (b[0] == 10) return true;
        // 172.16.0.0/12
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        // 192.168.0.0/16
        if (b[0] == 192 && b[1] == 168) return true;
        // 0.0.0.0/8
        if (b[0] == 0) return true;
        return false;
    }

    private static bool IsLinkLocalIPv4(System.Net.IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        // 169.254.0.0/16 — covers the AWS/GCP/Azure metadata
        // endpoint (169.254.169.254) and other link-local.
        if (b[0] == 169 && b[1] == 254) return true;
        return false;
    }

    private static bool IsMulticastIPv4(System.Net.IPAddress ip)
    {
        // 224.0.0.0/4
        return ip.GetAddressBytes()[0] >= 224 && ip.GetAddressBytes()[0] <= 239;
    }

    private static bool IsPrivateIPv6(System.Net.IPAddress ip)
    {
        // fc00::/7 — unique-local addresses.
        byte[] b = ip.GetAddressBytes();
        return (b[0] & 0xFE) == 0xFC;
    }

    private static bool IsLinkLocalIPv6(System.Net.IPAddress ip)
    {
        // fe80::/10
        byte[] b = ip.GetAddressBytes();
        return b[0] == 0xFE && (b[1] & 0xC0) == 0x80;
    }

    private static bool IsMulticastIPv6(System.Net.IPAddress ip)
    {
        // ff00::/8
        return ip.GetAddressBytes()[0] == 0xFF;
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
