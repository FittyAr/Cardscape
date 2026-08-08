using Cardscape.Domain.Common;

namespace Cardscape.Domain.Webhooks;

/// <summary>
/// Shared SSRF guard for webhook URLs. Used by
/// <see cref="WebhookEndpoint"/> at Create / ChangeUrl
/// time (the primary gate) and by the
/// <c>WebhookDeliveryHandler</c> at delivery time
/// (defence in depth against a DNS rebinding attack
/// that swaps the public resolution for an internal
/// one between enqueue and dispatch).
/// <para>
/// The check runs once per call. A second DNS lookup
/// at delivery time would race with DNS rebinding;
/// the recommendation is to pair this with outbound IP
/// pinning at the HttpClient level (the
/// infrastructure layer controls the
/// SocketsHttpHandler). Cloud metadata endpoints
/// (169.254.169.254, fd00:ec2::254) are in the
/// block-list.
/// </para>
/// </summary>
public static class WebhookUrlValidator
{
    /// <summary>
    /// BETA-9-#1 — see test-results/r9/r9-report.md.
    /// Returns true if the URL resolves to a local / internal address.
    /// Used by the application-layer command handlers that want the
    /// security-critical SSRF check to run BEFORE any other validation
    /// (e.g. secret length) so a misconfigured request never gets a
    /// more specific error than "URL is internal".
    /// </summary>
    public static bool IsInternalHost(Uri parsed) => ValidateNotInternalHost(parsed).IsFailure;

    public static Result ValidateNotInternalHost(Uri parsed)
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

    internal static bool IsPrivateIPv4(System.Net.IPAddress ip)
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

    internal static bool IsLinkLocalIPv4(System.Net.IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();
        // 169.254.0.0/16 — covers the AWS/GCP/Azure metadata
        // endpoint (169.254.169.254) and other link-local.
        if (b[0] == 169 && b[1] == 254) return true;
        return false;
    }

    internal static bool IsMulticastIPv4(System.Net.IPAddress ip)
    {
        // 224.0.0.0/4
        return ip.GetAddressBytes()[0] >= 224 && ip.GetAddressBytes()[0] <= 239;
    }

    internal static bool IsPrivateIPv6(System.Net.IPAddress ip)
    {
        // fc00::/7 — unique-local addresses.
        byte[] b = ip.GetAddressBytes();
        return (b[0] & 0xFE) == 0xFC;
    }

    internal static bool IsLinkLocalIPv6(System.Net.IPAddress ip)
    {
        // fe80::/10
        byte[] b = ip.GetAddressBytes();
        return b[0] == 0xFE && (b[1] & 0xC0) == 0x80;
    }

    internal static bool IsMulticastIPv6(System.Net.IPAddress ip)
    {
        // ff00::/8
        return ip.GetAddressBytes()[0] == 0xFF;
    }
}
