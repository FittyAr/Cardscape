using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// A freshly minted bearer secret: the cleartext shown to the caller once,
/// the SHA-256 hash that is persisted, and the short prefix used for
/// lookups and display.
/// </summary>
internal readonly record struct SecureToken(string Cleartext, string Hash, string Prefix)
{
    /// <summary>Mints a URL-safe secret from <paramref name="byteLength"/> random bytes.</summary>
    public static SecureToken Generate(int byteLength, int prefixLength)
    {
        string cleartext = RandomBase64Url(byteLength);
        return new(cleartext, HashHex(cleartext), cleartext[..Math.Min(prefixLength, cleartext.Length)]);
    }

    /// <summary>Unpadded base64url (RFC 4648 §5) text of <paramref name="byteLength"/> random bytes.</summary>
    public static string RandomBase64Url(int byteLength) =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(byteLength));

    /// <summary>Lower-case hex SHA-256 of the UTF-8 bytes of <paramref name="cleartext"/>.</summary>
    public static string HashHex(string cleartext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cleartext)));
}
