using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Common;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Resources;
using ModelContextProtocol;

namespace Cardscape.Mcp.Authorization;

/// <summary>Validates and canonicalizes board resource subscriptions.</summary>
public static class McpBoardSubscriptionAuthorization
{
    public const string InvalidResourceErrorCode = "mcp.subscription.resource_invalid";
    public const string ForbiddenErrorCode = "mcp.subscription.forbidden";

    public static async Task<string> AuthorizeAsync(
        string? rawUri,
        Guid userId,
        IBoardRepository boards,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boards);
        if (userId == Guid.Empty)
        {
            throw new McpException($"{ForbiddenErrorCode}: An authenticated user is required.");
        }

        Guid boardId = ParseBoardId(rawUri);
        Result<Domain.Boards.Board> access = await MembershipGuards.EnsureCanReadBoardAsync(
            boards, userId, boardId, cancellationToken);
        if (access.IsFailure)
        {
            throw new McpException($"{ForbiddenErrorCode}: {access.Error.Message}");
        }

        return ToCanonicalUri(boardId);
    }

    public static Guid ParseBoardId(string? rawUri)
    {
        if (string.IsNullOrWhiteSpace(rawUri)
            || !Uri.TryCreate(rawUri, UriKind.Absolute, out Uri? uri))
        {
            throw new McpException(
                $"{InvalidResourceErrorCode}: Only board://{{boardId}} resources can be subscribed.");
        }

        try
        {
            return McpResourceUriParser.ParseBoardId(uri);
        }
        catch (ArgumentException exception)
        {
            throw new McpException(
                $"{InvalidResourceErrorCode}: Board resource URI must contain a non-empty GUID.",
                exception);
        }
    }

    public static string ToCanonicalUri(Guid boardId) => $"board://{boardId:N}";
}
