using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Authentication handler for the MCP server. v0.2 accepts the same
/// JWT bearer tokens the REST API issues, so AI clients that can
/// already <c>POST /api/auth/login</c> can drive the MCP surface
/// without a parallel secret store. The eventual
/// <c>ApiToken</c>-based auth is in flight (see ADR 0002); this
/// handler will move to a first-class <c>ApiToken</c> scheme once
/// the entity is built.
/// </summary>
public sealed class JwtBearerAuthenticationHandler
    : AuthenticationHandler<JwtBearerAuthenticationOptions>
{
    public const string SchemeName = "Bearer";

    public JwtBearerAuthenticationHandler(
        IOptionsMonitor<JwtBearerAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string raw = authHeader.ToString();
        if (string.IsNullOrWhiteSpace(raw)
            || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "Authorization header must be 'Bearer <token>'."));
        }

        string token = raw["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty bearer token."));
        }

        JwtBearerAuthenticationOptions options = Options;
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "MCP server has no Jwt:SigningKey configured."));
        }

        try
        {
            Microsoft.IdentityModel.Tokens.TokenValidationParameters parameters =
                new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? "Cardscape" : options.Issuer,
                    ValidAudience = string.IsNullOrWhiteSpace(options.Audience) ? "Cardscape" : options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

            // ValidateToken throws on failure; on success it returns
            // a ClaimsPrincipal we can attach to the ticket. We
            // re-decode the JWT once to get the raw claims (so the
            // principal carries the original sub/email/name/role
            // values without the ClaimsIdentity default claim-type
            // remapping applied by the validator).
            _ = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out _);

            System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt =
                new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

            ClaimsIdentity identity = new(jwt.Claims, SchemeName, ClaimTypes.Name, ClaimTypes.Role);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Rejected MCP bearer token.");
            return Task.FromResult(AuthenticateResult.Fail(ex.Message));
        }
    }
}

public sealed class JwtBearerAuthenticationOptions : AuthenticationSchemeOptions
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? SigningKey { get; set; }
}
