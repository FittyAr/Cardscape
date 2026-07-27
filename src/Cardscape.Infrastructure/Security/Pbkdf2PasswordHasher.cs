using System.Security.Cryptography;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// PBKDF2 (HMAC-SHA256, 100k iterations, 16-byte salt) password
/// hasher. The stored format is <c>v1.{base64-salt}.{base64-hash}</c>.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public PasswordHash Hash(string plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            plaintext, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        var encoded = $"v1.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        return PasswordHash.FromHashed(encoded).Value;
    }

    public bool Verify(string plaintext, PasswordHash hash)
    {
        var parts = hash.Value.Split('.');
        if (parts.Length != 3 || parts[0] != "v1")
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            plaintext, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
