using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the data-residency guard added
/// in the v1.1.0 roadmap execution (P4.5). Uses its own
/// factory (not the shared collection) so the
/// <see cref="CardscapeWebApplicationFactory.DeploymentRegion"/>
/// override doesn't leak across test classes.
/// </summary>
public class RegionEndpointTests : IClassFixture<CardscapeWebApplicationFactory>
{
    private readonly CardscapeWebApplicationFactory _factory;

    public RegionEndpointTests(CardscapeWebApplicationFactory factory)
    {
        _factory = factory;
        // Pin the deployment to Europe; the test then tries
        // to create a workspace pinned to North America and
        // asserts the rejection.
        _factory.DeploymentRegion = Domain.Workspaces.Region.Europe;
    }

    [Fact]
    public async Task CreateWorkspace_WithExplicitNonMatchingRegion_ReturnsRegionMismatch()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/",
            new CreateWorkspaceRequest("Acme EU", Region: Domain.Workspaces.Region.NorthAmerica), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("workspaces.region_mismatch");
    }

    [Fact]
    public async Task CreateWorkspace_WithoutRegion_DefaultsToDeploymentRegion()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("Acme Default"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        WorkspaceDto? workspace = await response.Content.ReadFromJsonAsync<WorkspaceDto>(TestContext.Current.CancellationToken);
        workspace.Should().NotBeNull();
        workspace!.Region.Should().Be(Domain.Workspaces.Region.Europe);
    }

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"region-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Region User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
