using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cardscape.Mcp.Realtime;

/// <summary>
/// HTTP implementation of <see cref="IBoardPushClient"/>. The
/// client targets the API's <c>/api/internal/broadcast</c>
/// endpoint with the matching <c>X-Internal-Secret</c> header
/// for service-to-service auth.
/// </summary>
public sealed class HttpBoardPushClient : IBoardPushClient
{
    public const string SecretHeader = "X-Internal-Secret";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string? _secret;
    private readonly ILogger<HttpBoardPushClient> _logger;

    public HttpBoardPushClient(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<HttpBoardPushClient> logger)
    {
        _http = factory.CreateClient("Cardscape.Api");
        _secret = config["Internal:Secret"]
            ?? config["Cardscape:Internal:Secret"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__INTERNAL__SECRET");
        _logger = logger;
    }

    public Task PushCardCreatedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync("CardCreated", boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardUpdatedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync("CardUpdated", boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardMovedAsync(CardMovedPayload payload, CancellationToken ct = default) =>
        PushAsync("CardMoved", boardId: payload.BoardId, listId: null, cardId: payload.CardId, payload, ct);

    public Task PushCardCompletedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync("CardCompleted", boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardReopenedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync("CardReopened", boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushListCreatedAsync(ListEventPayload payload, CancellationToken ct = default) =>
        PushAsync("ListCreated", boardId: payload.BoardId, listId: payload.ListId, cardId: null, payload, ct);

    public Task PushCommentAddedAsync(CommentEventPayload payload, CancellationToken ct = default) =>
        PushAsync("CommentAdded", boardId: payload.BoardId, listId: null, cardId: payload.CardId, payload, ct);

    private async Task PushAsync(
        string method,
        Guid? boardId,
        Guid? listId,
        Guid? cardId,
        object payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogWarning(
                "Cardscape:Internal:Secret is not set on the MCP server. " +
                "Realtime broadcasts from the MCP will be rejected by the API.");
            return;
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "api/internal/broadcast/");
            request.Headers.Add(SecretHeader, _secret);
            request.Content = JsonContent.Create(new
            {
                boardId,
                listId,
                cardId,
                method,
                payload
            }, options: JsonOptions);

            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MCP->API broadcast {Method} failed: {Status}",
                    method, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Broadcasting is best-effort; the MCP tool has already
            // succeeded in mutating the database, and the Web
            // client will pick up the new state on the next
            // refresh. We log and move on.
            _logger.LogWarning(ex, "MCP->API broadcast {Method} threw", method);
        }
    }
}
