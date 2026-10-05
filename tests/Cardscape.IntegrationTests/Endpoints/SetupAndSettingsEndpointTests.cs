using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Setup.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Tests.Common.Fixtures;

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
    public async Task AdminSettings_Can_Be_Read_And_Updated_By_Admin()
    {
        HttpClient adminClient = await CreateAdminClientAsync();

        // Read settings
        HttpResponseMessage getResponse = await adminClient.GetAsync("api/admin/settings", TestContext.Current.CancellationToken);
        getResponse.IsSuccessStatusCode.Should().BeTrue();

        string getJson = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SystemSettingsDto? current = JsonSerializer.Deserialize<SystemSettingsDto>(getJson, TestJson.Options);
        current.Should().NotBeNull();

        // Update settings
        var updateReq = new UpdateSystemSettingsRequest(
            InstanceTitle: "Cardscape IT Suite",
            AllowPublicRegistration: true,
            DefaultLanguage: "es",
            JwtAccessTokenMinutes: 90);

        HttpResponseMessage putResponse = await adminClient.PutAsJsonAsync("api/admin/settings", updateReq, TestContext.Current.CancellationToken);
        putResponse.IsSuccessStatusCode.Should().BeTrue();

        string putJson = await putResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SystemSettingsDto? updated = JsonSerializer.Deserialize<SystemSettingsDto>(putJson, TestJson.Options);

        updated.Should().NotBeNull();
        updated!.InstanceTitle.Should().Be("Cardscape IT Suite");
        updated.DefaultLanguage.Should().Be("es");
        updated.JwtAccessTokenMinutes.Should().Be(90);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"admin-{Guid.NewGuid():N}@cardscape.local";

        RegisterRequest register = new(email, "Admin User", "Password123!");
        HttpResponseMessage regResponse = await client.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken);
        regResponse.IsSuccessStatusCode.Should().BeTrue();

        await _factory.Services.PromoteUserToAdminAsync(email, TestContext.Current.CancellationToken);

        HttpResponseMessage login = await client.PostAsJsonAsync(
            "api/auth/login", new { email, password = "Password123!" },
            TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue();

        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
