using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Security.Commands;
using Cardscape.Application.Security.Queries;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the per-API-token rate limit:
/// <list type="bullet">
///   <item>unauthenticated and JWT-authenticated requests are
///         never throttled (humans bypass the limiter);</item>
///   <item>API-token requests pass through the middleware and
///         get 429 + <c>Retry-After</c> when the bucket
///         is empty;</item>
///   <item>the PATCH + GET endpoints let the user inspect and
///         change the limits.</item>
/// </list>
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class RateLimitTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public RateLimitTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Unauthenticated_Request_To_Health_Is_NotRateLimited()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync("health", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jwt_Authenticated_Workspaces_Call_Is_NotRateLimited()
    {
        HttpClient client = await CreateJwtClientAsync();
        HttpResponseMessage response = await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized)
            .And.NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ApiToken_DefaultLimits_AllowsRequest()
    {
        HttpClient jwtClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto issued = await IssueTokenAsync(jwtClient, rateLimitPerHour: null, burstSize: null);
        HttpClient client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issued.CleartextSecret);

        // Hit a non-throttled surface (workspaces list) using the
        // API token. The middleware consumes one token from the
        // bucket but the bucket is at burst=50 by default, so a
        // single call must succeed.
        HttpResponseMessage response = await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ApiToken_ExceedsBurst_Returns429_WithRetryAfter()
    {
        // Issue a token with a tiny burst so we can exhaust it
        // in one test.
        HttpClient jwtClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto issued = await IssueTokenAsync(jwtClient, rateLimitPerHour: 60, burstSize: 2);

        HttpClient client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issued.CleartextSecret);

        // First two calls go through.
        (await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken)).StatusCode
            .Should().NotBe(HttpStatusCode.TooManyRequests);
        (await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken)).StatusCode
            .Should().NotBe(HttpStatusCode.TooManyRequests);

        // Third call hits an empty bucket.
        HttpResponseMessage denied = await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        denied.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        denied.Headers.RetryAfter.Should().NotBeNull();
        denied.Headers.RetryAfter!.Delta.Should().NotBeNull();
        denied.Headers.RetryAfter.Delta!.Value.TotalSeconds.Should().BeGreaterThan(0);

        string body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("rate_limited");
    }

    [Fact]
    public async Task PatchRateLimit_UpdatesBucketAndStaysInSync()
    {
        // Same JWT client issues AND patches the token, so the
        // authenticated user is the owner of the token.
        HttpClient jwtClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto issued = await IssueTokenAsync(jwtClient, rateLimitPerHour: 60, burstSize: 1);

        // PATCH the limit to burst=1, rate=3600 (1/s refill).
        HttpResponseMessage patch = await jwtClient.PatchAsJsonAsync(
            $"api/security/api-tokens/{issued.Id}/rate-limit",
            new { rateLimitPerHour = 3600, burstSize = 1 }, TestContext.Current.CancellationToken);
        if (!patch.IsSuccessStatusCode)
        {
            string body = await patch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException($"PATCH failed: {(int)patch.StatusCode} {patch.StatusCode} body={body}");
        }
        patch.IsSuccessStatusCode.Should().BeTrue();

        ApiTokenRateLimitDto? after = await patch.Content.ReadFromJsonAsync<ApiTokenRateLimitDto>(TestContext.Current.CancellationToken);
        if (after is null)
        {
            string raw = await patch.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException($"PATCH body deserialized null. Raw: {raw}");
        }
        after.Should().NotBeNull();
        after!.RateLimitPerHour.Should().Be(3600);
        after.BurstSize.Should().Be(1);
    }

    [Fact]
    public async Task GetRateLimitStatus_ReturnsCurrentBucketState()
    {
        HttpClient jwtClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto issued = await IssueTokenAsync(jwtClient, rateLimitPerHour: 1000, burstSize: 7);

        HttpResponseMessage status = await jwtClient.GetAsync(
            $"api/security/api-tokens/{issued.Id}/rate-limit-status", TestContext.Current.CancellationToken);
        if (!status.IsSuccessStatusCode)
        {
            string body = await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException($"GET rate-limit-status failed: {(int)status.StatusCode} {status.StatusCode} body={body}");
        }
        status.IsSuccessStatusCode.Should().BeTrue();

        ApiTokenRateLimitStatusDto? dto = await status.Content.ReadFromJsonAsync<ApiTokenRateLimitStatusDto>(TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.TokenId.Should().Be(issued.Id);
        dto.RateLimitPerHour.Should().Be(1000);
        dto.BurstSize.Should().Be(7);
        dto.AvailableTokens.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(7);
    }

    [Fact]
    public async Task GetRateLimitStatus_ForOtherUsersToken_Returns404()
    {
        // Owner issues a token.
        HttpClient ownerClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto ownerToken = await IssueTokenAsync(ownerClient, rateLimitPerHour: null, burstSize: null);

        // Different authenticated user tries to inspect it.
        HttpClient intruder = await CreateJwtClientAsync();
        HttpResponseMessage response = await intruder.GetAsync(
            $"api/security/api-tokens/{ownerToken.Id}/rate-limit-status", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRateLimitStatus_WithoutAuth_Returns401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.GetAsync(
            $"api/security/api-tokens/{Guid.NewGuid()}/rate-limit-status", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiToken_HealthCheck_IsNotRateLimited()
    {
        // The middleware explicitly skips /health so a throttled
        // user can still see liveness.
        HttpClient jwtClient = await CreateJwtClientAsync();
        ApiTokenIssuanceDto issued = await IssueTokenAsync(jwtClient, rateLimitPerHour: 60, burstSize: 1);

        HttpClient client = _factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issued.CleartextSecret);

        // Drain the bucket.
        await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);

        // Health is still reachable.
        HttpResponseMessage health = await client.GetAsync("health", TestContext.Current.CancellationToken);
        health.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<ApiTokenIssuanceDto> IssueTokenAsync(
        HttpClient client, int? rateLimitPerHour, int? burstSize)
    {
        object body = rateLimitPerHour is null
            ? (object)new { name = "rate-limit-test", scopes = new[] { "read" } }
            : new { name = "rate-limit-test", scopes = new[] { "read" }, rateLimitPerHour, burstSize };

        HttpResponseMessage issue = await client.PostAsJsonAsync("api/security/api-tokens/", body);
        if (!issue.IsSuccessStatusCode)
        {
            string raw = await issue.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Issue failed: {(int)issue.StatusCode} {issue.StatusCode} body={raw}");
        }
        ApiTokenIssuanceDto? issued = await issue.Content.ReadFromJsonAsync<ApiTokenIssuanceDto>();
        issued.Should().NotBeNull();
        return issued!;
    }

    private async Task<HttpClient> CreateJwtClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"rl-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Rate Limit Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
