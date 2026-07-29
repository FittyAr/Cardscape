using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Application.Abstractions.Idempotency;

/// <summary>
/// Helpers for hashing a request body into a stable
/// <c>SHA-256</c> digest that the <c>idempotency_keys</c>
/// table can compare against. The hash is deterministic and
/// case-insensitive on the wire (we lower-case the hex
/// output) so a retried request produces the same digest
/// regardless of how the client serialised the JSON.
/// </summary>
public static class RequestHasher
{
    /// <summary>
    /// Hashes <paramref name="rawBody"/> (UTF-8 JSON or any
    /// other canonicalised byte stream) into a lowercase hex
    /// SHA-256 digest. <paramref name="rawBody"/> may be
    /// <c>null</c> or empty (in which case the hash of the
    /// empty string is returned) — handlers with no body
    /// (e.g. a parameterised POST) still get a stable
    /// digest as long as the same parameters are sent.
    /// </summary>
    public static string Hash(string? rawBody)
    {
        byte[] bytes = string.IsNullOrEmpty(rawBody)
            ? []
            : Encoding.UTF8.GetBytes(rawBody);
        byte[] digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
