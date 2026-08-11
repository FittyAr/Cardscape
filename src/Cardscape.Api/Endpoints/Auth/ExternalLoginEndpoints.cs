using System.Security.Claims;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.ExternalLogins;
using Cardscape.Domain.Authentication.ExternalLogins.Errors;
using Cardscape.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Wolverine;

namespace Cardscape.Api.Endpoints.Auth;

/// <summary>
/// REST endpoints for the OAuth 2.0 / OIDC external login
/// flow. The two endpoints are:
/// <list type="bullet">
///   <item><c>GET /api/auth/external/{provider}/start</c> —
///         kicks the OAuth challenge. The browser is
///         redirected to the provider's consent screen; on
///         consent the provider redirects back to
///         <c>/api/auth/external/{provider}/callback</c>.</item>
///   <item><c>GET /api/auth/external/{provider}/callback</c>
///         — handles the redirect, resolves the external
///         identity to a Cardscape user, mints a JWT, and
///         redirects the browser back to the Web client
///         (the configured
///         <c>Cardscape:Web:ExternalLoginRedirectUrl</c>)
///         with the token in a fragment so the SPA can pick
///         it up.</item>
/// </list>
/// Apple is wired end-to-end via the
/// <c>AppleClientSecretGenerator</c> (ES256-signed JWT
/// regenerated per request) — the OIDC handler is only
/// registered when the full <c>Authentication:Apple:*</c>
/// configuration block is present; otherwise the
/// <see cref="ExternalProviderExtensions.IsKnown"/>
/// check on the <c>apple</c> provider keeps the start
/// endpoint out of the menu (returns 501).
/// </summary>
public static class ExternalLoginEndpoints
{
    /// <summary>State cookie name used to round-trip the
    /// <c>returnUrl</c> through the OAuth flow.</summary>
    public const string StateCookieName = "cardscape.ext.state";

    public static IEndpointRouteBuilder MapExternalLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/external").WithTags("Auth");

