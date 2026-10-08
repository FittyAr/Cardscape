using System.Text.Json;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;
using Cardscape.Infrastructure.Configuration;
using Cardscape.Mcp.Logging;

namespace Cardscape.Mcp.Realtime;

/// <summary>
/// HTTP implementation of <see cref="IBoardPushClient"/>. The
/// client targets the API's <c>/api/internal/broadcast</c>
/// endpoint with the matching <c>X-Internal-Secret</c> header
/// for service-to-service auth.
/// </summary>
public sealed class HttpBoardPushClient(
    IHttpClientFactory factory,
    IConfiguration config,
    ILogger<HttpBoardPushClient> logger) : IBoardPushClient
{

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _http = factory.CreateClient("Cardscape.Api");
    private readonly string? _secret = config.OutboundInternalSecret;

    public Task PushCardCreatedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CardCreated), boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardUpdatedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CardUpdated), boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardMovedAsync(CardMovedPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CardMoved), boardId: payload.BoardId, listId: null, cardId: payload.CardId, payload, ct);

    public Task PushCardCompletedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CardCompleted), boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushCardReopenedAsync(CardEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CardReopened), boardId: payload.BoardId, listId: null, cardId: null, payload, ct);

    public Task PushListCreatedAsync(ListEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.ListCreated), boardId: payload.BoardId, listId: payload.ListId, cardId: null, payload, ct);

    public Task PushCommentAddedAsync(CommentEventPayload payload, CancellationToken ct = default) =>
        PushAsync(nameof(IBoardClient.CommentAdded), boardId: payload.BoardId, listId: null, cardId: payload.CardId, payload, ct);

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
            logger.InternalSecretMissing();
            return;
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, "api/internal/broadcast/");
            request.Headers.Add(InternalSecret.HeaderName, _secret);
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
                logger.ApiBroadcastUnsuccessful(method, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Broadcasting is best-effort; the MCP tool has already
            // succeeded in mutating the database, and the Web
            // client will pick up the new state on the next
            // refresh. We log and move on.
            logger.ApiBroadcastFailed(ex, method);
        }
    }
}
