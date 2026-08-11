using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cardscape.Sdk;

/// <summary>
/// Configuration for <see cref="CardscapeClient"/>. <see cref="AccessToken"/>
/// is a function (not a value) so the caller can rotate the token
/// without rebuilding the client. <see cref="JsonOptions"/> lets
/// callers plug in their own converters (custom date formats,
/// snake_case ↔ camelCase policy, etc.) — defaults to
/// <see cref="JsonSerializerDefaults.Web"/>.
/// </summary>
public sealed class CardscapeClientOptions
{
    public Uri BaseAddress { get; set; } = null!;
    public Func<Task<string?>>? AccessToken { get; set; }
    public JsonSerializerOptions JsonOptions { get; set; } = DefaultJsonOptions;
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };
}

/// <summary>
/// Typed surface over the Cardscape REST API. Sub-clients
/// (<see cref="Workspaces"/>, <see cref="Boards"/>, <see cref="Lists"/>,
/// <see cref="Cards"/>, <see cref="Labels"/>, <see cref="Comments"/>,
/// <see cref="Activities"/>) cover the 30 most-used endpoints. The
/// rest of the surface stays reachable through
/// <see cref="SendAsync"/> and <see cref="SendAsync{TResult}"/>.
/// </summary>
public sealed class CardscapeClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly CardscapeClientOptions _options;
    private readonly bool _ownsHttp;

    public CardscapeClient(CardscapeClientOptions options) : this(new HttpClient(), options, ownsHttp: true)
    {
    }

    public CardscapeClient(HttpClient http, CardscapeClientOptions options, bool ownsHttp = false)
    {
        _http = http;
        _options = options;
        _ownsHttp = ownsHttp;

        _http.BaseAddress ??= options.BaseAddress;
        _http.Timeout = options.HttpTimeout;

        // The sub-clients fan out per-resource; each one routes
        // through the parent (this) for the actual transport.
        // The property is declared `null!` so the field can be
        // initialised here without a circular constructor.
        Workspaces = new WorkspacesClient(this);
        Boards = new BoardsClient(this);
        Lists = new ListsClient(this);
        Cards = new CardsClient(this);
        Labels = new LabelsClient(this);
        Comments = new CommentsClient(this);
        Activities = new ActivitiesClient(this);
    }

    public WorkspacesClient Workspaces { get; private set; } = null!;
    public BoardsClient Boards { get; private set; } = null!;
    public ListsClient Lists { get; private set; } = null!;
    public CardsClient Cards { get; private set; } = null!;
    public LabelsClient Labels { get; private set; } = null!;
    public CommentsClient Comments { get; private set; } = null!;
    public ActivitiesClient Activities { get; private set; } = null!;

    /// <summary>Lower-level: send a request and return the raw
    /// <see cref="HttpResponseMessage"/>. Use for endpoints the
    /// typed sub-clients don't cover.</summary>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
        => SendCoreAsync(request, ct);

    internal JsonContent CreateJsonContent<T>(T value) =>
        JsonContent.Create(value, options: _options.JsonOptions);

    /// <summary>Lower-level: send a request and deserialize the
    /// JSON body to <typeparamref name="TResult"/>. Throws
    /// <see cref="CardscapeApiException"/> on non-2xx.</summary>
    public async Task<TResult> SendAsync<TResult>(HttpRequestMessage request, CancellationToken ct = default)
    {
        HttpResponseMessage response = await SendCoreAsync(request, ct);
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                // Buffer the body once so we can both surface it
                // on the exception and free the response stream.
                string? errorBody = response.Content is null
                    ? null
                    : await ReadContentAsStringAsync(response.Content, ct);
                throw new CardscapeApiException(
                    code: "cardscape.http_error",
                    message: $"Cardscape API returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                    statusCode: (int)response.StatusCode,
                    responseBody: errorBody);
            }

            TResult? payload = await response.Content.ReadFromJsonAsync<TResult>(_options.JsonOptions, ct);
            return payload ?? throw new CardscapeApiException(
                "cardscape.empty_response",
                "The server returned a 2xx with an empty body.",
                (int)response.StatusCode);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_options.AccessToken is { } tokenProvider)
        {
            string? token = await tokenProvider();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new("Bearer", token);
            }
        }

        return await _http.SendAsync(request, ct);
    }

    /// <summary>
    /// netstandard2.0 does not expose
    /// <c>HttpContent.ReadAsStringAsync(CancellationToken)</c>;
    /// the multi-target SDK bridges to the net8.0+ overload
    /// via a single helper so the call site is uniform.
    /// </summary>
    private static Task<string> ReadContentAsStringAsync(HttpContent content, CancellationToken ct)
    {
#if NETSTANDARD2_0
        return content.ReadAsStringAsync();
#else
        return content.ReadAsStringAsync(ct);
#endif
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
#if NETSTANDARD2_0
        return new ValueTask(Task.CompletedTask);
#else
        return ValueTask.CompletedTask;
#endif
    }
}

/// <summary>Raised when the Cardscape API returns a non-2xx status
/// code. The body, when present, is exposed as <see cref="ResponseBody"/>.</summary>
public sealed class CardscapeApiException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public CardscapeApiException(string code, string message, int statusCode, string? responseBody = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
