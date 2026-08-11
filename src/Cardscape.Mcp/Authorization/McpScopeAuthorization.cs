using System.Security.Claims;
using Cardscape.Domain.Security;
using ModelContextProtocol;

namespace Cardscape.Mcp.Authorization;

/// <summary>Enforces exact API-token scopes at the MCP transport boundary.</summary>
public static class McpScopeAuthorization
{
    public const string ScopeClaimType = "scope";
    public const string ForbiddenErrorCode = "mcp.scope.forbidden";

    public static void Authorize(Scope required, string operation, ClaimsPrincipal? principal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        string requiredValue = required.ToWire();
        bool granted = principal?.Identity?.IsAuthenticated == true
            && principal.FindAll(ScopeClaimType)
                .Any(claim => string.Equals(claim.Value, requiredValue, StringComparison.Ordinal));

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
