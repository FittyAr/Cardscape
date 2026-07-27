using System.Security.Claims;
using Cardscape.Domain.Members;

namespace Cardscape.Application.Abstractions.Security;

/// <summary>
/// Default <see cref="ICurrentUser"/> implementation. Reads the
/// id, email, display name, and roles from the
/// <see cref="ClaimsPrincipal"/> resolved by
/// <see cref="ICurrentUserAccessor"/>.
/// </summary>
public sealed class CurrentUser(ICurrentUserAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.GetCurrentPrincipal();

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public UserId? Id
    {
        get
        {
            var raw = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(raw, out var id) ? new UserId(id) : null;
        }
    }

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public string? DisplayName => Principal?.FindFirst("display_name")?.Value;

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
}
