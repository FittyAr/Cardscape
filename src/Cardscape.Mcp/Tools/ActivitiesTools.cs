using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Activities.Queries;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for the activity timeline. The two list tools
/// return <see cref="ActivityPage"/> so an AI client can paginate
/// by passing the <c>nextCursor</c> back in <c>cursor</c>.
/// </summary>
[McpServerToolType]
public sealed class ActivitiesTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "boards_list_activities")]
    public async Task<ActivityPage> ListForBoard(
        Guid boardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_list_activities");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<ActivityPage>>(
            new ListBoardActivitiesQuery(boardId, cursor, limit), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_list_activities")]
    public async Task<ActivityPage> ListForCard(
        Guid cardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_list_activities");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<ActivityPage>>(
            new ListCardActivitiesQuery(cardId, cursor, limit), ct);
        return Ensure(result);
    }

    private void RequireAuth()
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthorizedAccessException(
                "MCP tool call rejected: no authenticated principal. "
                + "Pass a Bearer JWT or API token in the Authorization header.");
        }
    }

    private static T Ensure<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value!;
    }
}

