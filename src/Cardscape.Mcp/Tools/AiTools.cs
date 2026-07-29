using Cardscape.Application.Ai;
using Cardscape.Domain.Common;
using ModelContextProtocol.Server;
using Wolverine;

namespace Cardscape.Mcp.Tools;

/// <summary>
/// AI-powered MCP tools. Exposed to AI clients through the
/// Model Context Protocol when the AI provider is configured
/// (either <c>RuleBased</c> or <c>OpenAiCompatible</c>).
/// </summary>
[McpServerToolType]
public sealed class AiTools
{
    [McpServerTool(Name = "ai_generate_card_description")]
    public async Task<AiFeatures.AiGeneratedText> GenerateCardDescription(
        Guid cardId,
        string? extraContext = null,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedText>>(
            new AiFeatures.GenerateCardDescriptionCommand(cardId, extraContext), ct);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    [McpServerTool(Name = "ai_summarize_thread")]
    public async Task<AiFeatures.AiGeneratedText> SummarizeThread(
        Guid[] commentIds,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedText>>(
            new AiFeatures.SummarizeCommentThreadCommand(commentIds), ct);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    [McpServerTool(Name = "ai_make_checklist")]
    public async Task<AiFeatures.AiGeneratedChecklist> MakeChecklist(
        Guid cardId,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        var result = await bus.InvokeAsync<Result<AiFeatures.AiGeneratedChecklist>>(
            new AiFeatures.GenerateChecklistFromDescriptionCommand(cardId), ct);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }

    [McpServerTool(Name = "ai_suggest_owners")]
    public async Task<AiFeatures.AiOwnerSuggestions> SuggestOwners(
        Guid cardId,
        int maxSuggestions = 3,
        CancellationToken ct = default)
    {
        var bus = McpToolContext.Bus;
        var result = await bus.InvokeAsync<Result<AiFeatures.AiOwnerSuggestions>>(
            new AiFeatures.SuggestCardOwnersCommand(cardId, maxSuggestions), ct);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(result.Error.Message);
    }
}
