using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cardscape.Infrastructure.Ai;

/// <summary>
/// AI provider that talks to any OpenAI-compatible chat
/// completions endpoint: OpenAI, Azure OpenAI, Ollama, vLLM,
/// LM Studio, etc. Configured from the
/// <c>Ai:Endpoint</c>, <c>Ai:ApiKey</c>, and <c>Ai:Model</c>
/// configuration values.
///
/// The request body follows the OpenAI
/// <c>/v1/chat/completions</c> shape; the response is parsed
/// the same way.
///
/// No NuGet dependency on an OpenAI SDK: <see cref="HttpClient"/>
/// + <see cref="JsonSerializer"/> are enough. The point of
/// "BYOK" is that the self-hoster owns the API key and the
/// endpoint URL — Cardscape never proxies through a third party.
/// </summary>
public sealed class OpenAiCompatibleAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly AiProviderOptions _options;
    private readonly ILogger<OpenAiCompatibleAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public OpenAiCompatibleAiService(HttpClient http, IOptions<AiProviderOptions> options, ILogger<OpenAiCompatibleAiService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<Result<AiTextCompletion>> CompleteAsync(AiPrompt prompt, AiOptions options, CancellationToken ct = default)
    {
        try
        {
            ChatCompletionsRequest request = new(
                Model: options.ModelOverride ?? _options.Model,
                Messages:
                [
                    new ChatMessage("system", prompt.System),
                    new ChatMessage("user", prompt.User)
                ],
                Temperature: options.Temperature,
                MaxTokens: options.MaxTokens);

            using HttpResponseMessage response = await _http.PostAsJsonAsync("v1/chat/completions", request, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("OpenAI-compatible provider returned {Status}: {Body}", (int)response.StatusCode, body);
                return Result<AiTextCompletion>.Failure(new DomainError(
                    ErrorType.External,
                    "ai.provider_error",
                    $"Provider returned HTTP {(int)response.StatusCode}."));
            }

            ChatCompletionsResponse? parsed = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(JsonOptions, ct);
            if (parsed is null || parsed.Choices.Count == 0)
            {
                return Result<AiTextCompletion>.Failure(new DomainError(
                    ErrorType.External,
                    "ai.empty_response",
                    "Provider returned an empty completion."));
            }

            string text = parsed.Choices[0].Message?.Content ?? string.Empty;
            return Result<AiTextCompletion>.Success(new AiTextCompletion(
                text, parsed.Model, parsed.Usage?.PromptTokens, parsed.Usage?.CompletionTokens));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OpenAI-compatible provider call failed.");
            return Result<AiTextCompletion>.Failure(new DomainError(
                ErrorType.External,
                "ai.network_error",
                ex.Message));
        }
    }

    // ── Wire types (OpenAI v1 /chat/completions) ─────

    private sealed record ChatCompletionsRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature, int MaxTokens);
    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionsResponse(string Id, string Model, IReadOnlyList<ChatChoice> Choices, ChatUsage? Usage);
    private sealed record ChatChoice(int Index, ChatMessage? Message, string? FinishReason);
    private sealed record ChatUsage(int? PromptTokens, int? CompletionTokens, int? TotalTokens);

}

/// <summary>Configuration for the AI provider. Bound from <c>Ai:*</c> keys.</summary>
public sealed class AiProviderOptions
{
    /// <summary>Provider name. Today: <c>RuleBased</c>, <c>OpenAiCompatible</c>. Default: <c>RuleBased</c>.</summary>
    public string Provider { get; set; } = "RuleBased";

    /// <summary>Base URL of the OpenAI-compatible endpoint. Required for <c>OpenAiCompatible</c>.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Bearer token for the OpenAI-compatible endpoint. Optional for local models like Ollama.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Default model name. Provider-specific (e.g. <c>gpt-4o-mini</c>, <c>llama3</c>).</summary>
    public string Model { get; set; } = "gpt-4o-mini";
}