        group.MapGet("/{provider}/start", (
            string provider,
            HttpContext http,
            IConfiguration configuration,
            string? returnUrl) =>
        {
            if (!ExternalProviderExtensions.TryParse(provider, out var parsed))
            {
                return Results.Problem(
                    title: ExternalLoginErrors.UnknownProvider.Code,
                    detail: ExternalLoginErrors.UnknownProvider.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // BETA-2-#8 — see test-results/BETA-TEST-REPORT.md.
            //
            // The old `IsImplemented()` hard-coded `true`
            // for Google / Microsoft / Apple. The
            // challenge call below would then throw
            // `InvalidOperationException: No authentication
            // handler is registered for the scheme
            // 'google'` when the operator hadn't supplied
            // the matching Authentication:Google:* keys, and
            // the GlobalExceptionMiddleware mapped it to 500.
            // The check now reads the live IConfiguration
            // and verifies the matching
            // Authentication:{Provider}:* keys are
            // populated — exactly the precondition the
            // scheme registration in ServiceCollectionExtensions
            // uses to decide whether to call
            // `AddGoogle()` / `AddMicrosoftAccount()` /
            // `AddOpenIdConnect()`. If the keys are missing,
            // the auth scheme is not registered, and the
            // 501 keeps the user on the right side of the
            // 500 cliff.
            if (!IsSchemeRegistered(configuration, parsed))
            {
                return Results.Problem(
                    title: ExternalLoginErrors.ProviderNotImplemented.Code,
                    detail: ExternalLoginErrors.ProviderNotImplemented.Message,
                    statusCode: StatusCodes.Status501NotImplemented);
            }

            // Round-trip the returnUrl through the OAuth
            // state parameter / cookie so the callback can
            // hand the user back to the page they came from.
            var state = Guid.NewGuid().ToString("N");
            http.Response.Cookies.Append(StateCookieName, Uri.EscapeDataString(returnUrl ?? "/"),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(10)
                });
            http.Response.Cookies.Append("cardscape.ext.state.id", state,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = http.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(10)
                });

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/auth/external/{parsed.WireName()}/callback",
                Items = { ["state"] = state }
            };
            return Results.Challenge(properties, new[] { parsed.WireName() });
        });

        group.MapGet("/{provider}/callback", async (
            string provider,
            HttpContext http,
            IMessageBus bus,
            ITokenService tokens,
            IClock clock,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!ExternalProviderExtensions.TryParse(provider, out var parsed))
            {
                return Results.Problem(
                    title: ExternalLoginErrors.UnknownProvider.Code,
                    detail: ExternalLoginErrors.UnknownProvider.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Pull the claims from the external auth
            // handler. The middleware has already run
            // AuthenticateAsync on the request so the
            // principal is available.
            var authenticateResult = await http.AuthenticateAsync(parsed.WireName());
            if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            {
                return Results.Problem(
                    title: "auth.external.failed",
                    detail: "External provider did not return a valid principal.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var principal = authenticateResult.Principal;
            string? subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Problem(
                    title: ExternalLoginErrors.SubjectMissing.Code,
                    detail: ExternalLoginErrors.SubjectMissing.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var subjectResult = SubjectId.Create(subject);
            if (subjectResult.IsFailure)
            {
                return Results.Problem(
                    title: subjectResult.Error.Code,
                    detail: subjectResult.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string? email = principal.FindFirstValue(ClaimTypes.Email);
            string? displayName = principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue("name");

            var auth = await bus.InvokeAsync<Result<AuthResponse>>(
                new ResolveExternalLoginCommand(parsed, subjectResult.Value, email, displayName),
                ct);
            if (auth.IsFailure)
            {
                return Results.Problem(
                    title: auth.Error.Code,
                    detail: auth.Error.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Drop the state cookies and hand the user
            // back to the SPA via a redirect fragment.
            http.Response.Cookies.Delete(StateCookieName);
            http.Response.Cookies.Delete("cardscape.ext.state.id");

            string redirect = configuration["Cardscape:Web:ExternalLoginRedirectUrl"]
                ?? configuration["Web:ExternalLoginRedirectUrl"]
                ?? "/oauth/callback";
            string fragment =
                $"access_token={Uri.EscapeDataString(auth.Value.AccessToken ?? string.Empty)}"
                + $"&user_id={Uri.EscapeDataString(auth.Value.User.Id.ToString())}"
                + $"&user_email={Uri.EscapeDataString(auth.Value.User.Email)}"
                + $"&user_name={Uri.EscapeDataString(auth.Value.User.DisplayName)}";
            return Results.Redirect($"{redirect}#{fragment}");
        });

        return app;
    }

    /// <summary>
    /// Returns <c>true</c> when the matching authentication
    /// scheme is actually registered for this provider. The
    /// registration in <c>ServiceCollectionExtensions</c> only
    /// calls <c>AddGoogle()</c> / <c>AddMicrosoftAccount()</c>
    /// / <c>AddOpenIdConnect()</c> when the corresponding
    /// <c>Authentication:{Provider}:*</c> configuration keys
    /// are present, so the same keys are the source of truth
    /// here. Mirrors the conditions in
    /// <c>AddApiAuthentication</c> verbatim — see BETA-2-#8
    /// in test-results/BETA-TEST-REPORT.md.
    /// </summary>
    private static bool IsSchemeRegistered(IConfiguration configuration, ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google =>
            !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]),
        ExternalProvider.Microsoft =>
            !string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Microsoft:ClientSecret"]),
        ExternalProvider.Apple =>
            !string.IsNullOrWhiteSpace(configuration["Authentication:Apple:ClientId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Apple:TeamId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Apple:KeyId"])
            && !string.IsNullOrWhiteSpace(configuration["Authentication:Apple:PrivateKeyPem"]),
        _ => false
    };
}
