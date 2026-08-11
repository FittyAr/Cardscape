using System.Security.Claims;
using System.Text.Encodings.Web;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Api.Authentication;

/// <summary>Options bag for <see cref="ScimAuthenticationHandler"/>.</summary>
public sealed class ScimAuthenticationOptions : AuthenticationSchemeOptions { }

/// <summary>
/// SCIM v2 bearer-token authentication. Looks up the presented
/// token in the <c>scim_tokens</c> table; on match, the principal
/// is marked authenticated and the resolved workspace id is
/// stashed on <c>HttpContext.Items["scim.workspaceId"]</c> for
/// <c>ScimEndpoints</c> to read.
/// </summary>
public sealed class ScimAuthenticationHandler(
    IOptionsMonitor<ScimAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IScimTokenRepository tokens,
    IUnitOfWork unitOfWork,
    IClock clock) : AuthenticationHandler<ScimAuthenticationOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Scim";
    public const string WorkspaceIdItemKey = "scim.workspaceId";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        string header = authHeader.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.Fail("Empty bearer token.");
        }

        var scimToken = await tokens.FindByPlaintextAsync(token, Context.RequestAborted);
        if (scimToken is null)
        {
            return AuthenticateResult.Fail("Invalid SCIM bearer token.");
        }

        Context.Items[WorkspaceIdItemKey] = scimToken.WorkspaceId.Value;
        scimToken.RecordUse(clock.UtcNow);

        // The RecordUse call mutates the aggregate in memory
        // (sets LastUsedAt) but EF Core never sees the change
        // unless the surrounding unit of work calls
        // SaveChanges. The auth handler historically relied
        // on the per-request ScimEndpoint to flush changes
        // implicitly — it does not, because the read-only
        // ListUsersAsync / ListGroupsAsync paths do not
        // trigger a SaveChanges, so the LastUsedAt column
        // stayed at its initial value forever. The admin
        // "this token was last used N days ago" widget was
        // therefore permanently stale and a leaked token's
        // "last touched" timestamp was useless for
        // forensics. Persist here so every successful
        // request — read or write — bumps the timestamp.
        try
        {
            await unitOfWork.SaveChangesAsync(Context.RequestAborted);
        }
        catch (Exception ex)
        {
            // A failure to persist the last-used timestamp
            // must not fail the auth — the operator's audit
            // trail is best-effort, the IdP's request
            // is not. Log loudly so a flaky DB is visible.
            Logger.LogWarning(
                ex,
                "Failed to persist SCIM token LastUsedAt for token prefix {Prefix}.",
                scimToken.TokenPrefix);
        }

        Claim[] claims = [
            new(ClaimTypes.NameIdentifier, scimToken.Id.Value.ToString()),
            new("scim.workspace_id", scimToken.WorkspaceId.Value.ToString()),
            new("scim.token_prefix", scimToken.TokenPrefix)
        ];
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
