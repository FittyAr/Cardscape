using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using Cardscape.Tests.Common.Fixtures;
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
        // The McpSubscriptionsAdminPolicy passes (the test
        // user is promoted to admin and re-logs in to
        // get a JWT with the fresh is_admin claim). The
        // McpSubscriptionsClient then returns null because
        // the MCP process is not running in the in-process
        // test host, and the endpoint translates that to
        // 503 + a structured problem body.
        HttpClient client = await CreateAdminClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("MCP subscriptions snapshot is unavailable");
    }

    [Fact]
    public async Task GetSnapshot_Token_Minted_Before_Promotion_Still_Returns_403()
    {
        // The is_admin claim is embedded in the JWT at
        // mint time. A token issued before the test fixture
        // changes the underlying user still carries
        // is_admin=false even after the DB row is
        // updated — the operator has to re-authenticate
        // (or wait for the access-token TTL, default
        // 60 minutes) to pick up the new value. This
        // test pins the contract so the implementation
        // never silently falls back to the DB lookup for
        // tokens that DO carry the claim.
        (HttpClient client, string email) = await CreateRegisteredClientAsync();
        await _factory.Services.PromoteUserToAdminAsync(
            email, TestContext.Current.CancellationToken);
        // Same client, same token — no re-login. The
        // is_admin claim is still false because the
        // token was minted before the promotion.
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the cached is_admin claim wins over the DB row; the operator must re-login to pick up the change");
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<(HttpClient Client, string Email)> CreateRegisteredClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"mcp-admin-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, email);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        (HttpClient client, string _) = await CreateRegisteredClientAsync();
        return client;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        // Register, promote through the test fixture, then re-login so the new
        // JWT carries the is_admin=true claim. The
        // McpSubscriptionsAdminPolicy reads the claim
        // (no DB lookup) and a stale token would
        // 403.
        (HttpClient firstClient, string email) = await CreateRegisteredClientAsync();
        await _factory.Services.PromoteUserToAdminAsync(
            email, TestContext.Current.CancellationToken);
        firstClient.Dispose();

        // Re-login to get a JWT with the fresh
        // is_admin claim. The test API's login
        // endpoint takes (email, password) and
        // returns a fresh AuthResponse.
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage login = await client.PostAsJsonAsync(
            "api/auth/login", new { email, password = "Password123!" },
            TestContext.Current.CancellationToken);
        login.IsSuccessStatusCode.Should().BeTrue(
            "after fixture promotion, the user should be able to log in with the same password");
        AuthResponse auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
