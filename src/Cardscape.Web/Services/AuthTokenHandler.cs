namespace Cardscape.Web.Services;

/// <summary>
/// DelegatingHandler that pulls the access token from <see cref="TokenStore"/>
/// and attaches it as a Bearer header on every outgoing API request.
/// Registered on the named "Cardscape.Api" HttpClient.
/// </summary>
public sealed class AuthTokenHandler(
    TokenStore tokens,
    AuthStateProvider stateProvider,
    ILogger<AuthTokenHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never attach the token to anonymous auth endpoints — saves an
        // unnecessary localStorage read on /login and /register.
        if (request.RequestUri is null ||
            !request.RequestUri.AbsolutePath.Contains("/api/", StringComparison.OrdinalIgnoreCase) ||
            request.RequestUri.AbsolutePath.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        string? accessToken = await tokens.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        // BETA-AUTH-401: when the API rejects the token (401), the
        // local token is stale or belongs to a deleted user. Clear it
        // and notify the auth state provider so the UI redirects to
        // /login instead of silently rendering an empty list.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("API call to {Path} returned 401; clearing stored token and notifying auth state.", request.RequestUri.AbsolutePath);
            await tokens.ClearAsync();
            stateProvider.Notify();
        }

        return response;
    }
}
