using System.Security.Claims;
using Cardscape.Application.Abstractions.Security;

namespace Cardscape.Api.Authentication;

/// <summary>
/// Bridges the current <see cref="HttpContext"/>'s
/// <see cref="ClaimsPrincipal"/> to the Application layer's
/// <see cref="ICurrentUserAccessor"/> abstraction.
/// </summary>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor accessor) : ICurrentUserAccessor
{
    public ClaimsPrincipal? GetCurrentPrincipal() => accessor.HttpContext?.User;
}
