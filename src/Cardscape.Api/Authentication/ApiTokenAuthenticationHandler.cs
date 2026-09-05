using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Api.Logging;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Authentication handler for first-class API tokens on the REST
/// surface. The cleartext secret travels in the
/// <c>Authorization: Bearer</c> header; the handler hashes it with
/// the same SHA-256 used at issuance and delegates the
/// active/revoked/expiry checks to <see cref="IApiTokenService"/>.
///
/// On success the handler:
/// <list type="bullet">
///   <item>builds a <see cref="ClaimsPrincipal"/> whose
///         <c>Identity.AuthenticationType</c> is
///         <see cref="SchemeName"/> (so the rate-limit middleware
///         can tell API-token requests from human JWT
///         requests);</item>
///   <item>stashes the loaded <see cref="ApiToken"/> aggregate in
///         <c>HttpContext.Items["ApiToken"]</c> so the
///         rate-limit middleware can read its
///         <c>RateLimitPerHour</c>/<c>BurstSize</c> config without
///         re-querying the database.</item>
/// </list>
///
/// Selection between this scheme and the JWT bearer scheme is
/// driven by the <c>BearerPolicy</c> policy scheme: API tokens
/// are base64url (no dots), JWTs are three base64url segments
/// separated by dots. We dispatch on the secret's shape.
/// </summary>
public sealed class ApiTokenAuthenticationHandler
    : AuthenticationHandler<ApiTokenAuthenticationOptions>
{
    public const string SchemeName = "ApiToken";
    public const string HttpContextItemKey = "ApiToken";

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<ApiTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenService tokens,
        IApiTokenRepository repository)
        : base(options, logger, encoder)
    {
        Tokens = tokens;
        Repository = repository;
    }

    public IApiTokenService Tokens { get; }
    public IApiTokenRepository Repository { get; }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string raw = authHeader.ToString();
        if (string.IsNullOrWhiteSpace(raw)
            || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string secret = raw["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return AuthenticateResult.Fail("Empty bearer secret.");
        }

        Result<ApiTokenValidation> validation =
            await Tokens.ValidateAsync(secret, Context.RequestAborted);
        if (validation.IsFailure)
        {
            Logger.ApiTokenRejected(validation.Error.Code);
            return AuthenticateResult.Fail(validation.Error.Message);
        }

        ApiToken? aggregate = await Repository.GetByIdAsync(
            validation.Value.TokenId, Context.RequestAborted);
        if (aggregate is null)
        {
            return AuthenticateResult.Fail("API token aggregate not found.");
        }

        // The rate-limit middleware reads this slot to find the
        // token's configured limits.
        Context.Items[HttpContextItemKey] = aggregate;

        ClaimsIdentity identity = new(SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(
            ClaimTypes.NameIdentifier,
            validation.Value.UserId.Value.ToString()));
        identity.AddClaim(new Claim(
            "token_id",
            validation.Value.TokenId.Value.ToString()));
        foreach (string scope in validation.Value.Scopes)
        {
            identity.AddClaim(new Claim("scope", scope));
        }

        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}

public sealed class ApiTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}
