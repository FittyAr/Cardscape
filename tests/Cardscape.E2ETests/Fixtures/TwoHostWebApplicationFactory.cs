using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cardscape.E2ETests.Fixtures;

/// <summary>
/// In-process test host that boots BOTH the API and
/// the MCP server in the same test process and wires
/// the cross-process broadcaster path. The E2E coverage
/// of this contract lives here because no single-host
/// test can exercise the cross-process call.
/// <para>
/// Both hosts run as <see cref="WebApplicationFactory{TEntryPoint}"/>
/// TestServer instances, which means the request pipeline
/// runs in-process but the network transport is the
/// TestServer's in-memory bus. A real
/// <c>HttpClient.PostAsync("http://127.0.0.1:15999/...")</c>
/// from the API host would fail with "actively refused"
/// because no real socket is bound on that port.
/// </para>
/// <para>
/// To exercise the cross-process path we install a
/// <see cref="DelegatingHandler"/> on the API's named
/// HttpClient that dispatches every outbound request
/// into the MCP's in-memory test server. The result
/// is the same execution path the production code
/// takes on a real network — the only difference is
/// the transport.
/// </para>
/// </summary>
public sealed class TwoHostWebApplicationFactory : IAsyncLifetime
{
    public const string SharedSecret = "e2e-internal-key-32+characters-long";
    public const int ApiPort = 15998;
    public const int McpPort = 15999;

    public ApiHostFactory Api { get; } = new();
    public McpHostFactory Mcp { get; } = new();

    private RecordingSink? _apiRecordingSink;

    /// <summary>
    /// The API's cross-process HTTP call recording sink.
    /// The recording handler in the API factory writes
    /// every request and response into this sink so the
    /// E2E test can assert that the broadcaster actually
    /// hit the MCP (and not just observe a missing event
    /// on the MCP side).
    /// </summary>
    public RecordingSink RecordingSink => _apiRecordingSink ??= Api.Services.GetRequiredService<RecordingSink>();

    public ValueTask InitializeAsync()
    {
        // Boot the API first so the MCP can target its
        // /api/internal/board-event endpoint with a real
        // port. The fixed port means the API's
        // HttpMcpResourceNotifier is configured with the
        // MCP URL before the API host is built.
        Api.Initialize(ApiPort);
        Mcp.ApiBaseAddress = $"http://127.0.0.1:{ApiPort}";
        Mcp.Initialize(McpPort);

        // The recording handler needs the sink; the
        // dispatcher needs the MCP client. Wire them
        // through the API host's services before the
        // notifier resolves its HttpClient.
        RecordingSink sink = Api.Services.GetRequiredService<RecordingSink>();
        McpServerDispatcher dispatcher = Api.Services.GetRequiredService<McpServerDispatcher>();
        HttpClient mcpClient = Mcp.CreateClient();
        dispatcher.Configure(sink, mcpClient);

        _apiRecordingSink = sink;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Mcp.Dispose();
        Api.Dispose();
        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class E2E : ICollectionFixture<TwoHostWebApplicationFactory>
{
    public const string Name = nameof(E2E);
}

public sealed class ApiHostFactory : WebApplicationFactory<Cardscape.Api.Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;
    private int _port;

    public string ServerAddress
    {
        get
        {
            if (Server is null) return $"http://127.0.0.1:{_port} (not-built)";
            return Server.Features
                .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                .Addresses.First();
        }
    }

    public void Initialize(int port)
    {
        _port = port;
        _ = CreateClient();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        string? previousConn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string? previousProvider = Environment.GetEnvironmentVariable("Database__Provider");
        string? previousJwt = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        string? previousStorage = Environment.GetEnvironmentVariable("Storage__LocalRoot");
        string? previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        string? previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        string? previousMcpUrl = Environment.GetEnvironmentVariable("Cardscape__Mcp__BaseUrl");
        string? previousSecret = Environment.GetEnvironmentVariable("Internal__Secret");

        string id = Guid.NewGuid().ToString("N");
        _connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"cardscape-e2e-api-{id}.db")}";
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(typeof(Cardscape.Api.Program).Assembly.Location)!,
            "e2e-tmp",
            $"api-{id}");
        Directory.CreateDirectory(_storageRoot);

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "e2e-tests-signing-key-please-override-32+chars");
        Environment.SetEnvironmentVariable("Storage__LocalRoot", _storageRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://127.0.0.1:{_port}");
        Environment.SetEnvironmentVariable("Cardscape__Mcp__BaseUrl",
            $"http://127.0.0.1:{TwoHostWebApplicationFactory.McpPort}");
        Environment.SetEnvironmentVariable("Internal__Secret", TwoHostWebApplicationFactory.SharedSecret);

        builder.ConfigureLogging(logging =>
        {
            logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss.fff ";
            });
            logging.AddFilter("Cardscape.Api.Realtime", LogLevel.Trace);
            logging.AddFilter("Cardscape.Mcp", LogLevel.Information);
        });

        // The cross-process broadcaster diagnostic. We
        // replace the API's named HttpClient "Cardscape.Mcp"
        // with two delegating handlers: a recording handler
        // that captures every request, and an in-process
        // dispatcher that routes the call into the MCP's
        // in-memory test server. The TestServer transport
        // is in-process, so the production HttpClient (which
        // would try to hit 127.0.0.1:15999 on a real
        // socket) would get "actively refused" without the
        // dispatcher.
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<RecordingSink>();
            services.AddSingleton<RecordingHandler>();
            services.AddSingleton<McpServerDispatcher>();
            services.AddHttpClient("Cardscape.Mcp")
                .AddHttpMessageHandler<RecordingHandler>()
                .AddHttpMessageHandler<McpServerDispatcher>();
        });

        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", previousConn);
            Environment.SetEnvironmentVariable("Database__Provider", previousProvider);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", previousJwt);
            Environment.SetEnvironmentVariable("Storage__LocalRoot", previousStorage);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousUrls);
            Environment.SetEnvironmentVariable("Cardscape__Mcp__BaseUrl", previousMcpUrl);
            Environment.SetEnvironmentVariable("Internal__Secret", previousSecret);
        }
    }
}

