using Cardscape.Application.Abstractions.Search;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Search;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// MCP tool surface for relational search. Delegates
/// to the same <see cref="SearchQuery"/> handler the REST
/// <c>GET /api/search</c> endpoint uses, so authorization,
/// the per-board read-access filter, and the 4 KB query cap
/// all stay in one place. The shape is <see cref="SearchPageDto"/>;
/// an AI client paginates by re-calling with
/// <c>page = previousPage + 1</c>.
///
/// BETA-8-MCP-#1 — see <c>test-results/r8/r8-report.md</c>.
/// </summary>
[McpServerToolType]
public sealed class SearchTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "cards_search")]
    public async Task<SearchPageDto> Search(
        string query,
        Guid? boardId = null,
        SearchHitKind? kind = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_search");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: boardId, cardId: null);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<SearchPageDto>>(
                new SearchQuery(query, boardId, kind, page, pageSize), ct);
            var value = Ensure(result);
            __mcpSpan.MarkSuccess();
            return value;
        }
        catch (Exception ex)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
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
