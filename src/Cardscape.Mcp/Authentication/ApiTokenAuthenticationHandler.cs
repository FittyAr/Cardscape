using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Authentication handler for the MCP server. v0.3 accepts the
/// long-lived API tokens minted via <see cref="IApiTokenService"/>.
/// The cleartext secret travels in the <c>Authorization: Bearer</c>
/// header, the handler hashes it with the same SHA-256 used at
/// issuance, and the <see cref="IApiTokenService"/> decides whether
/// the token is active. A successful validation produces a
/// <see cref="ClaimsPrincipal"/> with the user id (as
/// <see cref="ClaimTypes.NameIdentifier"/>), the token id (as
/// <c>"token_id"</c>), and one <c>"scope"</c> claim per granted
/// scope.
/// </summary>
public sealed class ApiTokenAuthenticationHandler
    : AuthenticationHandler<ApiTokenAuthenticationOptions>
{
    public const string SchemeName = "ApiToken";

    public ApiTokenAuthenticationHandler(
        IOptionsMonitor<ApiTokenAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiTokenService tokens)
        : base(options, logger, encoder)
    {
        Tokens = tokens;
    }

    public IApiTokenService Tokens { get; }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // BETA-8-MCP-#6 — see test-results/r8/r8-report.md and
        // docs/mcp/claude-desktop.md. The MCP transport is stdio,
        // so a desktop AI client (Claude Desktop, Cursor, …) has
        // no HTTP request to put the token on. We fall back to
        // the `Cardscape__ApiToken` env var the client sets in
        // `claude_desktop_config.json`, then to the
        // `CARDS_API_TOKEN` shorthand. Either path produces the
        // same `Bearer <secret>` value the rest of the handler
        // expects.
        string? raw = null;
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            raw = authHeader.ToString();
        }
        else
        {
            string? fromEnv = Request.HttpContext?.RequestServices
                    .GetService<IConfiguration>()?["Cardscape:ApiToken"]
                ?? Environment.GetEnvironmentVariable("CARDS_API_TOKEN")
                ?? Environment.GetEnvironmentVariable("Cardscape__ApiToken");
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                raw = "Bearer " + fromEnv;
            }
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return AuthenticateResult.NoResult();
        }

        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail(
                "Authorization header must be 'Bearer <secret>'.");
        }

        string secret = raw["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return AuthenticateResult.Fail("Empty bearer secret.");
        }

        var validation = await Tokens.ValidateAsync(secret, Context.RequestAborted);
        if (validation.IsFailure)
        {
            Logger.LogInformation(
                "Rejected MCP API token: {ErrorCode}", validation.Error.Code);
            return AuthenticateResult.Fail(validation.Error.Message);
        }

        ClaimsIdentity identity = new(SchemeName, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier,
                                   validation.Value.UserId.Value.ToString()));
        identity.AddClaim(new Claim("token_id",
                                   validation.Value.TokenId.Value.ToString()));
        foreach (var scope in validation.Value.Scopes)
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
