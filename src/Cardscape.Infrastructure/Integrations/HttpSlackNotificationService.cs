using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Integrations;
using Cardscape.Domain.Common;
using Cardscape.Domain.Integrations.Slack;
using Microsoft.Extensions.Configuration;

namespace Cardscape.Infrastructure.Integrations;

/// <summary>
/// Default <see cref="ISlackNotificationService"/> that POSTs to
/// <c>https://slack.com/api/chat.postMessage</c> with a bot token
/// sourced from configuration (<c>Integrations:Slack:BotToken</c>).
///
/// <para>The endpoint is the well-documented Slack Web API method;
/// the body shape (<c>channel</c>, <c>text</c>) is documented at
/// <see href="https://api.slack.com/methods/chat.postMessage"/>.
/// A 2xx HTTP response with <c>{"ok": true}</c> in the body is
/// treated as success; any other shape (HTTP error or
/// <c>{"ok": false, "error": "..."}</c>) is surfaced as a domain
/// <see cref="ErrorType.External"/> failure.</para>
/// </summary>
public sealed class HttpSlackNotificationService : ISlackNotificationService
{
    private const string SlackApiBase = "https://slack.com/api/";
    private const string ChatPostMessagePath = "chat.postMessage";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string? _botToken;

    public HttpSlackNotificationService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _botToken = configuration["Integrations:Slack:BotToken"];
        if (!string.IsNullOrWhiteSpace(_botToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _botToken);
        }
    }

    public async Task<Result> SendAsync(
        SlackWorkspace workspace,
        string channelId,
        string message,
        CancellationToken ct = default)
    {
        if (workspace is null)
        {
            return Result.Failure(DomainError.External(
                "slack.workspace_required", "Slack workspace is required."));
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return Result.Failure(DomainError.External(
                "slack.channel_id_required", "Slack channel id is required."));
        }

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            return Result.Failure(DomainError.External(
                "slack.bot_token_missing",
                "Slack bot token is not configured (Integrations:Slack:BotToken)."));
        }

        SlackPostMessageResponse? response;
        try
        {
            using HttpResponseMessage http = await _http.PostAsJsonAsync(
                SlackApiBase + ChatPostMessagePath,
                new { channel = channelId, text = message },
                JsonOptions,
                ct);
            response = await http.Content.ReadFromJsonAsync<SlackPostMessageResponse>(
                JsonOptions, ct);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure(DomainError.External(
                "slack.transport_error",
                $"Slack API call failed: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            return Result.Failure(DomainError.External(
                "slack.response_invalid",
                $"Slack API returned an unparseable response: {ex.Message}"));
        }

        if (response is null)
        {
            return Result.Failure(DomainError.External(
                "slack.response_empty", "Slack API returned an empty response."));
        }

        if (!response.Ok)
        {
            string err = string.IsNullOrWhiteSpace(response.Error)
                ? "unknown_error"
                : response.Error;
            return Result.Failure(DomainError.External(
                "slack.api_error", $"Slack API error: {err}"));
        }

        return Result.Success();
    }

    private sealed record SlackPostMessageResponse(bool Ok, string? Error);
}
