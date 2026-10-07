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
public sealed partial class BoardsTools(
    IMessageBus bus,
    ICurrentUser currentUser,
    IBoardPushClient push,
    ICardRepository cards)
{
}
