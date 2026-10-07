using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Setup.DTOs;
using Cardscape.Contracts.Settings;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Tests.Common.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class SetupAndSettingsEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public SetupAndSettingsEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Setup_Status_Returns_Database_Provider_And_Initialized_State()
    {
        HttpClient client = _factory.CreateApiClient();

        HttpResponseMessage response = await client.GetAsync("api/setup/status", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SetupStatusResponse? status = JsonSerializer.Deserialize<SetupStatusResponse>(json, TestJson.Options);

        status.Should().NotBeNull();
        status!.DatabaseProvider.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AdminSettings_Requires_Admin_Authorization()
    {
        HttpClient anonymousClient = _factory.CreateApiClient();

        // 1. Anonymous -> 401
        HttpResponseMessage anonResponse = await anonymousClient.GetAsync("api/admin/settings", TestContext.Current.CancellationToken);
        anonResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 2. Regular user (non-admin) -> 403
        string email = $"user-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Regular User", "Password123!");
        HttpResponseMessage regResponse = await anonymousClient.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken);
        regResponse.IsSuccessStatusCode.Should().BeTrue();

        string regJson = await regResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        AuthResponse? auth = JsonSerializer.Deserialize<AuthResponse>(regJson, TestJson.Options);

        HttpClient userClient = _factory.CreateApiClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        HttpResponseMessage userResponse = await userClient.GetAsync("api/admin/settings", TestContext.Current.CancellationToken);
        userResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminSettings_RoundTrip_Validate_And_Reset()
    {
        using WebApplicationFactory<Program> host = _factory.WithWebHostBuilder(_ => { });
        HttpClient admin = await CreateAdminClientAsync(host);
        CancellationToken ct = TestContext.Current.CancellationToken;

        SystemSettings current = (await admin.GetFromJsonAsync<SystemSettings>("api/admin/settings", TestJson.Options, ct))!;
        current.General.InstanceTitle.Should().Be("Cardscape");

        current.General.InstanceTitle = "Cardscape IT Suite";
        current.General.SupportEmail = "it@cardscape.local";
        current.Ai.ApiKey = "sk-integration";
        HttpResponseMessage put = await admin.PutAsJsonAsync("api/admin/settings", current, TestJson.Options, ct);
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        SystemSettings updated = (await put.Content.ReadFromJsonAsync<SystemSettings>(TestJson.Options, ct))!;
        updated.General.InstanceTitle.Should().Be("Cardscape IT Suite");
        updated.Ai.ApiKey.Should().BeNull("secrets never leave the server");
        updated.Ai.HasApiKey.Should().BeTrue();

        updated.Limits.InvitationLifetimeDays = 0;
        HttpResponseMessage invalid = await admin.PutAsJsonAsync("api/admin/settings", updated, TestJson.Options, ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the contract's DataAnnotations are enforced by the built-in endpoint validation");
        (await invalid.Content.ReadAsStringAsync(ct)).Should().Contain("InvitationLifetimeDays");

        HttpResponseMessage reset = await admin.PostAsync("api/admin/settings/reset", null, ct);
        (await reset.Content.ReadFromJsonAsync<SystemSettings>(TestJson.Options, ct))!.General.InstanceTitle.Should().Be("Cardscape");
    }

    [Fact]
    public async Task AdminSettings_ReportRuntimeConfiguration_AndDiagnostics_WithoutLeakingSecrets()
    {
        HttpClient admin = await CreateAdminClientAsync(_factory);
        CancellationToken ct = TestContext.Current.CancellationToken;

        RuntimeConfigurationEntry[] runtime = (await admin.GetFromJsonAsync<RuntimeConfigurationEntry[]>(
            "api/admin/settings/runtime", TestJson.Options, ct))!;
        runtime.Should().Contain(e => e.EnvironmentVariable == "Database__Provider" && e.Value == "Sqlite");
        runtime.Where(e => e.IsSecret).Should().OnlyContain(e => e.Value == null);
        runtime.Should().Contain(e => e.EnvironmentVariable == "Jwt__SigningKey" && e.IsConfigured);

        SystemDiagnostics diagnostics = (await admin.GetFromJsonAsync<SystemDiagnostics>(
            "api/admin/settings/diagnostics", TestJson.Options, ct))!;
        diagnostics.DatabaseReachable.Should().BeTrue();
        diagnostics.ThreadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PublicConfig_And_Registration_FollowTheSettings()
    {
        using WebApplicationFactory<Program> host = _factory.WithWebHostBuilder(_ => { });
        HttpClient admin = await CreateAdminClientAsync(host);
        CancellationToken ct = TestContext.Current.CancellationToken;
        SystemSettings settings = (await admin.GetFromJsonAsync<SystemSettings>("api/admin/settings", TestJson.Options, ct))!;
        settings.General.InstanceTitle = "Nexora";
        settings.Notices.AnnouncementEnabled = true;
        settings.Notices.AnnouncementMessage = "Release on Friday";
        settings.Access.AllowPublicRegistration = false;
        settings.Access.GoogleSignInEnabled = true;
        (await admin.PutAsJsonAsync("api/admin/settings", settings, TestJson.Options, ct)).EnsureSuccessStatusCode();

        HttpClient anonymous = host.CreateClient();
        PublicInstanceSettings config = (await anonymous.GetFromJsonAsync<PublicInstanceSettings>("api/auth/config", TestJson.Options, ct))!;
        config.InstanceTitle.Should().Be("Nexora");
        config.Announcement.Should().Be("Release on Friday");
        config.AllowPublicRegistration.Should().BeFalse();
        config.Google.Should().BeFalse("enabled in settings, but no Google credentials are configured on this host");

        HttpResponseMessage register = await anonymous.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest($"late-{Guid.NewGuid():N}@cardscape.local", "Late", "Password123!"), ct);
        register.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MaintenanceMode_Blocks_Regular_Users_But_Not_Administrators()
    {
        using WebApplicationFactory<Program> host = _factory.WithWebHostBuilder(_ => { });
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient admin = await CreateAdminClientAsync(host);
        HttpClient member = await CreateUserClientAsync(host);

        SystemSettings settings = (await admin.GetFromJsonAsync<SystemSettings>("api/admin/settings", TestJson.Options, ct))!;
        settings.Notices.MaintenanceEnabled = true;
        settings.Notices.MaintenanceMessage = "Back at 18:00";
        (await admin.PutAsJsonAsync("api/admin/settings", settings, TestJson.Options, ct)).EnsureSuccessStatusCode();

        HttpResponseMessage blocked = await member.GetAsync("api/workspaces", ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await blocked.Content.ReadAsStringAsync(ct)).Should().Contain("Back at 18:00");

        (await admin.GetAsync("api/workspaces", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.CreateClient().GetAsync("api/auth/config", ct)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the login page must still learn that the instance is in maintenance");
    }

    private static async Task<HttpClient> CreateUserClientAsync(WebApplicationFactory<Program> host)
    {
        HttpClient client = host.CreateClient();
        string email = $"user-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage register = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, "Regular User", "Password123!"), TestContext.Current.CancellationToken);
        register.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await register.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<HttpClient> CreateAdminClientAsync(WebApplicationFactory<Program> host)
    {
        HttpClient client = host.CreateClient();
        string email = $"admin-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest register = new(email, "Admin User", "Password123!");
        HttpResponseMessage regResponse = await client.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken);
        regResponse.IsSuccessStatusCode.Should().BeTrue();

        await host.Services.PromoteUserToAdminAsync(email, TestContext.Current.CancellationToken);

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "api/auth/login", new { email, password = "Password123!" },
            TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue();

        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
