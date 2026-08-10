using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Seeder.Generators;

/// <summary>Tiny utility surface the seeder uses to build
/// deterministic-looking but random data without pulling in a
/// full Bogus dependency.</summary>
public static class PasswordGenerator
{
    /// <summary>Returns a strong demo password that the seeder
    /// can hand to every user it creates. The cleartext is the
    /// same across runs — the seeder is for demo only, not for
    /// production deployments — but the value is hashed with the
    /// configured <c>IPasswordHasher</c> before persistence.</summary>
    public static string DemoPassword() => "Nexora!Demo-2026";

    /// <summary>Returns a 64-character lowercase hex SHA-256
    /// digest of <paramref name="input"/>. Used to seed the
    /// opaque hash columns that the domain aggregates expose
    /// (SCIM tokens, webhook secrets, OAuth client secrets).</summary>
    public static string Sha256Hex(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Returns a URL-safe base64-encoded random string
    /// of <paramref name="byteCount"/> random bytes. The SCIM
    /// and OAuth flows use this for their plaintext
    /// tokens.</summary>
    public static string RandomUrlSafeToken(int byteCount)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>Picks a deterministic 8-character prefix of
    /// <paramref name="token"/> for the audit-log column.</summary>
    public static string Prefix(string token, int length) =>
        string.IsNullOrEmpty(token) || token.Length <= length
            ? token
            : token[..length];
}
