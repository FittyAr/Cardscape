using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cardscape.IntegrationTests.Fixtures;

/// <summary>
/// In-process test host for the Cardscape API. Boots the real
/// <c>Program</c> but rewires configuration so each test class
/// gets a fresh SQLite file under the system temp directory.
/// The file is removed by <see cref="Dispose"/>.
/// </summary>
public sealed class CardscapeWebApplicationFactory : WebApplicationFactory<Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;

    /// <summary>Reusable HttpClient bound to the in-process test server.</summary>
    public HttpClient CreateApiClient() => CreateClient();

    public CardscapeWebApplicationFactory()
    {
        string id = Guid.NewGuid().ToString("N");
        _connectionString = $"Data Source=file:cardscape-it-{id}?mode=memory&cache=shared";
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "it-tmp",
            $"cardscape-it-{id}");
        Directory.CreateDirectory(_storageRoot);

        // Env vars override the appsettings values BEFORE Program.cs builds
        // the WebApplication. This is the only place the override is
        // guaranteed to take effect, because AddDbContext captures the
        // connection string at DI-registration time.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "integration-tests-signing-key-please-override-in-production-32+chars");
        Environment.SetEnvironmentVariable("Storage__LocalRoot", _storageRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The API's AddApiAuthentication already registers both
        // the JWT bearer scheme and the API-token scheme behind a
        // "BearerPolicy" wrapper. Tests just use the production
        // setup as-is — no extra wiring needed here.
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Restore the env vars so the next test class / process gets
            // a clean slate, and the in-memory DB is released when the
            // last connection closes. The on-disk storage dir is best-
            // effort to clean up.
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
            Environment.SetEnvironmentVariable("Database__Provider", null);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
            Environment.SetEnvironmentVariable("Storage__LocalRoot", null);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

            TryDeleteDir(_storageRoot);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort.
        }
    }
}

/// <summary>
/// xUnit collection definition. All tests in this assembly that
/// opt in via <c>[Collection(CardscapeApi.Name)]</c> share the
/// same <see cref="CardscapeWebApplicationFactory"/>, which keeps
/// the test suite fast (one host boot).
/// </summary>
[CollectionDefinition(Name)]
public sealed class CardscapeApi : ICollectionFixture<CardscapeWebApplicationFactory>
{
    public const string Name = nameof(CardscapeApi);
}
