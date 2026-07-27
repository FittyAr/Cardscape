using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Adapts the JWT principal to the Application layer's
/// <see cref="ICurrentUser"/> abstraction. The MCP server is
/// stateless across tool calls, so the resolver is registered as
/// a singleton that reaches into the per-request
/// <see cref="IHttpContextAccessor"/>.
/// </summary>
public sealed class McpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public UserId? Id
    {
        get
        {
            string? sub = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? new UserId(id) : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public string? DisplayName => Principal?.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}
