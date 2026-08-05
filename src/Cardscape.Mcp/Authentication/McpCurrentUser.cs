using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Adapts the API-token principal to the Application layer's
/// <see cref="ICurrentUser"/> abstraction. The MCP server is
/// stateless across tool calls, so the resolver is registered as
/// a scoped service that reaches into the per-request
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

    /// <summary>Roles granted to the current user. For MCP API-token
    /// principals this is empty; the meaningful access vector is
    /// <see cref="Scopes"/>.</summary>
    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public string? FindFirst(string claimType) =>
        Principal?.FindFirstValue(claimType);

    /// <summary>Scopes granted to the current user by the API
    /// token. Empty for non-API-token principals.</summary>
    public IReadOnlyCollection<string> Scopes =>
        Principal?.FindAll("scope").Select(c => c.Value).ToList() ?? [];
}
