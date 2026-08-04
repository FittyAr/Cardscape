using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan) — integration coverage for the
/// user-facing OAuthApp management endpoints. The
/// flow-level endpoints (the OAuth 2.0 protocol
/// itself) are covered in OAuthFlowTests.cs.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class OAuthAppEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public OAuthAppEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Then_List_Adds_OAuthApp_To_Owner()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "api/oauth-apps/",
            new
            {
                name = "My Third-Party App",
                allowedScopes = new[] { "cards.read", "cards.write" },
                redirectUris = new[] { "https://app.example/callback" }
            },
            TestContext.Current.CancellationToken);
        registered.IsSuccessStatusCode.Should().BeTrue();
        if (!registered.IsSuccessStatusCode)
        {
            string errorBody = await registered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException($"Register returned {(int)registered.StatusCode} {registered.StatusCode}. Body: {errorBody}");
        }
        // The POST returns OAuthAppRegistrationDto (Id,
        // ClientId, ClientSecret, SecretPrefix). The name
        // and scopes are echoed back only via the GET
        // list, not the create response.
        OAuthAppRegistrationDto registered_app = (await registered.Content.ReadFromJsonAsync<OAuthAppRegistrationDto>(TestContext.Current.CancellationToken))!;
        registered_app.ClientId.Should().NotBeNullOrWhiteSpace();
        Guid appId = registered_app.Id;

        HttpResponseMessage listed = await client.GetAsync(
            "api/oauth-apps/", TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        OAuthAppSummaryDto[] summaries =
            (await listed.Content.ReadFromJsonAsync<OAuthAppSummaryDto[]>(TestContext.Current.CancellationToken))!;
        OAuthAppSummaryDto summary = summaries.Single(s => s.Id == appId);
        summary.Name.Should().Be("My Third-Party App");
        summary.AllowedScopes.Should().Contain("cards.read");
        summary.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Revoke_Marks_OAuthApp_As_Revoked()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "api/oauth-apps/",
            new
            {
                name = "Revoke me",
                allowedScopes = new[] { "cards.read" },
                redirectUris = new[] { "https://app.example/cb" }
            },
            TestContext.Current.CancellationToken);
        registered.IsSuccessStatusCode.Should().BeTrue();
        OAuthAppRegistrationDto registered_app = (await registered.Content.ReadFromJsonAsync<OAuthAppRegistrationDto>(TestContext.Current.CancellationToken))!;
        Guid appId = registered_app.Id;

        HttpResponseMessage revoked = await client.DeleteAsync(
            $"api/oauth-apps/{appId}", TestContext.Current.CancellationToken);
        if (!revoked.IsSuccessStatusCode)
        {
            string errorBody = await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"Revoke returned {(int)revoked.StatusCode} {revoked.StatusCode}. Body: {errorBody}");
        }
        revoked.IsSuccessStatusCode.Should().BeTrue();

        // The list still contains the app (revoke is a
        // soft delete so the user can see their history),
        // but the summary marks it as revoked. The DTO
        // exposes IsRevoked; the test asserts the boolean
        // flipped from false (the POST default) to true.
        HttpResponseMessage listed = await client.GetAsync(
            "api/oauth-apps/", TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        OAuthAppSummaryDto[]? summaries =
            (await listed.Content.ReadFromJsonAsync<OAuthAppSummaryDto[]>(TestContext.Current.CancellationToken))!;
        OAuthAppSummaryDto summary = summaries.Single(s => s.Id == appId);
        summary.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task List_For_Fresh_User_Returns_Empty()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage listed = await client.GetAsync(
            "api/oauth-apps/", TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        string body = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Be("[]");
    }

    [Fact]
    public async Task Revoke_Unknown_App_Returns_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.DeleteAsync(
            $"api/oauth-apps/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"oauth-app-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed record OAuthAppRegistrationDto(
        Guid Id,
        string ClientId,
        string ClientSecret,
        string SecretPrefix);

    private sealed record OAuthAppSummaryDto(
        Guid Id,
        string Name,
        string ClientId,
        string SecretPrefix,
        IReadOnlyList<string> AllowedScopes,
        IReadOnlyList<string> RedirectUris,
        bool IsRevoked,
        DateTimeOffset CreatedAt);
}
