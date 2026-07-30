using Cardscape.Application.Abstractions.Security;
using Cardscape.Application.Ai;
using Cardscape.Domain.Common;
using Cardscape.Mcp.Observability;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// AI-powered MCP tools. Exposed to AI clients through the
/// Model Context Protocol when the AI provider is configured
/// (either <c>RuleBased</c> or <c>OpenAiCompatible</c>).
/// </summary>
[McpServerToolType]
public sealed class AiTools(ICurrentUser currentUser)
{
    [McpServerTool(Name = "ai_generate_card_description")]
    public async Task<AiFeatures.AiGeneratedText> GenerateCardDescription(
        Guid cardId,
        string? extraContext = null,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("ai_generate_card_description");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            var bus = McpToolContext.Bus;
            var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedText>>(
                new AiFeatures.GenerateCardDescriptionCommand(cardId, extraContext), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return result.Value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "ai_summarize_thread")]
    public async Task<AiFeatures.AiGeneratedText> SummarizeThread(
        Guid[] commentIds,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("ai_summarize_thread");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: null);
        try
        {
            var bus = McpToolContext.Bus;
            var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedText>>(
                new AiFeatures.SummarizeCommentThreadCommand(commentIds), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return result.Value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "ai_make_checklist")]
    public async Task<AiFeatures.AiGeneratedChecklist> MakeChecklist(
        Guid cardId,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("ai_make_checklist");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            var bus = McpToolContext.Bus;
            var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedChecklist>>(
                new AiFeatures.GenerateChecklistFromDescriptionCommand(cardId), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return result.Value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }

    [McpServerTool(Name = "ai_suggest_owners")]
    public async Task<AiFeatures.AiOwnerSuggestions> SuggestOwners(
        Guid cardId,
        int maxSuggestions = 3,
        CancellationToken ct = default)
    {
        using var __mcpSpan = McpToolSpan.Begin("ai_suggest_owners");
        __mcpSpan.SetContext(userId: currentUser.Id?.Value.ToString(), boardId: null, cardId: cardId);
        try
        {
            var bus = McpToolContext.Bus;
            var result = await bus.InvokeAsync<Result<AiFeatures.AiOwnerSuggestions>>(
                new AiFeatures.SuggestCardOwnersCommand(cardId, maxSuggestions), ct);
            if (result.IsFailure)
            {
                __mcpSpan.MarkFailure(result.Error.Code, result.Error.Message);
                throw new InvalidOperationException(result.Error.Message);
            }
            __mcpSpan.MarkSuccess();
            return result.Value;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            __mcpSpan.MarkFailure(ex.GetType().Name, ex.Message);
            throw;
        }
    }
}
