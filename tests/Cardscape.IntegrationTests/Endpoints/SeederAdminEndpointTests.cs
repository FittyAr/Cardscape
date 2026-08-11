using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class SeederAdminEndpointTests
{
    private static readonly (HttpMethod Method, string Route)[] SeederRoutes =
    [
        (HttpMethod.Get, "api/admin/seeder/status"),
        (HttpMethod.Get, "api/admin/seeder/options"),
        (HttpMethod.Post, "api/admin/seeder/run"),
        (HttpMethod.Post, "api/admin/seeder/wipe")
    ];

    private readonly CardscapeWebApplicationFactory _factory;

    public SeederAdminEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SeederEndpoints_WithoutAuthentication_ReturnUnauthorized()
    {
        using WebApplicationFactory<Program> enabledFactory = CreateEnabledFactory();
        using HttpClient client = enabledFactory.CreateClient();

        foreach ((HttpMethod method, string route) in SeederRoutes)
        {
            using HttpResponseMessage response = await client.SendAsync(
                new HttpRequestMessage(method, route), TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {route} is an admin route");
        }
    }

    [Fact]
    public async Task SeederEndpoints_ForAuthenticatedNonAdmin_ReturnForbidden()
    {
        using WebApplicationFactory<Program> enabledFactory = CreateEnabledFactory();
        using HttpClient client = await CreateAuthenticatedClientAsync(enabledFactory);

        foreach ((HttpMethod method, string route) in SeederRoutes)
        {
            using HttpResponseMessage response = await client.SendAsync(
                new HttpRequestMessage(method, route), TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {route} requires AdminOnly");
        }
    }

    [Fact]
    public async Task SeederReadEndpoints_ForAdmin_WhenEnabled_ReturnOk()
    {
        using WebApplicationFactory<Program> enabledFactory = CreateEnabledFactory();
        using HttpClient client = await CreateAdminClientAsync(enabledFactory);

        foreach (string route in new[] { "api/admin/seeder/status", "api/admin/seeder/options" })
        {
            using HttpResponseMessage response = await client.GetAsync(route, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {route} is available to admins when enabled");
        }
    }

    [Theory]
    [InlineData("api/admin/seeder/run")]
    [InlineData("api/admin/seeder/wipe")]
    public async Task SeederCommandEndpoint_ForAdmin_WhenEnabled_ReturnsAccepted(string route)
    {
        using WebApplicationFactory<Program> enabledFactory = CreateEnabledFactory();
        using HttpClient client = await CreateAdminClientAsync(enabledFactory);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            route, new { wipe = false }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, $"POST {route} is available to admins when enabled");
    }

    private WebApplicationFactory<Program> CreateEnabledFactory() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cardscape:Seeder:Enabled"] = "true"
                })));

    private static async Task<(HttpClient Client, string Email)> CreateRegisteredClientAsync(
        WebApplicationFactory<Program> factory)
    {
        HttpClient client = factory.CreateClient();
        string email = $"seeder-admin-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage registration = await client.PostAsJsonAsync(
            "api/auth/register",
            new RegisterRequest(email, "Seeder Tester", "Password123!"),
            TestContext.Current.CancellationToken);
        registration.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await registration.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, email);
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        (HttpClient client, _) = await CreateRegisteredClientAsync(factory);
        return client;
    }

    private static async Task<HttpClient> CreateAdminClientAsync(WebApplicationFactory<Program> factory)
    {
        (HttpClient firstClient, string email) = await CreateRegisteredClientAsync(factory);
        using (firstClient)
        {
            HttpResponseMessage promotion = await firstClient.PostAsync(
                "api/dev/promote-self-admin", null, TestContext.Current.CancellationToken);
            promotion.IsSuccessStatusCode.Should().BeTrue();
        }

        HttpClient client = factory.CreateClient();
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "api/auth/login", new { email, password = "Password123!" }, TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
