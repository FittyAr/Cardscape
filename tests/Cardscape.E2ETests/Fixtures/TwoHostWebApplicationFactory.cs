using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cardscape.E2ETests.Fixtures;

/// <summary>
/// In-process test host that boots BOTH the API and
/// the MCP server in the same test process, then wires
/// the MCP's outbound HTTP-calls to the API's actual
/// listening port. The E2E coverage of the cross-process
/// broadcaster (API mutates -&gt; board event -&gt; MCP
/// receives -&gt; resource subscription delivers to
/// AI client) lives here because no single-host test
/// can exercise the cross-process contract.
/// </summary>
public sealed class TwoHostWebApplicationFactory : IAsyncLifetime
{
    public ApiHostFactory Api { get; } = new();
    public McpHostFactory Mcp { get; } = new();

    public ValueTask InitializeAsync()
    {
        // Boot the API first so the MCP can target its
        // /api/internal/board-event endpoint with a real
        // port.
        Api.Initialize();
        Mcp.ApiBaseAddress = Api.ServerAddress;
        Mcp.Initialize();
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

    public string ServerAddress => Server.Features
        .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
        .Addresses.First();

    public void Initialize()
    {
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
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");

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
        }
    }
}

public sealed class McpHostFactory : WebApplicationFactory<Cardscape.Mcp.Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;
    public string ApiBaseAddress { get; set; } = string.Empty;

    public string ServerAddress => Server.Features
        .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!
        .Addresses.First();

    public void Initialize()
    {
        _ = CreateClient();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The MCP does not register the API's
        // ICurrentUserAccessor (it uses API-token
        // auth, not the user-context pipeline).
        // The Application layer's scoped ICurrentUser
        // is still constructed at boot for the
        // domain-event side-effects; without an
        // accessor the DI graph fails validation.
        // A no-op accessor returning null is the
        // right answer for the MCP (no
        // ClaimsPrincipal context).
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<Cardscape.Application.Abstractions.Security.ICurrentUserAccessor>(
                new Cardscape.Api.Authentication.HttpContextCurrentUserAccessor(
                    new HttpContextAccessor()));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        string? previousConn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string? previousProvider = Environment.GetEnvironmentVariable("Database__Provider");
        string? previousStorage = Environment.GetEnvironmentVariable("Storage__LocalRoot");
        string? previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        string? previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        string? previousApiUrl = Environment.GetEnvironmentVariable("Cardscape__Api__BaseAddress");
        string? previousApiKey = Environment.GetEnvironmentVariable("Cardscape__Api__InternalKey");

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
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        // The MCP's outbound HTTP client uses this base
        // address to call the API's /api/internal/board-event
        // after every board-changing domain event. Pointing
        // it at the API factory's actual listening port is
        // what makes the cross-process E2E test real.
        Environment.SetEnvironmentVariable("Cardscape__Api__BaseAddress", ApiBaseAddress);
        Environment.SetEnvironmentVariable("Cardscape__Api__InternalKey",
            "e2e-internal-key-32+characters-long");

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
            Environment.SetEnvironmentVariable("Cardscape__Api__InternalKey", previousApiKey);
        }
    }
}
