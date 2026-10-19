using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Mcp.Authentication;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Bridges the current <see cref="HttpContext"/>'s
/// <see cref="ClaimsPrincipal"/> to the Application layer's
/// <see cref="ICurrentUserAccessor"/> abstraction. Inside a call to the
/// in-process MCP server the protocol's own principal wins: MCP handlers can
/// run in a scope that is distinct from the HTTP request that carried them.
/// </summary>
public sealed class HttpContextCurrentUserAccessor(
    IHttpContextAccessor accessor,
    McpRequestCurrentUserAccessor mcp) : ICurrentUserAccessor
{
    public ClaimsPrincipal? GetCurrentPrincipal() => mcp.GetCurrentPrincipal() ?? accessor.HttpContext?.User;
}
