using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Realtime;
using Cardscape.Application.Realtime;

namespace Cardscape.Api.Realtime;

/// <summary>
/// HTTP client for the MCP server's
/// <c>GET /api/internal/board-event/subscriptions</c>
/// snapshot endpoint. The MCP runs in a separate process;
/// the API exposes the snapshot to the Web UI's
/// <c>/admin/mcp-subscriptions</c> page through the
/// <c>GET /api/admin/mcp-subscriptions</c> admin endpoint.
/// Auth is the same shared secret the API uses for the
/// reverse direction (<c>Internal:Secret</c>).
/// </summary>
public sealed class McpSubscriptionsClient
{
    public const string SecretHeader = "X-Internal-Secret";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string? _secret;
    private readonly string? _baseUrl;
    private readonly ILogger<McpSubscriptionsClient> _logger;

    public McpSubscriptionsClient(
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<McpSubscriptionsClient> logger)
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

    /// <summary>
    /// Calls the MCP snapshot endpoint and returns the
    /// parsed response. Returns <c>null</c> if the MCP is
    /// unreachable, the secret is missing, or the response
    /// shape is unrecognised — the admin endpoint
    /// translates that to HTTP 503 with a structured error.
    /// </summary>
    public async Task<McpSubscriptionsSnapshot?> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            _logger.LogWarning(
                "Cardscape:Mcp:BaseUrl is not set on the API. " +
                "MCP subscriptions snapshot is unavailable.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_secret))
        {
            _logger.LogWarning(
                "Internal:Secret is not set on the API. " +
                "MCP subscriptions snapshot is unavailable.");
            return null;
        }

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri(new Uri(_baseUrl, UriKind.Absolute), "api/internal/board-event/subscriptions"));
            request.Headers.Add(SecretHeader, _secret);

            using HttpResponseMessage response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MCP subscriptions snapshot returned {Status} {Reason}",
                    (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<McpSubscriptionsSnapshot>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "MCP subscriptions snapshot threw");
            return null;
        }
    }
}

/// <summary>
/// Read-only snapshot of the MCP server's resource
/// subscription state. The shape mirrors the
/// <c>McpResourceBroadcasterSnapshot</c> on the MCP side
/// but is defined here so the API does not depend on the
/// MCP project (the Web UI consumes it from the API).
/// </summary>
public sealed record McpSubscriptionsSnapshot(
    Dictionary<string, IReadOnlyList<string>> Subscribers,
    IReadOnlyList<McpSubscriptionEventDto> Events,
    DateTimeOffset CapturedAt);

public sealed record McpSubscriptionEventDto(
    string EventKind,
    string Uri,
    string? SessionId,
    DateTimeOffset Timestamp,
    string Detail);
