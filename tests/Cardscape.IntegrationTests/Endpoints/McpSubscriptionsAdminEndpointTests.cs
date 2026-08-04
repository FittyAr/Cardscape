using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan follow-up) — integration coverage for
/// the read-only admin endpoint that surfaces the MCP
/// server's resource-subscription state to the Web UI's
/// <c>/admin/mcp-subscriptions</c> page.
///
/// The endpoint is gated by the <c>AdminOnly</c>
/// authorization policy (a v1.2.0 follow-up that
/// supersedes the original <c>RequireAuthorization</c>
/// gate). The tests pin the access contract:
/// <list type="bullet">
///   <item>unauthenticated → 401</item>
///   <item>authenticated, NOT admin → 403</item>
///   <item>authenticated, admin, MCP unreachable → 503</item>
///   <item>authenticated, admin, MCP reachable → 200</item>
/// </list>
///
/// The "MCP reachable" path is not covered by this file —
/// it would require a second host running the MCP server,
/// which is out of scope for the in-process test setup.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class McpSubscriptionsAdminEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public McpSubscriptionsAdminEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetSnapshot_Without_Auth_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSnapshot_Without_Admin_Role_Returns_403()
    {
        // The AdminOnly policy looks up the user's
        // IsAdmin flag in the database. A freshly
        // registered user has IsAdmin=false, so the
        // policy fails and the endpoint returns 403.
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSnapshot_For_Admin_Returns_503_When_Mcp_Unreachable()
    {
        // The AdminOnly policy passes (the test user is
        // promoted to admin by the helper). The
        // McpSubscriptionsClient then returns null
        // because the MCP process is not running in
        // the in-process test host, and the endpoint
        // translates that to 503 + a structured
        // problem body.
        HttpClient client = await CreateAdminClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("MCP subscriptions snapshot is unavailable");
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"mcp-admin-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        // The dev-only promote-self-admin endpoint is
        // registered in Development only. The test
        // fixture sets ASPNETCORE_ENVIRONMENT=Development,
        // so the endpoint is wired and the POST
        // promotes the calling user to admin in the DB.
        HttpResponseMessage promote = await client.PostAsync(
            "api/dev/promote-self-admin", content: null, TestContext.Current.CancellationToken);
        if (!promote.IsSuccessStatusCode)
        {
            string body = await promote.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"promote-self-admin returned {(int)promote.StatusCode} {promote.StatusCode}. Body: {body}");
        }
        return client;
    }
}