public sealed class McpHostFactory : WebApplicationFactory<Cardscape.Mcp.Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;
    private int _port;
    public string ApiBaseAddress { get; set; } = string.Empty;

    public string ServerAddress
    {
        get
        {
            if (Server is null) return $"http://127.0.0.1:{_port} (not-built)";
            return Server.Features
                .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
                .Addresses.First();
        }
    }

    public void Initialize(int port)
    {
        _port = port;
        _ = CreateClient();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        string? previousConn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string? previousProvider = Environment.GetEnvironmentVariable("Database__Provider");
        string? previousStorage = Environment.GetEnvironmentVariable("Storage__LocalRoot");
        string? previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        string? previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        string? previousApiUrl = Environment.GetEnvironmentVariable("Cardscape__Api__BaseAddress");
        string? previousSecret = Environment.GetEnvironmentVariable("Cardscape__Internal__Secret");
        string? previousMcpSec = Environment.GetEnvironmentVariable("Internal__Secret");

        string id = Guid.NewGuid().ToString("N");
        _connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"cardscape-e2e-mcp-{id}.db")}";
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(typeof(Cardscape.Mcp.Program).Assembly.Location)!,
            "e2e-tmp",
            $"mcp-{id}");
        Directory.CreateDirectory(_storageRoot);

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Storage__LocalRoot", _storageRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://127.0.0.1:{_port}");
        Environment.SetEnvironmentVariable("Cardscape__Api__BaseAddress", ApiBaseAddress);
        Environment.SetEnvironmentVariable("Cardscape__Internal__Secret", TwoHostWebApplicationFactory.SharedSecret);
        Environment.SetEnvironmentVariable("Internal__Secret", TwoHostWebApplicationFactory.SharedSecret);

        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", previousConn);
            Environment.SetEnvironmentVariable("Database__Provider", previousProvider);
            Environment.SetEnvironmentVariable("Storage__LocalRoot", previousStorage);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousUrls);
            Environment.SetEnvironmentVariable("Cardscape__Api__BaseAddress", previousApiUrl);
            Environment.SetEnvironmentVariable("Cardscape__Internal__Secret", previousSecret);
            Environment.SetEnvironmentVariable("Internal__Secret", previousMcpSec);
        }
    }
}

