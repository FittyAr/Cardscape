namespace Cardscape.Application.Abstractions.Authentication;

/// <summary>
/// Apple "Sign in with Apple" requires the OAuth
/// <c>client_secret</c> to be a fresh JWT signed with
/// ES256 using a private key downloaded from
/// developer.apple.com (the .p8 file). The JWT is short-lived
/// (Apple's maximum is six months) and the implementation
/// generates one per token request so the server never holds
/// a long-lived Apple-issued secret.
/// </summary>
public interface IAppleClientSecretGenerator
{
    /// <summary>Generates a fresh client_secret JWT. The
    /// <c>exp</c> is <paramref name="ttl"/> from now; the
    /// caller is expected to clamp <paramref name="ttl"/>
    /// to Apple's six-month maximum.</summary>
    string GenerateClientSecret(TimeSpan ttl);
}
