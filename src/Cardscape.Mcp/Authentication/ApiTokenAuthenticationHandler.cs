using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Infrastructure.Security;
using Cardscape.Mcp.Logging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Authentication handler for the standalone MCP host. It accepts the
/// agent keys (API tokens) minted via <see cref="IApiTokenService"/>: the
/// cleartext secret travels in the <c>Authorization: Bearer</c> header, the
/// handler hashes it with the same SHA-256 used at issuance, and the
/// <see cref="IApiTokenService"/> decides whether the key is active. The
/// resulting principal is the key's owner, marked as acting through the
/// agent (see <see cref="AgentIdentity.CreatePrincipal"/>). When the API hosts
/// MCP in-process it uses its own API-token handler instead.
/// </summary>
public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<ApiTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiTokenService tokens,
    ISystemSettingsService settings)
        : AuthenticationHandler<ApiTokenAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";

    public IApiTokenService Tokens { get; } = tokens;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? raw = Request.Headers.TryGetValue("Authorization", out var authHeader)
            ? authHeader.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return AuthenticateResult.NoResult();
        }

        string? secret = BearerAuthorization.Credential(raw);
        if (secret is null)
        {
            return AuthenticateResult.Fail(
                "Authorization header must be 'Bearer <secret>'.");
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            return AuthenticateResult.Fail("Empty bearer secret.");
        }

        var validation = await Tokens.ValidateAsync(secret, Context.RequestAborted);
        if (validation.IsFailure)
        {
            Logger.ApiTokenRejected(validation.Error.Code);
            return AuthenticateResult.Fail(validation.Error.Message);
        }

        bool writesAllowed = (await settings.GetAsync(Context.RequestAborted)).Agents.AllowWrites;
        ClaimsPrincipal principal = AgentIdentity.CreatePrincipal(SchemeName, validation.Value, writesAllowed);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

public sealed class ApiTokenAuthenticationOptions : AuthenticationSchemeOptions
{
}
