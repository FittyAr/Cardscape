using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Recurrence;
using Cardscape.Domain.Common;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

[McpServerToolType]
public sealed class RecurrenceTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "cards_get_recurrence")]
    public async Task<CardRecurrenceDto?> Get(Guid cardId, CancellationToken ct = default)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardRecurrenceDto?>>(
            new GetCardRecurrenceQuery(cardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_set_recurrence")]
    public async Task<CardRecurrenceDto> Set(
        Guid cardId, int intervalDays, DateTimeOffset firstOccurrenceAt,
        CancellationToken ct = default)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardRecurrenceDto>>(
            new SetCardRecurrenceCommand(cardId, intervalDays, firstOccurrenceAt), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_delete_recurrence")]
    public async Task<string> Delete(Guid cardId, CancellationToken ct = default)
    {
        RequireAuth();
        var result = await bus.InvokeAsync<Result>(
            new DeleteCardRecurrenceCommand(cardId), ct);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        return "deleted";
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
