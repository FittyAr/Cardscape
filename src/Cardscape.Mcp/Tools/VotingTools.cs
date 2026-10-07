using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Voting;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

[McpServerToolType]
public sealed class VotingTools(IMessageBus bus, ICurrentUser currentUser)
{
    [McpServerTool(Name = "cards_toggle_vote")]
    public async Task<CardVoteStateDto> ToggleVoteAsync(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_toggle_vote");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
                new ToggleCardVoteCommand(cardId), ct);
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

    [McpServerTool(Name = "cards_get_votes")]
    public async Task<CardVoteStateDto> GetVotesAsync(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_get_votes");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            currentUser.RequireAuthenticated();
            var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
                new ListCardVotesQuery(cardId), ct);
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
