using System.Net.Http.Json;
using System.Text.Json;

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
public sealed class HttpMcpResourceNotifier
{
    public const string SecretHeader = "X-Internal-Secret";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string? _secret;
    private readonly string? _baseUrl;
    private readonly ILogger<HttpMcpResourceNotifier> _logger;

    public HttpMcpResourceNotifier(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<HttpMcpResourceNotifier> logger)
    {
        _http = factory.CreateClient("Cardscape.Mcp");
        _secret = config["Internal:Secret"]
            ?? config["Cardscape:Internal:Secret"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__INTERNAL__SECRET");
        _baseUrl = config["Cardscape:Mcp:BaseUrl"]
            ?? config["Mcp:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("CARDS_CAPE__MCP__BASEURL");
        _logger = logger;
    }

    public async Task NotifyAsync(Guid boardId, CancellationToken ct = default)
    {
        if (boardId == Guid.Empty)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            _logger.LogDebug(
                "Cardscape:Mcp:BaseUrl is not set on the API. " +
                "MCP resource subscription fan-out is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogDebug(
                "Internal:Secret is not set on the API. " +
                "MCP resource subscription fan-out is disabled.");
            return;
        }

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                new Uri(new Uri(_baseUrl, UriKind.Absolute), "api/internal/board-event/"));
            request.Headers.Add(SecretHeader, _secret);
            request.Content = JsonContent.Create(new { boardId }, options: JsonOptions);

            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "API->MCP board-event for {BoardId} returned {Status}",
                    boardId, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: the board change is durable; the
            // SignalR push already fired; AI clients can
            // re-poll the resource on the next refresh.
            _logger.LogDebug(
                ex,
                "API->MCP board-event for {BoardId} threw",
                boardId);
        }
    }
}
