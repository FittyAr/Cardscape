namespace Cardscape.Application.Abstractions.Authentication;

/// <summary>
/// Encrypts / decrypts secrets at rest (currently used for
/// the TOTP shared secret). The default implementation is
/// the <c>DataProtectionSecretProtector</c> in
/// <c>Cardscape.Infrastructure</c>, which delegates to
/// ASP.NET Core's <c>IDataProtector</c>.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Encrypts a cleartext secret. The output is a
    /// base64 string safe to store in a TEXT column.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypts a previously-protected secret. The
    /// inverse of <see cref="Protect"/>.</summary>
    string Unprotect(string protectedValue);
}
