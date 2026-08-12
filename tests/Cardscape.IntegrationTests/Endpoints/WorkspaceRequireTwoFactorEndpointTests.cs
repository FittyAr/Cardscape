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
/// Coverage for the enforced workspace 2FA requirement.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class WorkspaceRequireTwoFactorEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public WorkspaceRequireTwoFactorEndpointTests(CardscapeWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task SetRequireTwoFactor_ByOwner_TogglesPolicy()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "2FA WS");
        await EnrollTotpAsync(owner);

        HttpResponseMessage on = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/security/require-2fa",
            new { require = true }, TestContext.Current.CancellationToken);
        on.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto after = (await on.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        after.RequireTwoFactor.Should().BeTrue();

        HttpResponseMessage off = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/security/require-2fa",
            new { require = false }, TestContext.Current.CancellationToken);
        off.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto after2 = (await off.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        after2.RequireTwoFactor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRequireTwoFactor_WhenMemberIsNotEnrolled_ReturnsConflict()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "2FA incomplete enrollment WS");

        HttpResponseMessage response = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/security/require-2fa",
            new { require = true }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        WorkspaceDto unchanged = await GetWorkspaceAsync(owner, ws.Id);
        unchanged.RequireTwoFactor.Should().BeFalse();
    }

    [Fact]
    public async Task SetRequireTwoFactor_ByNonOwner_ReturnsForbidden()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "2FA NonOwner WS");

        HttpClient other = await CreateAuthenticatedClientAsync("Other");
        HttpResponseMessage resp = await other.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/security/require-2fa",
            new { require = true }, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetRequireTwoFactor_WithoutAuth_ReturnsUnauthorized()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "2FA Unauth WS");

        HttpClient anon = _factory.CreateApiClient();
        HttpResponseMessage resp = await anon.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/security/require-2fa",
            new { require = true }, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWorkspace_DefaultsRequireTwoFactorToFalse()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");

        HttpResponseMessage resp = await owner.PostAsJsonAsync(
            "api/workspaces/", new { name = "Default 2FA WS" }, TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        ws.RequireTwoFactor.Should().BeFalse();
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string displayNamePrefix)
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"{displayNamePrefix}-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, $"{displayNamePrefix} User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name }, TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task EnrollTotpAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync(
            "api/auth/2fa/enroll", content: null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<WorkspaceDto> GetWorkspaceAsync(HttpClient client, Guid workspaceId)
    {
        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{workspaceId}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
    }
}
