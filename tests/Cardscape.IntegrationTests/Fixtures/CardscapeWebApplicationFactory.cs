using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
///
/// <para>Env vars are set JUST BEFORE the host is built (in
/// <see cref="CreateHost"/>) and cleared immediately after.
/// This minimises the race window with parallel test
/// collections that have their own factory instance
/// (e.g. <c>RegionEndpointTests</c> uses <c>IClassFixture</c>).
/// The constructor only allocates the per-factory file paths
/// and storage root — it does not touch process-wide state.</para>
/// </summary>
public sealed class CardscapeWebApplicationFactory : WebApplicationFactory<Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;

    /// <summary>Reusable HttpClient bound to the in-process test server.</summary>
    public HttpClient CreateApiClient() => CreateClient();

    /// <summary>Override the deployment's pinned data-residency
    /// region. Tests that exercise the cross-region write
    /// guard set this to <see cref="Domain.Workspaces.Region.Europe"/>
    /// (or another specific region); the default is
    /// <see cref="Domain.Workspaces.Region.Unspecified"/>
    /// (no gating).</summary>
    public Domain.Workspaces.Region DeploymentRegion { get; set; } = Domain.Workspaces.Region.Unspecified;

    /// <summary>SQLite file path the factory booted with. Exposed
    /// so tests that use <c>WithWebHostBuilder</c> can re-attach
    /// the same physical database to the auxiliary host.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>Storage root the factory booted with. Same
    /// rationale as <see cref="ConnectionString"/>.</summary>
    public string StorageRoot => _storageRoot;

    public CardscapeWebApplicationFactory()
    {
        string id = Guid.NewGuid().ToString("N");
        // Real on-disk file (not in-memory) so that auxiliary
        // WebApplicationFactory<Program>.WithWebHostBuilder(...) hosts
        // — which create their own EF Core connection pool — all
        // point at the same physical database. In-memory shared-cache
        // databases fail to open across separate test hosts because
        // each host's connection pool is initialised independently.
        _connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"cardscape-it-{id}.db")}";
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "it-tmp",
            $"cardscape-it-{id}");
        Directory.CreateDirectory(_storageRoot);
    }

    /// <summary>
    /// Set the per-factory env vars RIGHT BEFORE the host is
    /// built and clear them in a finally block. This is the
    /// narrowest possible window where process-wide state is
    /// mutated; the only thing that can race inside it is
    /// another factory's <c>CreateHost</c> call, which is
    /// serialised within the xUnit scheduler per test method.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        string? previousConnString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string? previousProvider = Environment.GetEnvironmentVariable("Database__Provider");
        string? previousJwtKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        string? previousStorage = Environment.GetEnvironmentVariable("Storage__LocalRoot");
        string? previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "integration-tests-signing-key-please-override-in-production-32+chars");
        Environment.SetEnvironmentVariable("Storage__LocalRoot", _storageRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Default", previousConnString);
            Environment.SetEnvironmentVariable("Database__Provider", previousProvider);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", previousJwtKey);
            Environment.SetEnvironmentVariable("Storage__LocalRoot", previousStorage);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The API's AddApiAuthentication already registers both
        // the JWT bearer scheme and the API-token scheme behind a
        // "BearerPolicy" wrapper. Tests just use the production
        // setup as-is.
        //
        // We also replace the IDeploymentRegion singleton with a
        // FakeDeploymentRegion bound to the test's DeploymentRegion
        // property. The Infrastructure layer registers the
        // configuration-backed implementation as a singleton; the
        // env-var approach was fragile across test classes that
        // share the host.
        Domain.Workspaces.Region pinnedRegion = DeploymentRegion;
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<Cardscape.Application.Abstractions.IDeploymentRegion>(
                new FakeDeploymentRegion(pinnedRegion));
        });
    }

    private sealed class FakeDeploymentRegion : Cardscape.Application.Abstractions.IDeploymentRegion
    {
        private readonly Domain.Workspaces.Region _region;
        public FakeDeploymentRegion(Domain.Workspaces.Region region) => _region = region;
        public Domain.Workspaces.Region Region => _region;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // No env vars to clear — they were restored in
            // CreateHost's finally block. Just remove the on-disk
            // DB and the storage root (best effort).
            TryDelete(_connectionString.Replace("Data Source=", string.Empty));
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
