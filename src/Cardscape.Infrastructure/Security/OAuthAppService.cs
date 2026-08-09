using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Integrations.OAuthApps.Errors;
using Cardscape.Domain.Members;
using Microsoft.Extensions.Logging;

namespace Cardscape.Infrastructure.Security;

/// <summary>
/// Default implementation of <see cref="IOAuthAppService"/>.
/// Owns the secret-generation (the domain aggregate knows
/// nothing about random bytes) and the SHA-256 hashing of
/// bearer tokens / authorization codes (the domain aggregate
/// only knows the opaque hash).
/// </summary>
public sealed class OAuthAppService(
    IOAuthAppRepository apps,
    IOAuthAuthorizationCodeRepository codes,
    IOAuthAccessTokenRepository tokens,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<OAuthAppService> logger) : IOAuthAppService
{
    /// <summary>5 minutes — short, by design.</summary>
    public static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>30 days — long enough to be useful, short enough
    /// to limit the blast radius of a leak.</summary>
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>Length in bytes of the random <c>clientSecret</c>
    /// and of the random authorization code / access token.
    /// 32 bytes = 256 bits, the recommended minimum for a
    /// high-entropy bearer credential.</summary>
    public const int SecretByteLength = 32;

    public async Task<Result<OAuthAppRegistration>> RegisterAsync(
        UserId ownerId,
        string name,
        IReadOnlyCollection<string> allowedScopes,
        IReadOnlyCollection<string> redirectUris,
        CancellationToken ct)
    {
        // Ensure the owner actually exists. We don't reject
        // unknown owners here — the caller has already passed
        // authentication and ICurrentUser.Id is authoritative
        // — but we want the FK to a real user row to be in
        // place at insert time.
        _ = await users.GetByIdAsync(ownerId, ct)
            ?? throw new InvalidOperationException(
                $"Cannot register OAuth app for non-existent user {ownerId}.");

        string clientId = GenerateClientId();
        string clientSecret = GenerateSecret();
        string hashed = HashSecret(clientSecret);
        string prefix = clientSecret[..8];

        var app = OAuthApp.Register(
            new OAuthAppId(Guid.NewGuid()),
            name.Trim(),
            clientId,
            hashed,
            ownerId.Value,
            allowedScopes.ToList(),
            redirectUris.ToList(),
            clock.UtcNow);

        if (app.IsFailure)
        {
            // BUG-A1-002: validation errors (e.g. invalid redirect
            // URI) used to be thrown as InvalidOperationException →
            // 500. They are expected failures (4xx), so return
            // the Result and let the endpoint map it to 400.
            logger.LogWarning(
                "OAuth app registration rejected: {Code} {Message}",
                app.Error.Code, app.Error.Message);
            return Result.Failure<OAuthAppRegistration>(app.Error);
        }

        await apps.AddAsync(app.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new OAuthAppRegistration(
            app.Value.Id,
            clientId,
            clientSecret,
            prefix));
    }

    public async Task<Result> RevokeAppAsync(
        OAuthAppId appId,
        UserId requesterId,
        CancellationToken ct)
    {
        var app = await apps.GetByIdAsync(appId, ct);
        if (app is null)
        {
            return Result.Failure(OAuthAppErrors.UnknownClient);
        }

        if (app.OwnerId != requesterId.Value)
        {
            return Result.Failure(DomainError.Forbidden(
                "oauth.not_owner",
                "Only the owner of the application can revoke it."));
        }

        app.Revoke(clock.UtcNow);
        // Persist the soft-delete. Without this line the
        // Revoke call mutates the in-memory aggregate but
        // the row stays in the DB with IsRevoked=false; the
        // next /api/oauth-apps/ list returns the app as if
        // nothing happened. Caught by the G15 integration
        // test pass (the OAuthAppEndpointTests.Revoke_Marks_
        // OAuthApp_As_Revoked test now asserts the IsRevoked
        // flag flipped).
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<OAuthAppSummary>> ListForOwnerAsync(
        UserId ownerId,
        CancellationToken ct)
    {
        var rows = await apps.ListForOwnerAsync(ownerId.Value, ct);
        return rows
            .Select(a => new OAuthAppSummary(
                a.Id.Value,
                a.Name,
                a.ClientId,
                a.ClientSecretHash[..Math.Min(8, a.ClientSecretHash.Length)],
                a.AllowedScopes,
                a.RedirectUris,
                a.IsRevoked,
                a.CreatedAt))
            .ToList();
    }

    public async Task<OAuthAuthorizationCodeIssuance> IssueAuthorizationCodeAsync(
        string clientId,
        UserId userId,
        string redirectUri,
        IReadOnlyCollection<string> scopes,
        CancellationToken ct)
    {
        var app = await apps.FindByClientIdAsync(clientId, ct)
            ?? throw new InvalidOperationException(
                $"Cannot issue auth code for unknown client {clientId}.");

        if (app.IsRevoked)
        {
            throw new InvalidOperationException(
                $"Cannot issue auth code for revoked app {app.Id}.");
        }

        if (!app.RedirectUris.Contains(redirectUri))
        {
            throw new InvalidOperationException(
                $"Redirect URI {redirectUri} not registered for app {app.Id}.");
        }

        // Defence in depth: even if a future refactor
        // edits the RedirectUris list through a path that
        // bypasses the OAuthApp.Register validation
        // (a UI in the Web app, an admin migration, a
        // bulk import), the authorize endpoint must
        // refuse to redirect the bearer to a non-http(s)
        // URL. Without this check a malicious owner who
        // somehow got a javascript: URI past Register
        // could turn the /oauth/authorize endpoint into
        // an XSS sink. The cost is one Uri.TryCreate per
        // authorize call; the safety is permanent.
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Redirect URI {redirectUri} is not an absolute http(s) URL.");
        }

        string cleartext = GenerateSecret();
        string codeHash = HashSecret(cleartext);
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset expiresAt = now + AuthorizationCodeLifetime;

        var codeResult = OAuthAuthorizationCode.Issue(
            new OAuthAuthorizationCodeId(Guid.NewGuid()),
            app.Id,
            userId,
            redirectUri,
            codeHash,
            scopes.ToList(),
            expiresAt,
            now);

        if (codeResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"OAuthAuthorizationCode.Issue invariant failed unexpectedly: {codeResult.Error.Code} {codeResult.Error.Message}");
        }

        await codes.AddAsync(codeResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new OAuthAuthorizationCodeIssuance(cleartext, expiresAt);
    }

    public async Task<Result<OAuthAccessTokenIssuance>> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        CancellationToken ct)
    {
        var app = await apps.FindByClientIdAsync(clientId, ct);
        if (app is null)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.UnknownClient);
        }

        if (app.IsRevoked)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.AppRevoked);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(HashSecret(clientSecret)),
                Encoding.ASCII.GetBytes(app.ClientSecretHash)))
        {
            logger.LogWarning("OAuth app {ClientId} presented an invalid client secret.", clientId);
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.InvalidClientSecret);
        }

        string codeHash = HashSecret(code);
        var authCode = await codes.FindByCodeHashAsync(codeHash, ct);
        if (authCode is null)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.UnknownCode);
        }

        if (authCode.AppId != app.Id)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.UnknownCode);
        }

        if (!string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
        {
            return Result.Failure<OAuthAccessTokenIssuance>(OAuthAppErrors.RedirectUriMismatch);
        }

        var consumeResult = authCode.MarkConsumed(clock.UtcNow);
        if (consumeResult.IsFailure)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(consumeResult.Error);
        }

        string cleartextToken = GenerateSecret();
        string tokenHash = HashSecret(cleartextToken);
        DateTimeOffset now = clock.UtcNow;
        DateTimeOffset expiresAt = now + AccessTokenLifetime;

        var tokenResult = OAuthAccessToken.Issue(
            new OAuthAccessTokenId(Guid.NewGuid()),
            app.Id,
            authCode.UserId,
            tokenHash,
            authCode.Scopes,
            expiresAt,
            now);

        if (tokenResult.IsFailure)
        {
            return Result.Failure<OAuthAccessTokenIssuance>(tokenResult.Error);
        }

        await tokens.AddAsync(tokenResult.Value, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new OAuthAccessTokenIssuance(
            cleartextToken,
            "Bearer",
            (int)AccessTokenLifetime.TotalSeconds,
            authCode.Scopes,
            RefreshToken: null));
    }

    public async Task<Result<OAuthAccessTokenValidation>> ValidateAccessTokenAsync(
        string cleartextToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cleartextToken))
        {
            return Result.Failure<OAuthAccessTokenValidation>(OAuthAppErrors.UnknownAccessToken);
        }

        string tokenHash = HashSecret(cleartextToken);
        var token = await tokens.FindByTokenHashAsync(tokenHash, ct);
        if (token is null)
        {
            return Result.Failure<OAuthAccessTokenValidation>(OAuthAppErrors.UnknownAccessToken);
        }

        if (token.RevokedAt is not null)
        {
            return Result.Failure<OAuthAccessTokenValidation>(OAuthAppErrors.AccessTokenRevoked);
        }

        if (clock.UtcNow >= token.ExpiresAt)
        {
            return Result.Failure<OAuthAccessTokenValidation>(OAuthAppErrors.AccessTokenExpired);
        }

        return Result.Success(new OAuthAccessTokenValidation(
            token.UserId,
            token.AppId,
            token.Scopes));
    }

    public async Task<Result> RevokeAccessTokenAsync(
        string cleartextToken,
        string clientId,
        string clientSecret,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cleartextToken))
        {
            return Result.Failure(OAuthAppErrors.UnknownAccessToken);
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return Result.Failure(OAuthAppErrors.InvalidClientSecret);
        }

        // RFC 7009 §2.1: "The authorization server first
        // validates the client credentials [...] and then
        // verifies whether the token was issued to the client
        // making the revocation request." We look up the
        // client first, fail on a missing / revoked / bad-secret
        // presentation, and only then attempt the revocation.
        // The token lookup happens after the client check so a
        // bad client_id never reveals whether the token exists.
        var app = await apps.FindByClientIdAsync(clientId, ct);
        if (app is null)
        {
            logger.LogWarning("OAuth revoke for unknown client {ClientId}.", clientId);
            return Result.Failure(OAuthAppErrors.InvalidClientSecret);
        }

        if (app.IsRevoked)
        {
            return Result.Failure(OAuthAppErrors.AppRevoked);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(HashSecret(clientSecret)),
                Encoding.ASCII.GetBytes(app.ClientSecretHash)))
        {
            logger.LogWarning("OAuth revoke for client {ClientId} presented an invalid secret.", clientId);
            return Result.Failure(OAuthAppErrors.InvalidClientSecret);
        }

        string tokenHash = HashSecret(cleartextToken);
        var token = await tokens.FindByTokenHashAsync(tokenHash, ct);
        if (token is null)
        {
            // RFC 7009 §2.2: revocation of an unknown token
            // returns 200 so the server does not leak which
            // tokens existed. The client authenticated
            // successfully, so this is a clean no-op.
            return Result.Success();
        }

        // The client knows the token's plaintext; if the
        // token's AppId does not match the client that is
        // presenting it, refuse the revoke. A successful
        // 200 here would otherwise let any client with a
        // valid secret revoke any token they could guess or
        // leak the existence of.
        if (token.AppId != app.Id)
        {
            logger.LogWarning(
                "OAuth revoke rejected: client {ClientId} presented token owned by a different app.",
                clientId);
            return Result.Failure(OAuthAppErrors.InvalidClientSecret);
        }

        token.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<OAuthUserInfo>> GetUserInfoAsync(
        string cleartextToken,
        CancellationToken ct)
    {
        var validation = await ValidateAccessTokenAsync(cleartextToken, ct);
        if (validation.IsFailure)
        {
            return Result.Failure<OAuthUserInfo>(validation.Error);
        }

        var user = await users.GetByIdAsync(validation.Value.UserId, ct);
        if (user is null)
        {
            return Result.Failure<OAuthUserInfo>(DomainError.NotFound(
                "users.not_found",
                "The user backing this access token no longer exists."));
        }

        return Result.Success(new OAuthUserInfo(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName.Value));
    }

    // ── helpers ────────────────────────────────────────────────

    /// <summary>Generates a public, opaque, base64url-encoded
    /// <c>clientId</c> (24 random bytes → 32-char string,
    /// URL-safe). Unique with overwhelming probability.</summary>
    private static string GenerateClientId()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[SecretByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string HashSecret(string cleartext) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.ASCII.GetBytes(cleartext))).ToLowerInvariant();

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
