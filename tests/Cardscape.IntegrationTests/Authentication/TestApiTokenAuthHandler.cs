using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Cardscape.IntegrationTests.Authentication;

/// <summary>
/// Test-only authentication scheme that mirrors the production
/// <c>ApiTokenAuthenticationHandler</c>: it validates a bearer
/// secret, sets the principal, and crucially, attaches the
/// loaded <see cref="ApiToken"/> aggregate to
/// <c>HttpContext.Items["ApiToken"]</c> so the rate-limit
/// middleware can find it.
///
/// Only registered when the test factory opts in via
/// <see cref="AuthenticationExtensions.AddTestApiTokenAuth"/>.
/// </summary>
public sealed class TestApiTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiToken";

    public TestApiTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out StringValues authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string raw = authHeader.ToString();
        if (string.IsNullOrWhiteSpace(raw)
            || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string secret = raw[7..].Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return AuthenticateResult.Fail("Empty bearer secret.");
        }

        IApiTokenService tokens = Context.RequestServices.GetRequiredService<IApiTokenService>();
        IApiTokenRepository repository = Context.RequestServices.GetRequiredService<IApiTokenRepository>();

        Result<ApiTokenValidation> validation = await tokens.ValidateAsync(secret, Context.RequestAborted);
        if (validation.IsFailure)
        {
            return AuthenticateResult.Fail(validation.Error.Message);
        }

        ApiToken? aggregate = await repository.GetByIdAsync(validation.Value.TokenId, Context.RequestAborted);
        if (aggregate is null)
        {
            return AuthenticateResult.Fail("Token aggregate not found.");
        }

        Context.Items["ApiToken"] = aggregate;

        ClaimsIdentity identity = new(SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, validation.Value.UserId.Value.ToString()));
        identity.AddClaim(new Claim("token_id", validation.Value.TokenId.Value.ToString()));
        foreach (string scope in validation.Value.Scopes)
        {
            identity.AddClaim(new Claim("scope", scope));
        }

        ClaimsPrincipal principal = new(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

internal static class AuthenticationExtensions
{
    /// <summary>Registers the test API-token auth scheme on the
    /// given <see cref="AuthenticationBuilder"/>.</summary>
    public static AuthenticationBuilder AddTestApiTokenAuth(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, TestApiTokenAuthHandler>(
            TestApiTokenAuthHandler.SchemeName,
            _ => { });
    }
}
