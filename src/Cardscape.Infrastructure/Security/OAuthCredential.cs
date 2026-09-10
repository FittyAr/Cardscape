using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Infrastructure.Security;

internal static class OAuthCredential
{
    internal const int SecretByteLength = 32;

    internal static string GenerateClientId() => Generate(24);

    internal static string GenerateSecret() => Generate(SecretByteLength);

    internal static string Hash(string cleartext) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(cleartext)))
            .ToLowerInvariant();

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

    private static string Generate(int byteLength)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
