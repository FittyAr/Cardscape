using Cardscape.Web.Logging;

namespace Cardscape.Web.Services;

/// <summary>
/// DelegatingHandler that pulls the access token from <see cref="TokenStore"/>
/// and attaches it as a Bearer header on every outgoing API request.
/// Registered on the named "Cardscape.Api" HttpClient.
/// </summary>
public sealed class AuthTokenHandler(
    TokenStore tokens,
    AuthStateProvider stateProvider,
    PasswordChangeGate passwordChange,
    ILogger<AuthTokenHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never attach the token to anonymous auth endpoints — saves an
        // unnecessary localStorage read on /login and /register.
        if (request.RequestUri is null ||
            !request.RequestUri.AbsolutePath.Contains("/api/", StringComparison.OrdinalIgnoreCase) ||
            IsAnonymousAuthEndpoint(request.RequestUri.AbsolutePath))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string? accessToken = await tokens.GetAccessTokenAsync();
        bool sentToken = false;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            sentToken = true;
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
            && response.Headers.Contains(PasswordChangeGate.HeaderName))
        {
            passwordChange.Require();
        }

        // BETA-AUTH-401: when the API rejects the token (401), the
        // local token is stale or belongs to a deleted user. Clear it
        // and notify the auth state provider so the UI redirects to
        // /login instead of silently rendering an empty list.
        //
        // BETA-SW-002 — see test-results/beta/round-2/console-errors.md.
        // The previous handler logged a Warning for every 401,
        // including the expected ones that anonymous users get when
        // hitting `RequireAuthorization()`-protected endpoints. The
        // console spammed "API call to /api/... returned 401" even
        // when no token was sent. Only escalate the warning to the
        // log when we actually attached a Bearer (i.e. the server
        // rejected a real token — that is the case the user has to
        // act on). Anonymous 401s are still returned to the caller
        // verbatim; they just don't pollute the log.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            if (sentToken)
            {
                logger.AuthenticatedApiCallUnauthorized(request.RequestUri.AbsolutePath);
                await tokens.ClearAsync();
                stateProvider.Notify();
            }
            else
            {
                logger.AnonymousApiCallUnauthorized(request.RequestUri.AbsolutePath);
            }
        }

        return response;
    }

    // The sign-in endpoints run before there is a session. Other
    // /api/auth/* endpoints (me, verification, 2fa) need the token, so
    // only these are skipped.
    private static readonly string[] AnonymousAuthPaths =
    [
        "/api/auth/login", "/api/auth/register", "/api/auth/forgot-password",
        "/api/auth/reset-password", "/api/auth/verify-email", "/api/auth/config",
    ];

    private static bool IsAnonymousAuthEndpoint(string path) =>
        AnonymousAuthPaths.Any(anonymous => path.EndsWith(anonymous, StringComparison.OrdinalIgnoreCase)
            || path.Contains(anonymous + "/", StringComparison.OrdinalIgnoreCase));
}
