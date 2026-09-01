using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Cardscape.Application.Abstractions.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Infrastructure.Authentication;

/// <summary>
/// Apple "Sign in with Apple" client_secret generator. The
/// secret is a JWT signed with the team's ES256 private key
/// (.p8 file from developer.apple.com). The configuration
/// block lives at <c>Authentication:Apple:*</c>:
/// <list type="bullet">
///   <item><c>TeamId</c> — the 10-character Apple developer
///   team id (the <c>iss</c> claim).</item>
///   <item><c>ClientId</c> — the Service Id (the <c>sub</c>
///   claim).</item>
///   <item><c>KeyId</c> — the 10-character key id from the
///   .p8 filename (the <c>kid</c> header).</item>
///   <item><c>PrivateKeyPem</c> — the contents of the .p8
///   file (PEM-encoded EC private key).</item>
/// </list>
/// </summary>
public sealed class AppleClientSecretGenerator : IAppleClientSecretGenerator
{
    private readonly string _teamId;
    private readonly string _clientId;
    private readonly string _keyId;
    private readonly ECDsa _signingKey;

    public AppleClientSecretGenerator(IConfiguration configuration)
    {
        _teamId = configuration["Authentication:Apple:TeamId"] ?? string.Empty;
        _clientId = configuration["Authentication:Apple:ClientId"] ?? string.Empty;
        _keyId = configuration["Authentication:Apple:KeyId"] ?? string.Empty;
        string? privateKeyPem = configuration["Authentication:Apple:PrivateKeyPem"];

        if (string.IsNullOrWhiteSpace(_teamId)
            || string.IsNullOrWhiteSpace(_clientId)
            || string.IsNullOrWhiteSpace(_keyId)
            || string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new InvalidOperationException(
                "Apple Sign-In is not configured. Set Authentication:Apple:TeamId, " +
                "ClientId, KeyId, and PrivateKeyPem before invoking " +
                "IAppleClientSecretGenerator.");
        }

        _signingKey = ECDsa.Create();
        _signingKey.ImportFromPem(privateKeyPem);
    }

    public string GenerateClientSecret(TimeSpan ttl)
    {
        // Apple's spec caps the lifetime at six months; we
        // clamp defensively so a misconfigured caller can't
        // produce a token Apple will reject.
        TimeSpan lifetime = ttl > TimeSpan.FromDays(180) ? TimeSpan.FromDays(180) : ttl;
        DateTime now = DateTime.UtcNow;
        DateTime expires = now + lifetime;

        var claims = new[]
        {
            new Claim("iss", _teamId),
            new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new Claim("exp", new DateTimeOffset(expires).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new Claim("aud", "https://appleid.apple.com"),
            new Claim("sub", _clientId)
        };

        // The KeyId header on the JWS is how Apple looks
        // up the right public key on its end. Set it
        // directly on the SecurityKey.
        var securityKey = new ECDsaSecurityKey(_signingKey) { KeyId = _keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var jwt = new JwtSecurityToken(
            issuer: _teamId,
            audience: "https://appleid.apple.com",
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
