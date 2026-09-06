using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cardscape.Sdk;

/// <summary>
/// Configures the HTTP transport and JSON serialization used by <see cref="CardscapeClient"/>.
/// </summary>
public sealed class CardscapeClientOptions
{
    /// <summary>
    /// Gets or sets the absolute base address of the Cardscape API.
    /// </summary>
    public Uri BaseAddress { get; set; } = null!;

    /// <summary>
    /// Gets or sets the asynchronous access-token provider invoked immediately before each request.
    /// </summary>
    /// <remarks>
    /// A provider is used instead of a fixed token so callers can refresh credentials without recreating the client.
    /// Returning <see langword="null"/>, an empty string, or whitespace sends the request without an Authorization header.
    /// </remarks>
    public Func<Task<string?>>? AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the serializer options used for request and response JSON.
    /// </summary>
    public JsonSerializerOptions JsonOptions { get; set; } = DefaultJsonOptions;

    /// <summary>
    /// Gets or sets the maximum duration of an HTTP request.
    /// </summary>
    /// <value>The request timeout. The default is 30 seconds.</value>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Provides the default web-oriented JSON serializer options used by the SDK.
    /// </summary>
    /// <remarks>
    /// The defaults ignore null values when writing, match property names without regard to case, and serialize enums as
    /// camel-case strings. Create a copy before customizing these shared options.
    /// </remarks>
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
/// Provides typed resource clients and lower-level access to the Cardscape REST API.
/// </summary>
/// <remarks>
/// The resource clients cover the most frequently used endpoints. Use <see cref="SendAsync(HttpRequestMessage, CancellationToken)"/>
/// or <see cref="SendAsync{TResult}(HttpRequestMessage, CancellationToken)"/> for endpoints not exposed by a resource client.
/// </remarks>
public sealed class CardscapeClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly CardscapeClientOptions _options;
    private readonly bool _ownsHttp;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardscapeClient"/> class with an internally owned HTTP client.
    /// </summary>
    /// <param name="options">The transport and serialization configuration.</param>
    public CardscapeClient(CardscapeClientOptions options) : this(new HttpClient(), options, ownsHttp: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CardscapeClient"/> class with the specified HTTP client.
    /// </summary>
    /// <param name="http">The HTTP client used to send API requests.</param>
    /// <param name="options">The transport and serialization configuration.</param>
    /// <param name="ownsHttp"><see langword="true"/> to dispose <paramref name="http"/> with this client; otherwise, <see langword="false"/>.</param>
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

    /// <summary>Gets the workspace resource client.</summary>
    public WorkspacesClient Workspaces { get; private set; } = null!;

    /// <summary>Gets the board resource client.</summary>
    public BoardsClient Boards { get; private set; } = null!;

    /// <summary>Gets the list resource client.</summary>
    public ListsClient Lists { get; private set; } = null!;

    /// <summary>Gets the card resource client.</summary>
    public CardsClient Cards { get; private set; } = null!;

    /// <summary>Gets the label resource client.</summary>
    public LabelsClient Labels { get; private set; } = null!;

    /// <summary>Gets the comment resource client.</summary>
    public CommentsClient Comments { get; private set; } = null!;

    /// <summary>Gets the activity resource client.</summary>
    public ActivitiesClient Activities { get; private set; } = null!;

    /// <summary>Sends a request and returns the raw HTTP response.</summary>
    /// <param name="request">The request to send.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The response returned by the Cardscape API.</returns>
    /// <remarks>The caller owns and must dispose the returned response.</remarks>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
        => SendCoreAsync(request, ct);

    internal JsonContent CreateJsonContent<T>(T value) =>
        JsonContent.Create(value, options: _options.JsonOptions);

    /// <summary>Sends a request and deserializes its successful JSON response.</summary>
    /// <typeparam name="TResult">The response-body contract.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="ct">The token used to cancel the operation.</param>
    /// <returns>The deserialized response body.</returns>
    /// <exception cref="CardscapeApiException">The API returns a non-success status code or an empty successful response.</exception>
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

    /// <inheritdoc/>
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

/// <summary>Represents an unsuccessful or invalid response from the Cardscape API.</summary>
public sealed class CardscapeApiException : Exception
{
    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the HTTP status code associated with the response.</summary>
    public int StatusCode { get; }

    /// <summary>Gets the response body captured for diagnostics, when available.</summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CardscapeApiException"/> class.
    /// </summary>
    /// <param name="code">The stable machine-readable error code.</param>
    /// <param name="message">The human-readable error description.</param>
    /// <param name="statusCode">The HTTP status code associated with the response.</param>
    /// <param name="responseBody">The response body captured for diagnostics, when available.</param>
    public CardscapeApiException(string code, string message, int statusCode, string? responseBody = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
