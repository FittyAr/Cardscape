using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Cardscape.Mcp.Authentication;

/// <summary>
/// Default <see cref="ICurrentUserResolver"/> implementation.
/// Reads the user id and token id from the authenticated principal
/// set by <see cref="ApiTokenAuthenticationHandler"/>.
/// </summary>
public sealed class McpCurrentUserResolver : ICurrentUserResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public McpCurrentUserResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId =>
        Guid.Parse(_httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "No authenticated user. The MCP server requires an API token."));

    public Guid TokenId =>
        Guid.Parse(_httpContextAccessor.HttpContext?.User
            .FindFirstValue("token_id")
            ?? throw new InvalidOperationException(
                "The authenticated principal does not carry a token_id claim."));

    public IReadOnlyCollection<string> Scopes =>
        _httpContextAccessor.HttpContext?.User
            .FindAll("scope")
            .Select(c => c.Value)
            .ToList() ?? [];
}
