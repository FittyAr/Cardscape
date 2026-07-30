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
    public async Task<CardVoteStateDto> ToggleVote(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_toggle_vote");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
            new ToggleCardVoteCommand(cardId), ct);
        return Ensure(result);
    }

    [McpServerTool(Name = "cards_get_votes")]
    public async Task<CardVoteStateDto> GetVotes(Guid cardId, CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("cards_get_votes");
        RequireAuth();
        var result = await bus.InvokeAsync<Result<CardVoteStateDto>>(
            new ListCardVotesQuery(cardId), ct);
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

