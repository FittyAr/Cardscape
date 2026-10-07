namespace Cardscape.Infrastructure.Security;

/// <summary>Parsing of <c>Authorization: Bearer &lt;credential&gt;</c> headers.</summary>
public static class BearerAuthorization
{
    private const string Prefix = "Bearer ";

    /// <summary>
    /// The trimmed credential after the case-insensitive <c>Bearer </c>
    /// prefix (possibly empty), or <c>null</c> when the header is missing or
    /// uses another scheme.
    /// </summary>
    public static string? Credential(string? authorizationHeader) =>
        authorizationHeader is not null && authorizationHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[Prefix.Length..].Trim()
            : null;
}
