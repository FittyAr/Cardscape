namespace Cardscape.Mcp.Resources;

/// <summary>Strict parser for the resource URI templates advertised by Cardscape MCP.</summary>
public static class McpResourceUriParser
{
    public static Guid ParseWorkspaceId(Uri uri) => ParseAuthorityId(uri, "workspace", "workspaceId");

    public static Guid ParseBoardId(Uri uri) => ParseAuthorityId(uri, "board", "boardId");

    public static Guid ParseCardId(Uri uri) => ParseAuthorityId(uri, "card", "cardId");

    public static Guid ParseCardsBoardId(Uri uri) => ParsePathId(uri, "cards", "board", "boardId");

    public static Guid ParseListsBoardId(Uri uri) => ParsePathId(uri, "lists", "board", "boardId");

    private static Guid ParseAuthorityId(Uri uri, string expectedScheme, string parameterName)
    {
        ValidateCommon(uri, expectedScheme, parameterName);
        if (uri.AbsolutePath != "/")
        {
            throw Invalid(uri, parameterName);
        }

        return ParseGuid(uri, uri.Host, parameterName);
    }

    private static Guid ParsePathId(
        Uri uri,
        string expectedScheme,
        string expectedAuthority,
        string parameterName)
    {
        ValidateCommon(uri, expectedScheme, parameterName);
        if (!string.Equals(uri.Host, expectedAuthority, StringComparison.Ordinal))
        {
            throw Invalid(uri, parameterName);
        }

        string candidate = uri.AbsolutePath.Trim('/');
        if (candidate.Contains('/', StringComparison.Ordinal))
        {
            throw Invalid(uri, parameterName);
        }

        return ParseGuid(uri, candidate, parameterName);
    }

    private static void ValidateCommon(Uri uri, string expectedScheme, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri
            || !string.Equals(uri.Scheme, expectedScheme, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw Invalid(uri, parameterName);
        }
    }

    private static Guid ParseGuid(Uri uri, string candidate, string parameterName)
    {
        if (!Guid.TryParse(candidate, out Guid id) || id == Guid.Empty)
        {
            throw Invalid(uri, parameterName);
        }

        return id;
    }

    private static ArgumentException Invalid(Uri? uri, string parameterName) => new(
        $"URI '{uri}' does not match the resource contract or contain a valid {parameterName} GUID.",
        nameof(uri));
}
