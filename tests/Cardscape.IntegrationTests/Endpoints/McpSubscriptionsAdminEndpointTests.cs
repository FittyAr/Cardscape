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
/// The endpoint proxies the MCP's
/// <c>GET /api/internal/board-event/subscriptions</c> over
/// the shared internal secret. In the in-process test
/// environment the MCP is not running, so the API returns
/// HTTP 503 (the McpSubscriptionsClient returns null when
/// the MCP is unreachable). The tests pin the response
/// contract:
/// <list type="bullet">
///   <item>unauthenticated → 401</item>
///   <item>authenticated, MCP unreachable → 503 with a
///         structured <c>ProblemDetails</c> body</item>
///   <item>authenticated, MCP reachable → 200 with the
///         McpSubscriptionsSnapshot shape</item>
/// </list>
///
/// The "MCP reachable" path is not covered by this file —
/// it would require a second host running the MCP server,
/// which is out of scope for the in-process test setup. The
/// MCP-side broadcaster is unit-tested instead
/// (see <c>McpResourceBroadcasterTests</c> in the unit
/// test project).
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
    public async Task GetSnapshot_With_Auth_But_Mcp_Unreachable_Returns_503()
    {
        // The in-process test host does not start the MCP
        // process. The McpSubscriptionsClient logs a warning
        // and returns null; the endpoint translates null to
        // 503 + a structured problem body. The body shape is
        // the standard ASP.NET Core ProblemDetails.
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("MCP subscriptions snapshot is unavailable");
    }

    [Fact]
    public async Task GetSnapshot_With_Auth_Reaches_The_Endpoint()
    {
        // The 401 (no auth) and 503 (auth, no MCP) tests
        // above pin the access gate and the failure mode.
        // This test pins the positive path: a registered
        // user with a valid bearer token can call the
        // endpoint. The MCP is not running in the test
        // environment, so we still get 503 — but the
        // assertion is that the request is not rejected
        // at the auth layer (a 401 here would mean the
        // auth pipeline is broken, not that the MCP is
        // down).
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.GetAsync(
            "api/admin/mcp-subscriptions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
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
}
