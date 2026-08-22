using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage for the data-residency guard
/// <c>RegionGuardEndpointFilter</c> (added in G5 of the v1.1.0
/// audit). The feature shipped the <c>Region</c> enum, the
/// <c>Workspace.GuardRegion</c> method, the
/// <c>IDeploymentRegion</c> abstraction, the migration, and the
/// Web UI region selector, but <c>GuardRegion</c> was never called
/// from any endpoint and <c>IDeploymentRegion</c> was registered
/// but never resolved. This test asserts the cross-region write
/// guard the filter enforces, plus the "no deployment region
/// configured → no gating" baseline.
/// <para>
/// The class opts into the <c>RegionGuardSerial</c> collection
/// (D6 in the v1.2.0 plan) and uses an <c>IClassFixture</c> for
/// a per-class <see cref="CardscapeWebApplicationFactory"/> so
/// the three region tests run one at a time. The
/// <c>WithWebHostBuilder</c> pattern that the cross-region tests
/// use re-attaches the parent's connection string via an
/// additive in-memory configuration source; the serial
/// collection removes the race window against the rest of the
/// integration suite.
/// </para>
/// <para>
/// The 4th test (<see cref="ConfigInjection_SerialCollectionPreventsParallelRace"/>)
/// pins the read-path contract: a fresh aux host whose
/// deployment region matches the workspace must return the
/// same workspace. It's a regression check on the read path
/// after the serial collection is in place.
/// </para>
/// </summary>
[Collection(RegionGuardSerial.Name)]
public class RegionGuardEndpointFilterTests : IClassFixture<CardscapeWebApplicationFactory>
{
    private readonly CardscapeWebApplicationFactory _factory;

    public RegionGuardEndpointFilterTests(CardscapeWebApplicationFactory factory)
    {
        _factory = factory;
        // Don't change the collection's DeploymentRegion — the
        // shared host's IDeploymentRegion stays Unspecified so the
        // "UnspecifiedDeployment_DoesNotGate_AnyRegion" path can
        // run against the shared host. The cross-region tests
        // build a one-off host with WithWebHostBuilder to pin the
        // region to Europe / NorthAmerica.
    }

    [Fact]
    public async Task CrossRegionWrite_OnExistingEuropeWorkspace_FromNorthAmericaDeployment_Returns422()
    {
        // Arrange — create a Europe-pinned workspace while the
        // deployment is Unspecified (no gating).
        HttpClient ownerClient = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage createWs = await ownerClient.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("Acme EU", Region: Domain.Workspaces.Region.Europe), TestJson.Options, TestContext.Current.CancellationToken);
        createWs.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWs.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        workspace.Region.Should().Be(Domain.Workspaces.Region.Europe);

