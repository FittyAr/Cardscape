using System.Security.Claims;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Provides the current <see cref="ClaimsPrincipal"/> in a
/// transport-agnostic way. The API project registers an
/// implementation that reads from <c>HttpContext</c>; the MCP
/// server registers one that reads from its authentication
/// handler.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>The current principal, or <c>null</c> if anonymous.</summary>
    ClaimsPrincipal? GetCurrentPrincipal();
}
