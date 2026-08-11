using System.Security.Claims;
using Cardscape.Api.Extensions;
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
    public static IEndpointRouteBuilder MapExternalLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/external").WithTags("Auth");

        group.MapGet("/{provider}/start", async Task<IResult> (
            string provider,
            IAuthenticationSchemeProvider schemes,
            string? returnUrl) =>
        {
            if (!ExternalProviderExtensions.TryParse(provider, out var parsed))
            {
                return Results.Problem(
                    title: ExternalLoginErrors.UnknownProvider.Code,
                    detail: ExternalLoginErrors.UnknownProvider.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string scheme = parsed.WireName();
            if (await schemes.GetSchemeAsync(scheme) is null)
            {
                return Results.Problem(
                    title: ExternalLoginErrors.ProviderNotImplemented.Code,
                    detail: ExternalLoginErrors.ProviderNotImplemented.Message,
                    statusCode: StatusCodes.Status501NotImplemented);
            }

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/auth/external/{scheme}/callback",
                Items =
                {
                    ["cardscape.provider"] = scheme,
                    ["cardscape.returnUrl"] = NormalizeReturnUrl(returnUrl)
                }
            };
            return Results.Challenge(properties, new[] { scheme });
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

            var authenticateResult = await http.AuthenticateAsync(
                ServiceCollectionExtensions.ExternalCookieScheme);
            if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
            {
                return Results.Problem(
                    title: "auth.external.failed",
                    detail: "External provider did not return a valid principal.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            string expectedProvider = parsed.WireName();
            if (!IsExpectedProvider(authenticateResult.Properties, expectedProvider))
            {
                await http.SignOutAsync(ServiceCollectionExtensions.ExternalCookieScheme);
                return Results.Problem(
                    title: "auth.external.provider_mismatch",
                    detail: "External login provider did not match the requested callback.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            string returnUrl = authenticateResult.Properties.Items.TryGetValue(
                "cardscape.returnUrl", out string? storedReturnUrl)
                ? NormalizeReturnUrl(storedReturnUrl)
                : "/";

            // Consume the temporary principal before processing domain data so
            // even malformed or rejected callbacks cannot be replayed.
            await http.SignOutAsync(ServiceCollectionExtensions.ExternalCookieScheme);

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

            string redirect = configuration["Cardscape:Web:ExternalLoginRedirectUrl"]
                ?? configuration["Web:ExternalLoginRedirectUrl"]
                ?? "/oauth/callback";
            string fragment =
                $"access_token={Uri.EscapeDataString(auth.Value.AccessToken ?? string.Empty)}"
                + $"&user_id={Uri.EscapeDataString(auth.Value.User.Id.ToString())}"
                + $"&user_email={Uri.EscapeDataString(auth.Value.User.Email)}"
                + $"&user_name={Uri.EscapeDataString(auth.Value.User.DisplayName)}"
                + $"&return_url={Uri.EscapeDataString(returnUrl)}";
            return Results.Redirect($"{redirect}#{fragment}");
        });

        return app;
    }

    internal static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
            || returnUrl.Contains('\\'))
        {
            return "/";
        }

        return returnUrl;
    }

    internal static bool IsExpectedProvider(
        AuthenticationProperties properties,
        string expectedProvider) =>
        properties.Items.TryGetValue("cardscape.provider", out string? actualProvider)
        && string.Equals(actualProvider, expectedProvider, StringComparison.Ordinal);
}
