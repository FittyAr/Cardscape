using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Security;
using ModelContextProtocol;

namespace Cardscape.Mcp.Authorization;

/// <summary>
/// Enforces agent-key scopes at the MCP transport boundary: the exact scope,
/// or the legacy <c>write</c> scope for any write capability.
/// </summary>
public static class McpScopeAuthorization
{
    public const string ScopeClaimType = AgentIdentity.ScopeClaim;
    public const string ForbiddenErrorCode = "mcp.scope.forbidden";

    public static void Authorize(Scope required, string operation, ClaimsPrincipal? principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        string requiredValue = required.ToWire();
        bool granted = ScopeExtensions.Grants(AgentIdentity.GrantedScopes(principal), required);

        if (!granted)
        {
            throw new McpException(
                $"{ForbiddenErrorCode}: MCP operation '{operation}' requires the '{requiredValue}' scope.");
        }
    }

    public static ValueTask<TResult> AuthorizeAndInvokeAsync<TResult>(
        Scope required,
        string operation,
        ClaimsPrincipal? principal,
        Func<ValueTask<TResult>> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        Authorize(required, operation, principal);
        return next();
    }
}
