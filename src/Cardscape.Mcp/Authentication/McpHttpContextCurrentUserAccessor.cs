using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Bridges the per-request <see cref="HttpContext"/>'s
/// <see cref="ClaimsPrincipal"/> to the Application layer's
/// <see cref="ICurrentUserAccessor"/> abstraction. The MCP server
/// does not own the
/// <c>Cardscape.Api.Authentication.HttpContextCurrentUserAccessor</c>
/// (that type lives in the API project to avoid a cross-project
/// reference), so this is the MCP-side equivalent. Without it, the
/// DI graph validation at startup throws
/// "Unable to resolve service for type ICurrentUserAccessor" — see
/// BUG-A9-001 in <c>test-results/beta/round-2/reports/A9-mcp.md</c>.
/// </summary>
public sealed class McpHttpContextCurrentUserAccessor(IHttpContextAccessor accessor) : ICurrentUserAccessor
{
    public ClaimsPrincipal? GetCurrentPrincipal() => accessor.HttpContext?.User;
}
