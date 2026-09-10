using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Tests.Common.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class BackgroundJobAdminEndpointTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public async Task GetDeadLetters_WithoutAuthentication_ReturnsUnauthorized()
    {
        using HttpClient client = factory.CreateApiClient();

        using HttpResponseMessage response = await client.GetAsync(
            "api/background-jobs/dead-letter",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDeadLetters_AsNonAdmin_ReturnsForbidden()
    {
        using HttpClient client = await CreateRegisteredClientAsync();

        using HttpResponseMessage response = await client.GetAsync(
            "api/background-jobs/dead-letter",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetDeadLetters_AsAdmin_ReturnsCollection()
    {
        using HttpClient client = await CreateAdminClientAsync();

        using HttpResponseMessage response = await client.GetAsync(
            "api/background-jobs/dead-letter?skip=-1&take=10000",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<object>? rows = await response.Content.ReadFromJsonAsync<IReadOnlyList<object>>(
            TestContext.Current.CancellationToken);
        rows.Should().NotBeNull();
    }

    private async Task<HttpClient> CreateRegisteredClientAsync()
    {
        HttpClient client = factory.CreateApiClient();
        string email = $"background-admin-{Guid.NewGuid():N}@cardscape.local";
        using HttpResponseMessage registration = await client.PostAsJsonAsync(
            "api/auth/register",
            new RegisterRequest(email, "Tester", "Password123!"),
            TestContext.Current.CancellationToken);
        registration.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await registration.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        HttpClient client = factory.CreateApiClient();
        string email = $"background-admin-{Guid.NewGuid():N}@cardscape.local";
        using HttpResponseMessage registration = await client.PostAsJsonAsync(
            "api/auth/register",
            new RegisterRequest(email, "Tester", "Password123!"),
            TestContext.Current.CancellationToken);
        registration.IsSuccessStatusCode.Should().BeTrue();

        await factory.Services.PromoteUserToAdminAsync(
            email,
            TestContext.Current.CancellationToken);
        client.Dispose();

        HttpClient adminClient = factory.CreateApiClient();
        using HttpResponseMessage login = await adminClient.PostAsJsonAsync(
            "api/auth/login",
            new { email, password = "Password123!" },
            TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(
            TestContext.Current.CancellationToken))!;
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return adminClient;
    }
}
