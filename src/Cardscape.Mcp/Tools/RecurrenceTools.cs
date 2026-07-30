using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Recurrence;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

[McpServerToolType]
public sealed class RecurrenceTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "cards_get_recurrence")]
    public async Task<CardRecurrenceDto?> Get(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_get_recurrence");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardRecurrenceDto?>>(
                new GetCardRecurrenceQuery(cardId), ct);
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

    [McpServerTool(Name = "cards_set_recurrence")]
    public async Task<CardRecurrenceDto> Set(
        Guid cardId, int intervalDays, DateTimeOffset firstOccurrenceAt,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_set_recurrence");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result<CardRecurrenceDto>>(
                new SetCardRecurrenceCommand(cardId, intervalDays, firstOccurrenceAt), ct);
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

    [McpServerTool(Name = "cards_delete_recurrence")]
    public async Task<string> Delete(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_delete_recurrence");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            RequireAuth();
            var result = await bus.InvokeAsync<Result>(
                new DeleteCardRecurrenceCommand(cardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return "deleted";
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
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
