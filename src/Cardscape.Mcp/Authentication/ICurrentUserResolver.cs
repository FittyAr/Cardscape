namespace Cardscape.Mcp.Authentication;

/// <summary>
/// The MCP server's view of the current user. Resolved from the
/// authenticated API-token principal set by
/// <see cref="ApiTokenAuthenticationHandler"/>.
/// </summary>
public interface ICurrentUserResolver
{
    /// <summary>The id of the user that owns the API token.</summary>
    Guid UserId { get; }

    /// <summary>The id of the API token used to authenticate the
    /// current request.</summary>
    Guid TokenId { get; }

    /// <summary>The scopes granted to the API token.</summary>
    IReadOnlyCollection<string> Scopes { get; }
}
