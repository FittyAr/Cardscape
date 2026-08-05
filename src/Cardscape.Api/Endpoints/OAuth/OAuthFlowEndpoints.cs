using System.Text;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Cardscape.Api.Endpoints.OAuth;

/// <summary>
/// Standard OAuth 2.0 authorization-code-flow endpoints.
/// These are the URLs a third-party app calls; they live on
/// the public surface (no <c>RequireAuthorization</c> on the
/// protocol endpoints themselves; the
/// <c>/oauth/authorize</c> step enforces the user is
/// logged-in to Cardscape).
///
/// <list type="bullet">
///   <item><c>GET /oauth/authorize</c> — the user-facing
///         consent step. Must be authenticated; on success
///         redirects to the registered redirect URI with
///         <c>?code=...&amp;state=...</c>.</item>
///   <item><c>POST /oauth/token</c> — exchanges an
///         authorization code for a Bearer access token.</item>
///   <item><c>POST /oauth/revoke</c> — revokes an access
///         token (RFC 7009).</item>
///   <item><c>GET /oauth/userinfo</c> — returns the
///         authenticated user's projection (subject,
///         email, display name) for the access token's
///         scopes.</item>
/// </list>
/// </summary>
public static class OAuthFlowEndpoints
{
    public static IEndpointRouteBuilder MapOAuthFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/oauth").WithTags("OAuthFlow");

        // /oauth/authorize — issues a one-shot authorization
        // code. The user must already be logged into
        // Cardscape; if not, the middleware redirects them
        // to /login?returnUrl=/oauth/authorize?... via the
        // standard authentication challenge.
        group.MapGet("/authorize", async (
            [FromQuery] string? client_id,
            [FromQuery] string? redirect_uri,
            [FromQuery] string? scope,
            [FromQuery] string? state,
            HttpContext http,
            [FromServices] IOAuthAppService service,
            [FromServices] Cardscape.Application.Abstractions.Security.ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(client_id) || string.IsNullOrWhiteSpace(redirect_uri))
            {
                return Results.BadRequest(new
                {
                    error = "invalid_request",
                    error_description = "client_id and redirect_uri are required."
                });
            }

            // If the user is not authenticated, bounce to
            // the login page with the original request as
            // the returnUrl so the browser comes back here
            // after the user signs in.
            if (currentUser.Id is null)
            {
                string returnUrl = $"/oauth/authorize?client_id={Uri.EscapeDataString(client_id)}" +
                                   $"&redirect_uri={Uri.EscapeDataString(redirect_uri)}" +
                                   (string.IsNullOrEmpty(scope) ? string.Empty : $"&scope={Uri.EscapeDataString(scope)}") +
                                   (string.IsNullOrEmpty(state) ? string.Empty : $"&state={Uri.EscapeDataString(state)}");
                return Results.Challenge(
                    new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        RedirectUri = returnUrl
                    },
                    new[] { "Cardscape" });
            }

