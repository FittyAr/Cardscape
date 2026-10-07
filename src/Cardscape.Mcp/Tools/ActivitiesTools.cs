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
    public async Task<ActivityPage> ListForBoardAsync(
        Guid boardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("boards_list_activities");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<ActivityPage>>(
                new ListBoardActivitiesQuery(boardId, cursor, limit), ct);
            var value = result.OrThrow();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "cards_list_activities")]
    public async Task<ActivityPage> ListForCardAsync(
        Guid cardId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_list_activities");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<ActivityPage>>(
                new ListCardActivitiesQuery(cardId, cursor, limit), ct);
            var value = result.OrThrow();
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
