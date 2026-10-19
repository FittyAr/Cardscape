using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Api.Logging;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Domain.Common;
using Cardscape.Domain.Security;
using Cardscape.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
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
public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<ApiTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiTokenService tokens,
    IApiTokenRepository repository,
    ISystemSettingsService settings)
        : AuthenticationHandler<ApiTokenAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";
    public const string HttpContextItemKey = "ApiToken";

    public IApiTokenService Tokens { get; } = tokens;
    public IApiTokenRepository Repository { get; } = repository;

    public ISystemSettingsService Settings { get; } = settings;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? secret = BearerAuthorization.Credential(Request.Headers.Authorization.ToString());
        if (secret is null)
        {
            return AuthenticateResult.NoResult();
        }

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

        // The principal is the key's owner, marked as acting through the
        // agent; the administrator's switch can make every key read-only.
        bool writesAllowed = (await Settings.GetAsync(Context.RequestAborted)).Agents.AllowWrites;
        ClaimsPrincipal principal = AgentIdentity.CreatePrincipal(SchemeName, validation.Value, writesAllowed);
        AuthenticationTicket ticket = new(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}

public sealed class ApiTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}
