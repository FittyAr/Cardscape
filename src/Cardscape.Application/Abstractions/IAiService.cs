using Cardscape.Domain.Common;

namespace Cardscape.Application.Abstractions;

/// <summary>
/// Abstraction over the AI provider that powers Cardscape's
/// generative features (card description generation, comment
/// summary, auto-checklist, smart owner suggestions).
///
/// The interface is intentionally narrow: every method takes
/// a prompt-shaped input and returns a result-shaped output
/// wrapped in <see cref="Result{T}"/>. The application layer
/// does not know whether the implementation talks to a
/// local rule-based engine, an OpenAI-compatible endpoint, or
/// a future provider. The choice is made at DI registration
/// time from <c>Ai:Provider</c> configuration.
///
/// See <c>docs/roadmap/03-execution-plan-v1.1.0.md</c> §4.7
/// for the rationale and the provider list.
/// </summary>
public interface IAiService
{
    /// <summary>Generates a text completion from a single prompt.</summary>
    Task<Result<AiTextCompletion>> CompleteAsync(AiPrompt prompt, AiOptions options, CancellationToken ct = default);

    /// <summary>Generates a chat completion from a list of messages.</summary>
    Task<Result<AiChatCompletion>> ChatAsync(IReadOnlyList<AiMessage> messages, AiOptions options, CancellationToken ct = default);

    /// <summary>Generates an embedding for semantic search / similarity.</summary>
    Task<Result<AiEmbedding>> EmbedAsync(string input, CancellationToken ct = default);
}

/// <summary>A single prompt to send to the AI provider.</summary>
public sealed record AiPrompt(string System, string User);

/// <summary>Per-call options: temperature, max tokens, model override.</summary>
public sealed record AiOptions(
    double Temperature = 0.2,
    int MaxTokens = 1024,
    string? ModelOverride = null);

/// <summary>One turn in a chat conversation.</summary>
public sealed record AiMessage(string Role, string Content);

/// <summary>The AI provider's reply to a single-prompt completion.</summary>
public sealed record AiTextCompletion(string Text, string? Model, int? PromptTokens, int? CompletionTokens);

/// <summary>The AI provider's reply to a multi-turn chat.</summary>
public sealed record AiChatCompletion(string Text, string? Model, int? PromptTokens, int? CompletionTokens);

/// <summary>A vector embedding of an input string.</summary>
public sealed record AiEmbedding(IReadOnlyList<float> Vector, string? Model);
