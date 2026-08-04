using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Cardscape.SecurityTests.Fixtures;

/// <summary>
/// In-process test host for the security regression
/// suite. Mirrors the shape of the integration-tests
/// factory (fresh SQLite per factory, Development
/// environment, env-var handoff) so the OWASP
/// regression tests run against the same real Program
/// composition the deployer gets. The fixture lives
/// here rather than in TestCommon so the security
/// suite is a standalone deliverable for a third-party
/// pen-test firm (the firm should be able to clone
/// this project and run <c>dotnet test
/// Cardscape.SecurityTests</c> without pulling the
/// integration-tests project).
/// </summary>
public sealed class SecurityTestsWebApplicationFactory : WebApplicationFactory<Program>
{
    private string _connectionString = string.Empty;
    private string _storageRoot = string.Empty;

    public HttpClient CreateApiClient() => CreateClient();

    public string ConnectionString => _connectionString;

    public SecurityTestsWebApplicationFactory()
    {
        string id = Guid.NewGuid().ToString("N");
        _connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"cardscape-sec-{id}.db")}";
        _storageRoot = Path.Combine(
            Path.GetDirectoryName(typeof(Program).Assembly.Location)!,
            "sec-tmp",
            $"cardscape-sec-{id}");
        Directory.CreateDirectory(_storageRoot);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        string? previousConn = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        string? previousProvider = Environment.GetEnvironmentVariable("Database__Provider");
        string? previousJwt = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        string? previousStorage = Environment.GetEnvironmentVariable("Storage__LocalRoot");
        string? previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Jwt__SigningKey",
            "security-tests-signing-key-please-override-32+chars");
        Environment.SetEnvironmentVariable("Storage__LocalRoot", _storageRoot);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

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
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<Cardscape.Application.Abstractions.IDeploymentRegion>(
                new UnpinnedRegion());
        });
    }

    private sealed class UnpinnedRegion : Cardscape.Application.Abstractions.IDeploymentRegion
    {
        public Domain.Workspaces.Region Region => Domain.Workspaces.Region.Unspecified;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
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
            // best effort
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
            // best effort
        }
    }
}

[CollectionDefinition(Name)]
public sealed class SecurityApi : ICollectionFixture<SecurityTestsWebApplicationFactory>
{
    public const string Name = nameof(SecurityApi);
}