            var userId = new Domain.Members.UserId(currentUser.Id.Value);
            var scopes = string.IsNullOrWhiteSpace(scope)
                ? []
                : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // The minimal-API endpoint is sync-by-design;
            // awaiting the bus call avoids the
            // .GetAwaiter().GetResult() deadlock risk under
            // a sync context. A failure of the service (an
            // unregistered redirect_uri, a revoked app)
            // surfaces as an invalid_request so the IdP
            // surfaces a clean OAuth-style error to the user.
            OAuthAuthorizationCodeIssuance issuance;
            try
            {
                issuance = await service.IssueAuthorizationCodeAsync(
                    client_id,
                    userId,
                    redirect_uri,
                    scopes,
                    ct);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = ex.Message });
            }

            string separator = redirect_uri.Contains('?') ? "&" : "?";
            string fragment = $"code={Uri.EscapeDataString(issuance.Code)}" +
                              (string.IsNullOrEmpty(state) ? string.Empty : $"&state={Uri.EscapeDataString(state)}");
            return Results.Redirect($"{redirect_uri}{separator}{fragment}");
        });

        // /oauth/token — exchanges an authorization code
        // for a Bearer access token. The request body is
        // application/x-www-form-urlencoded per RFC 6749.
        group.MapPost("/token", async (
            HttpContext http,
            [FromServices] IOAuthAppService service,
            CancellationToken ct) =>
        {
            IFormCollection form = await http.Request.ReadFormAsync(ct);
            string grantType = form["grant_type"].ToString();
            string code = form["code"].ToString();
            string clientId = form["client_id"].ToString();
            string clientSecret = form["client_secret"].ToString();
            string redirectUri = form["redirect_uri"].ToString();

            if (!string.Equals(grantType, "authorization_code", StringComparison.Ordinal))
            {
                return Results.BadRequest(new
                {
                    error = "unsupported_grant_type",
                    error_description = "Only authorization_code is supported."
                });
            }

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret) ||
                string.IsNullOrWhiteSpace(redirectUri))
            {
                return Results.BadRequest(new
                {
                    error = "invalid_request",
                    error_description = "code, client_id, client_secret, and redirect_uri are required."
                });
            }

            var exchange = await service.ExchangeCodeAsync(clientId, clientSecret, code, redirectUri, ct);
            if (exchange.IsFailure)
            {
                return Results.Json(
                    new { error = "invalid_grant", error_description = exchange.Error.Message },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            return Results.Ok(new
            {
                access_token = exchange.Value.AccessToken,
                token_type = exchange.Value.TokenType,
                expires_in = exchange.Value.ExpiresInSeconds,
                scope = string.Join(' ', exchange.Value.Scopes),
                refresh_token = exchange.Value.RefreshToken
            });
        });

        // /oauth/revoke — RFC 7009 token revocation. The
        // client authenticates with the same client_id /
        // client_secret it used at /oauth/token; the server
        // refuses to revoke a token owned by a different
        // client. A 200 is returned for both known and
        // unknown tokens (and for an unknown client) so the
        // server does not leak which tokens existed.
        group.MapPost("/revoke", async (
            HttpContext http,
            [FromServices] IOAuthAppService service,
            CancellationToken ct) =>
        {
            IFormCollection form = await http.Request.ReadFormAsync(ct);
            string token = form["token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new
                {
                    error = "invalid_request",
                    error_description = "token is required."
                });
            }

            // RFC 7009 §2.1 lets the client authenticate via
            // either HTTP Basic or form-encoded client_id +
            // client_secret. Form params is what /oauth/token
            // uses; we honour the same shape here so existing
            // third-party SDKs do not need a separate code
            // path for revoke. Per RFC 6749 §2.3.1 the Basic
            // form is preferred for confidential clients, so
            // we also accept the Authorization: Basic header
            // when present.
            (string clientId, string clientSecret) = ExtractClientCredentials(http, form);
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return Results.Json(
                    new
                    {
                        error = "invalid_client",
                        error_description = "client_id and client_secret are required (form params or HTTP Basic auth)."
                    },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await service.RevokeAccessTokenAsync(token, clientId, clientSecret, ct);
            return result.IsSuccess
                ? Results.Ok()
                : Results.Json(
                    new { error = "invalid_client", error_description = result.Error.Message },
                    statusCode: StatusCodes.Status400BadRequest);
        });

        // /oauth/userinfo — returns the authenticated
        // user's projection. The access token is read from
        // the Authorization: Bearer header.
        group.MapGet("/userinfo", async (
            HttpContext http,
            [FromServices] IOAuthAppService service,
            CancellationToken ct) =>
        {
            string? bearer = ExtractBearer(http);
            if (string.IsNullOrWhiteSpace(bearer))
            {
                return Results.Json(
                    new { error = "invalid_token", error_description = "Missing bearer token." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var info = await service.GetUserInfoAsync(bearer, ct);
            if (info.IsFailure)
            {
                return Results.Json(
                    new { error = "invalid_token", error_description = info.Error.Message },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new
            {
                sub = info.Value.UserId,
                email = info.Value.Email,
                name = info.Value.DisplayName
            });
        });

        return app;
    }

    private static string? ExtractBearer(HttpContext http)
    {
        string? header = http.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header[7..].Trim();
    }

    /// <summary>
    /// Resolve the <c>client_id</c> + <c>client_secret</c>
    /// pair for a confidential-client call. RFC 7009 §2.1
    /// (and RFC 6749 §2.3.1) authorise both
    /// <c>Authorization: Basic</c> and form params; the form
    /// params win when both are present so the test suite can
    /// pin the credentials in the body without re-encoding
    /// the secret on every call.
    /// </summary>
    private static (string ClientId, string ClientSecret) ExtractClientCredentials(
        HttpContext http,
        IFormCollection form)
    {
        string formId = form["client_id"].ToString();
        string formSecret = form["client_secret"].ToString();
        if (!string.IsNullOrWhiteSpace(formId) && !string.IsNullOrWhiteSpace(formSecret))
        {
            return (formId, formSecret);
        }

        string? authHeader = http.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader)
            && authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string decoded = Encoding.UTF8.GetString(
                    Convert.FromBase64String(authHeader["Basic ".Length..].Trim()));
                int sep = decoded.IndexOf(':');
                if (sep > 0 && sep < decoded.Length - 1)
                {
                    return (decoded[..sep], decoded[(sep + 1)..]);
                }
            }
            catch (FormatException)
            {
                // Malformed Basic header — fall through to the
                // form-params branch and let the service-layer
                // validation surface a 401 with the canonical
                // error_description.
            }
        }

        return (formId, formSecret);
    }
}
