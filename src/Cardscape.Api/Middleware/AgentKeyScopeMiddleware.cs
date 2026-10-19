using Cardscape.Api.Authentication;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Security;
using Cardscape.Mcp.Extensions;

namespace Cardscape.Api.Middleware;

/// <summary>
/// Keeps an AI agent key (an API token) on the REST surface within what its
/// owner granted it (ADR 0015):
/// <list type="bullet">
///   <item>account, security, administration and setup endpoints are never
///         open to an agent, so a key cannot mint a broader key, change its
///         owner's password or flip instance settings;</item>
///   <item>any other state-changing request needs the full <c>write</c>
///         scope. Keys with only fine-grained capabilities (e.g.
///         <c>cards:write</c>) are read-only on REST; those capabilities are
///         enforced per tool on the MCP endpoint, which this middleware skips.</item>
/// </list>
/// The administrator's "agents may change things" switch has already removed
/// every write scope from the principal when it is off.
/// </summary>
public sealed class AgentKeyScopeMiddleware(RequestDelegate next)
{
    public const string ForbiddenErrorCode = "auth.agent_scope_forbidden";

    private static readonly PathString[] ClosedToAgents =
    [
        "/api/security",
        "/api/admin",
        "/api/auth",
        "/api/setup",
        "/api/users/me/password",
        "/oauth",
        "/scim",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAgentRequest(context)
            || context.Request.Path.StartsWithSegments(McpEndpoint.DefaultPattern, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (ClosedToAgents.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await WriteForbiddenAsync(context, "AI agent keys cannot use account, security or administration endpoints.");
            return;
        }

        if (!IsReadOnlyMethod(context.Request.Method)
            && !ScopeExtensions.Grants(AgentIdentity.GrantedScopes(context.User), Scope.Write))
        {
            await WriteForbiddenAsync(context, "This AI agent key may not change data through the REST API.");
            return;
        }

        await next(context);
    }

    private static bool IsAgentRequest(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && string.Equals(
            context.User.Identity.AuthenticationType,
            ApiTokenAuthenticationHandler.SchemeName,
            StringComparison.Ordinal);

    private static bool IsReadOnlyMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    private static Task WriteForbiddenAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(
            new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                title = "Forbidden for this AI agent key",
                status = StatusCodes.Status403Forbidden,
                detail,
                code = ForbiddenErrorCode,
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
