using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Carries the principal attached by the MCP transport into the
/// Application current-user abstraction. MCP handlers can run in a request
/// scope that is distinct from ASP.NET Core's original HTTP scope, so the
/// protocol's <c>RequestContext.User</c> is the authoritative identity source.
/// <see cref="AsyncLocal{T}"/> preserves it across the SDK's nested scopes
/// while isolating concurrent asynchronous request flows.
/// </summary>
public sealed class McpRequestCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly AsyncLocal<ClaimsPrincipal?> _principal = new();

    public ClaimsPrincipal? GetCurrentPrincipal() => _principal.Value;

    public void SetCurrentPrincipal(ClaimsPrincipal? principal) => _principal.Value = principal;
}