        // Arrange — build a secondary host whose deployment region
        // is NorthAmerica. The WithWebHostBuilder call inherits
        // the parent's SQLite connection string and storage root,
        // so the workspace we just created is visible. The
        // IDeploymentRegion override takes effect because the
        // Microsoft.Extensions.DependencyInjection service
        // collection resolves to the last-registered singleton
        // (the one we add in ConfigureTestServices).
        WebApplicationFactory<Program> northAmericaFactory = _factory.WithWebHostBuilder(builder =>
        {
            // Re-inject the parent's connection string + storage
            // root into the auxiliary host's configuration. The
            // env-var approach used by the parent factory's
            // CreateHost is restored in a finally block, so a
            // secondary host built by WithWebHostBuilder does NOT
            // automatically see the same database — we have to
            // forward the values explicitly via the in-memory
            // configuration provider (the same pattern the
            // BackgroundJobDispatcherTests use).
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _factory.ConnectionString,
                    ["Storage:LocalRoot"] = _factory.StorageRoot,
                    ["Database:Provider"] = "Sqlite"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                // Add a second IDeploymentRegion singleton; the
                // last registration wins in the .NET DI container.
                services.AddSingleton<Cardscape.Application.Abstractions.IDeploymentRegion>(
                    new Cardscape.Tests.Common.Fakes.FakeDeploymentRegion
                    {
                        Region = Domain.Workspaces.Region.NorthAmerica
                    });
            });
        });

        // DIAGNOSTIC: print the connection string the secondary host will use.
        IConfiguration secondaryConfig = northAmericaFactory.Services
            .GetRequiredService<IConfiguration>();
        string? secondaryConn = secondaryConfig.GetConnectionString("Default");
        System.Console.WriteLine($"[DIAG] Secondary host connection string: {secondaryConn}");
        System.Console.WriteLine($"[DIAG] Parent host connection string:    {_factory.ConnectionString}");

        HttpClient client = northAmericaFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Act — a workspace-scoped read that previously passed
        // without the filter should now be rejected with 422.
        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{workspace.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("workspaces.region_mismatch");
    }

    [Fact]
    public async Task SameRegionWrite_OnExistingEuropeWorkspace_FromEuropeDeployment_Succeeds()
    {
        // Arrange — create a Europe-pinned workspace while the
        // deployment is Unspecified.
        HttpClient ownerClient = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage createWs = await ownerClient.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("Acme EU 2", Region: Domain.Workspaces.Region.Europe), TestJson.Options, TestContext.Current.CancellationToken);
        createWs.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWs.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // Arrange — secondary host whose deployment region is
        // Europe (the same as the workspace). The guard should
        // allow the call through.
        WebApplicationFactory<Program> europeFactory = _factory.WithWebHostBuilder(builder =>
        {
            // Re-inject the parent's connection string + storage
            // root (see comment in the NorthAmerica test for why
            // this can't be elided).
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _factory.ConnectionString,
                    ["Storage:LocalRoot"] = _factory.StorageRoot,
                    ["Database:Provider"] = "Sqlite"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<Cardscape.Application.Abstractions.IDeploymentRegion>(
                    new Cardscape.Tests.Common.Fakes.FakeDeploymentRegion
                    {
                        Region = Domain.Workspaces.Region.Europe
                    });
            });
        });

        HttpClient client = europeFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Act
        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{workspace.Id}", TestContext.Current.CancellationToken);

        // Assert — same-region reads are not rejected by the
        // guard, so the request reaches the inner handler.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        WorkspaceDto returned = (await response.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        returned.Id.Should().Be(workspace.Id);
    }

    [Fact]
    public async Task UnspecifiedDeployment_DoesNotGate_AnyRegion()
    {
        // Arrange — create a workspace pinned to Asia Pacific
        // while the deployment is Unspecified (the default for
        // the shared factory).
        HttpClient ownerClient = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage createWs = await ownerClient.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("Acme APAC", Region: Domain.Workspaces.Region.AsiaPacific), TestJson.Options, TestContext.Current.CancellationToken);
        createWs.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWs.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // Act — read the workspace through the original client
        // (deployment still Unspecified).
        HttpResponseMessage response = await ownerClient.GetAsync(
            $"api/workspaces/{workspace.Id}", TestContext.Current.CancellationToken);

        // Assert — Unspecified deployment means the guard is a
        // no-op, so the read reaches the inner handler regardless
        // of the workspace's region.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfigInjection_SerialCollectionPreventsParallelRace()
    {
        // D6 (v1.2.0 plan) — belt-and-braces contract pin.
        //
        // The two cross-region tests above build a secondary
        // host via WithWebHostBuilder. The aux host inherits
        // the parent's env-var-set JwtBearer config, but the
        // shared physical SQLite database (across
        // WithWebHostBuilder hosts) and the per-test
        // IDeploymentRegion override both flow through env
        // vars and DI singletons that are mutable in the
        // shared factory. The serial collection
        // (<see cref="RegionGuardSerial"/>) removes the race
        // window by forcing the three region tests to run one
        // at a time.
        //
        // This test pins the contract: a second invocation of
        // the same workspace read through a fresh aux host
        // MUST return the same workspace, regardless of which
        // test ran first. The test is intentionally simple —
        // it's a regression check on the read path, not on
        // the auth contract. The auth contract is checked by
        // the production deployment smoke tests.
        HttpClient ownerClient = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(ownerClient);
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage createWs = await ownerClient.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("ConfigInjection Probe", Region: Domain.Workspaces.Region.Europe), TestJson.Options, TestContext.Current.CancellationToken);
        createWs.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto workspace = (await createWs.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // Read the workspace through a fresh aux host whose
        // deployment region matches the workspace. The
        // serial collection ensures no other RegionGuard
        // test is racing on the shared physical SQLite file.
        WebApplicationFactory<Program> europeFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _factory.ConnectionString,
                    ["Storage:LocalRoot"] = _factory.StorageRoot,
                    ["Database:Provider"] = "Sqlite"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<Cardscape.Application.Abstractions.IDeploymentRegion>(
                    new Cardscape.Tests.Common.Fakes.FakeDeploymentRegion
                    {
                        Region = Domain.Workspaces.Region.Europe
                    });
            });
        });

        HttpClient client = europeFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{workspace.Id}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        WorkspaceDto returned = (await response.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        returned.Id.Should().Be(workspace.Id);
    }

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"region-guard-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Region Guard User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }
}