/// <summary>
/// Thread-safe list of every HTTP call the API made on
/// the named "Cardscape.Mcp" client. The E2E test reads
/// the list to assert that the cross-process broadcaster
/// actually reached the MCP.
/// </summary>
public sealed class RecordingSink
{
    private readonly List<RecordedCall> _calls = new();
    private readonly object _gate = new();

    public void Record(RecordedCall call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    public IReadOnlyList<RecordedCall> Snapshot()
    {
        lock (_gate)
        {
            return _calls.ToArray();
        }
    }
}

public sealed record RecordedCall(
    string Method,
    string Uri,
    int? StatusCode,
    string? Failure);

public sealed class RecordingHandler : DelegatingHandler
{
    private readonly RecordingSink sink;
    public RecordingHandler(RecordingSink sink) => this.sink = sink;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            sink.Record(new RecordedCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "(no-uri)",
                (int)response.StatusCode,
                null));
            return response;
        }
        catch (Exception ex)
        {
            sink.Record(new RecordedCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "(no-uri)",
                null,
                ex.GetType().Name + ": " + ex.Message));
            throw;
        }
    }
}

/// <summary>
/// Routes an outbound HttpClient call into the MCP's
/// in-process test server. The MCP host's WebApplicationFactory
/// uses Microsoft's TestServer, which does not bind a real
/// network socket. The dispatcher takes the request the
/// API's HttpMcpResourceNotifier made (URL + method + body +
/// headers) and replays it through the MCP's in-memory
/// client. The MCP processes the request through the same
/// pipeline that production traffic hits; the only
/// difference is the transport.
/// </summary>
public sealed class McpServerDispatcher : DelegatingHandler
{
    private RecordingSink? _sink;
    private HttpClient? _mcpClient;

    public void Configure(RecordingSink sink, HttpClient mcpClient)
    {
        _sink = sink;
        _mcpClient = mcpClient;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_mcpClient is null || _sink is null)
        {
            // The fixture's InitializeAsync was not called
            // (or was called too early). Fall back to the
            // production behaviour (real network call).
            return await base.SendAsync(request, cancellationToken);
        }

        // Build a fresh request to dispatch through the
        // MCP's in-memory client. The original request's
        // RequestUri is the full absolute URL; we need
        // to translate it to a relative path the MCP
        // client can use. The MCP's in-memory client
        // uses its BaseAddress to resolve relative paths,
        // so we strip the host+port and keep the path+query.
        string? relativePath = ExtractRelativePath(request.RequestUri);

        var dispatched = new HttpRequestMessage(request.Method, relativePath);
        if (request.Content is not null)
        {
            // Copy the body. Content.ReadAsStreamAsync is
            // not always safe to call multiple times;
            // buffer once.
            byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            dispatched.Content = new ByteArrayContent(body);

            // Copy content headers (Content-Type, etc.).
            foreach (var header in request.Content.Headers)
            {
                dispatched.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Copy non-content headers (X-Internal-Secret, etc.).
        foreach (var header in request.Headers)
        {
            dispatched.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        try
        {
            HttpResponseMessage response = await _mcpClient.SendAsync(
                dispatched, cancellationToken);
            _sink.Record(new RecordedCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "(no-uri)",
                (int)response.StatusCode,
                null));
            return response;
        }
        catch (Exception ex)
        {
            _sink.Record(new RecordedCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "(no-uri)",
                null,
                ex.GetType().Name + ": " + ex.Message));
            throw;
        }
    }

    private static string? ExtractRelativePath(Uri? requestUri)
    {
        if (requestUri is null)
        {
            return null;
        }
        // Build "/path?query" — drop the host+port+scheme.
        string path = requestUri.AbsolutePath;
        string query = string.IsNullOrEmpty(requestUri.Query) ? string.Empty : requestUri.Query;
        return path + query;
    }
}
