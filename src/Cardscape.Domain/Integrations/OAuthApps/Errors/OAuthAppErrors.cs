using Cardscape.Domain.Common;

namespace Cardscape.Domain.Integrations.OAuthApps.Errors;


/// <summary>
/// Domain errors raised by the OAuth 3rd-party app aggregate
/// lifecycle (<see cref="OAuthApp"/>, <see cref="OAuthAuthorizationCode"/>,
/// <see cref="OAuthAccessToken"/>).
/// </summary>
public static class OAuthAppErrors
{
    public static readonly DomainError UnknownClient = DomainError.NotFound(
        "oauth.client_not_found",
        "No registered application was found for the supplied client id.");

    public static readonly DomainError InvalidClientSecret = DomainError.Unauthenticated(
        "oauth.invalid_client_secret",
        "The supplied client secret did not match the registered application's secret.");

    public static readonly DomainError AppRevoked = DomainError.Forbidden(
        "oauth.app_revoked",
        "The application has been revoked and may no longer be used.");

    public static readonly DomainError RedirectUriMismatch = DomainError.Validation(
        "oauth.redirect_uri_mismatch",
        "The supplied redirect URI does not match any of the application's registered redirect URIs.");

    public static readonly DomainError UnknownCode = DomainError.NotFound(
        "oauth.code_not_found",
        "The supplied authorization code does not exist or has already been consumed.");

    public static readonly DomainError CodeAlreadyConsumed = DomainError.Conflict(
        "oauth.code_already_consumed",
        "The authorization code has already been exchanged for an access token.");

    public static readonly DomainError CodeExpired = DomainError.Validation(
        "oauth.code_expired",
        "The authorization code has expired. Request a new one via /oauth/authorize.");

    public static readonly DomainError UnknownAccessToken = DomainError.NotFound(
        "oauth.token_not_found",
        "The supplied access token is unknown.");

    public static readonly DomainError AccessTokenRevoked = DomainError.Unauthenticated(
        "oauth.token_revoked",
        "The access token has been revoked.");

    public static readonly DomainError AccessTokenExpired = DomainError.Unauthenticated(
        "oauth.token_expired",
        "The access token has expired.");

    public static readonly DomainError ScopeNotGranted = DomainError.Forbidden(
        "oauth.scope_not_granted",
        "The supplied scope was not granted to the access token.");

    public static readonly DomainError InvalidScope = DomainError.Validation(
        "oauth.invalid_scope",
        "The supplied scope is not a recognised Cardscape OAuth scope.");
}
