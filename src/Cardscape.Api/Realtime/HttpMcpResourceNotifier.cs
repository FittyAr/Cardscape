using System.Text.Json;
using Cardscape.Api.Logging;
using Cardscape.Infrastructure.Configuration;

namespace Cardscape.Api.Realtime;

/// <summary>
/// Best-effort HTTP notifier that targets the MCP's
/// <c>POST /api/internal/board-event</c>
/// endpoint with the same shared secret the MCP uses when
/// it calls the API's <c>/api/internal/broadcast</c> webhook.
/// Best-effort: a transient network failure (MCP restart,
/// timeout, 503) logs and returns; it never aborts the
/// SignalR fan-out. The board change is durable in the
/// database — AI clients can re-fetch the resource on
/// their next poll if they miss the push.
/// </summary>
public sealed class HttpMcpResourceNotifier(
    IHttpClientFactory factory,
    IConfiguration config,
    ILogger<HttpMcpResourceNotifier> logger)
{

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _http = factory.CreateClient("Cardscape.Mcp");
    private readonly string? _secret = config.OutboundInternalSecret;
    private readonly string? _baseUrl = config["Cardscape:Mcp:BaseUrl"]
            ?? config["Mcp:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__MCP__BASEURL");

    public async Task NotifyAsync(Guid boardId, CancellationToken ct = default)
    {
        if (boardId == Guid.Empty)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            logger.McpResourceBaseUrlMissing();
            return;
        }

        if (string.IsNullOrWhiteSpace(_secret))
        {
            logger.McpResourceSecretMissing();
            return;
        }

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                new Uri(new Uri(_baseUrl, UriKind.Absolute), "api/internal/board-event/"));
            request.Headers.Add(InternalSecret.HeaderName, _secret);
            request.Content = JsonContent.Create(new { boardId }, options: JsonOptions);

            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"API-to-MCP board-event returned HTTP {(int)response.StatusCode}.");
            }
        }
        catch (Exception ex)
        {
            logger.McpBoardEventNotificationFailed(ex, boardId);
            throw;
        }
    }
}
