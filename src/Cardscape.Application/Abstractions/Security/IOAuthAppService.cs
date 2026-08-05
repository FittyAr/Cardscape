using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.OAuthApps;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Application service that owns the lifecycle of an
/// <see cref="OAuthApp"/> and the OAuth 2.0 / OIDC code flow:
/// registering apps (and the secret-generation that the domain
/// deliberately knows nothing about), issuing and exchanging
/// authorization codes, minting access tokens, validating
/// incoming bearer tokens, and revoking.
/// </summary>
public interface IOAuthAppService
{
    /// <summary>
    /// Registers a new third-party app for the given owner.
    /// The cleartext <c>clientSecret</c> is returned to the
    /// caller exactly once (the server only persists its
    /// SHA-256 hash + an 8-char prefix for display).
    /// </summary>
    Task<OAuthAppRegistration> RegisterAsync(
        UserId ownerId,
        string name,
        IReadOnlyCollection<string> allowedScopes,
        IReadOnlyCollection<string> redirectUris,
        CancellationToken ct);

    /// <summary>Revokes the given app. Idempotent.</summary>
    Task<Result> RevokeAppAsync(OAuthAppId appId, UserId requesterId, CancellationToken ct);

    /// <summary>Lists every app the given user has registered.</summary>
    Task<IReadOnlyList<OAuthAppSummary>> ListForOwnerAsync(UserId ownerId, CancellationToken ct);

    /// <summary>
    /// Generates a one-shot authorization code for the given
    /// app + user. The cleartext code is returned to the
    /// caller (the consent page) once; the server only keeps
    /// the SHA-256 hash. The code expires in 5 minutes.
    /// </summary>
    Task<OAuthAuthorizationCodeIssuance> IssueAuthorizationCodeAsync(
        string clientId,
        UserId userId,
        string redirectUri,
        IReadOnlyCollection<string> scopes,
        CancellationToken ct);

    /// <summary>
    /// Exchanges an authorization code for a long-lived access
    /// token. The code is marked consumed (one-shot) and the
    /// access token is returned with its cleartext value
    /// exactly once.
    /// </summary>
    Task<Result<OAuthAccessTokenIssuance>> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string code,
        string redirectUri,
        CancellationToken ct);

    /// <summary>
    /// Validates an incoming <c>Authorization: Bearer</c>
    /// access token. On success returns the principal
    /// (userId, appId, granted scopes). Updates the token's
    /// <c>LastUsedAt</c>. On failure returns a domain error.
    /// </summary>
    Task<Result<OAuthAccessTokenValidation>> ValidateAccessTokenAsync(
        string cleartextToken,
        CancellationToken ct);

    /// <summary>
    /// Revokes the given access token, authenticating the
    /// calling client per RFC 7009. The presentation must
    /// include the <c>client_id</c> and <c>client_secret</c>
    /// the third-party app received at registration; the
    /// service refuses to revoke a token whose
    /// <see cref="Domain.Integrations.OAuthApps.OAuthAccessToken.AppId"/>
    /// does not match the supplied client. Unknown tokens
    /// still return success so the endpoint does not leak
    /// which tokens existed.
    /// </summary>
    Task<Result> RevokeAccessTokenAsync(
        string cleartextToken,
        string clientId,
        string clientSecret,
        CancellationToken ct);

    /// <summary>Returns the user-facing <c>/oauth/userinfo</c>
    /// projection for the token's owner.</summary>
    Task<Result<OAuthUserInfo>> GetUserInfoAsync(
        string cleartextToken,
        CancellationToken ct);
}

/// <summary>Returned by <see cref="IOAuthAppService.RegisterAsync"/>.
/// The cleartext <c>ClientSecret</c> is the only time the
/// server ever reveals it.</summary>
public sealed record OAuthAppRegistration(
    OAuthAppId Id,
    string ClientId,
    string ClientSecret,
    string SecretPrefix);

/// <summary>Compact projection of an <see cref="OAuthApp"/> for
/// the Web UI list.</summary>
public sealed record OAuthAppSummary(
    Guid Id,
    string Name,
    string ClientId,
    string SecretPrefix,
    IReadOnlyCollection<string> AllowedScopes,
    IReadOnlyCollection<string> RedirectUris,
    bool IsRevoked,
    DateTimeOffset CreatedAt);

/// <summary>Returned by <see cref="IOAuthAppService.IssueAuthorizationCodeAsync"/>.
/// <c>Code</c> is the cleartext one-shot value the consent
/// page hands to the third-party app.</summary>
public sealed record OAuthAuthorizationCodeIssuance(
    string Code,
    DateTimeOffset ExpiresAt);

/// <summary>Returned by <see cref="IOAuthAppService.ExchangeCodeAsync"/>.
/// <c>AccessToken</c> is the cleartext bearer the third-party
/// app uses on subsequent API calls; <c>RefreshToken</c> is
/// <c>null</c> for v1.1.0 (we don't ship refresh tokens yet).</summary>
public sealed record OAuthAccessTokenIssuance(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    IReadOnlyCollection<string> Scopes,
    string? RefreshToken);

/// <summary>Result of <see cref="IOAuthAppService.ValidateAccessTokenAsync"/>.</summary>
public sealed record OAuthAccessTokenValidation(
    UserId UserId,
    OAuthAppId AppId,
    IReadOnlyCollection<string> Scopes);

/// <summary>User projection returned by
/// <c>GET /oauth/userinfo</c>.</summary>
public sealed record OAuthUserInfo(
    Guid UserId,
    string Email,
    string DisplayName);
