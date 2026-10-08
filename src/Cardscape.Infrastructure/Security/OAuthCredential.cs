using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Infrastructure.Security;

internal static class OAuthCredential
{
    internal const int SecretByteLength = 32;

    internal static string GenerateClientId() => SecureToken.RandomBase64Url(24);

    internal static string GenerateSecret() => SecureToken.RandomBase64Url(SecretByteLength);

    internal static string Hash(string cleartext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(cleartext)));

    internal static bool MatchesHash(string cleartext, string expectedHash)
    {
        byte[] actual = SHA256.HashData(Encoding.ASCII.GetBytes(cleartext));
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return expected.Length == SHA256.HashSizeInBytes
            && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
