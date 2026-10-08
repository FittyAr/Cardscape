using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Cardscape.Application.Authentication.Commands;

internal static class PasswordResetToken
{
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    public static string Hash(string token)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(token), hash);
        return Convert.ToHexString(hash);
    }
}
