using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Realtime;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface that lets an AI assistant drive a Cardscape board.
/// All operations reuse Application-layer commands and queries.
/// </summary>
[McpServerToolType]
public sealed partial class BoardsTools
{
    private readonly IMessageBus bus;
    private readonly ICurrentUser currentUser;
    private readonly IBoardPushClient push;
    private readonly ICardRepository cards;

    public BoardsTools(
        IMessageBus bus,
        ICurrentUser currentUser,
        IBoardPushClient push,
        ICardRepository cards)
    {
        this.bus = bus;
        this.currentUser = currentUser;
        this.push = push;
        this.cards = cards;
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass the API token as an Authorization: Bearer header to the MCP HTTP endpoint.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }
}
