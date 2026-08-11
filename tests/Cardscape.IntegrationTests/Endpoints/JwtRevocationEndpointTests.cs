using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the JWT-revocation flow:
/// <list type="number">
///   <item>The user calls <c>POST /api/auth/revoke</c>.</item>
///   <item>The <c>jti</c> of the access token is
///     persisted in <c>revoked_tokens</c>.</item>
///   <item>The next request that presents the same
///     token is rejected by the
///     <c>JwtRevocationValidator</c> with HTTP 401.</item>
/// </list>
/// The fixture reuses the integration WebApplicationFactory
/// (SQLite + real JwtBearer pipeline) so the
/// migration that creates <c>revoked_tokens</c> is
/// applied at boot, and the validator runs in the
/// same process as the API.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class JwtRevocationEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public JwtRevocationEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Revoke_Access_Token_Then_Reuse_It_Returns_401()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        // The first protected call is fine: the token is
        // still valid. The /api/workspaces/ endpoint is
        // the cheapest authenticated read the project
        // offers; it returns the list of workspaces the
        // caller is a member of.
        HttpResponseMessage before = await client.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        before.IsSuccessStatusCode.Should().BeTrue("the freshly-issued token must be accepted");

        // Pull the access token off the Authorization
        // header and read the jti + exp claims out of
        // it so the test can prove the row was written
        // with the matching values.
        string token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        JsonWebToken jwt = new(token);
        string jti = jwt.Id;
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow, "the test token must be alive");

        // Revoke the token via the public endpoint.
        HttpResponseMessage revokeResponse = await client.PostAsJsonAsync(
            "api/auth/revoke",
            new { reason = "logout from integration test" },
            TestContext.Current.CancellationToken);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The row is in the table with the right jti
        // BEFORE the second request goes out, so the
        // assertion proves the persistence happened
        // (the next test step re-uses the same factory
        // and the same on-disk DB).
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<
                Cardscape.Application.Abstractions.Persistence.IRevokedTokenRepository>();
            bool isRevoked = await repo.IsRevokedAsync(jti, TestContext.Current.CancellationToken);
            isRevoked.Should().BeTrue("the revoke call must persist the jti in revoked_tokens");
        }

        // The next request with the same token is
        // rejected. The bearer handler surfaces a 401
        // even though the JWT signature is still valid
        // — the validator's revocation check fails
        // before the controller runs.
        HttpClient reusedClient = _factory.CreateApiClient();
        reusedClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage after = await reusedClient.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a revoked JWT must be rejected by JwtRevocationValidator");
    }

    [Fact]
    public async Task Revoke_Without_Authorization_Returns_401()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/revoke", new { reason = "no auth" }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoke_Is_Idempotent()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "api/auth/revoke", new { reason = "first" }, TestContext.Current.CancellationToken);
        first.IsSuccessStatusCode.Should().BeTrue("the first revoke must succeed");

        // The second call with the SAME token must also
        // succeed: the handler short-circuits if the jti
        // is already revoked. The 204 is the contract.
        HttpResponseMessage second = await client.PostAsJsonAsync(
            "api/auth/revoke", new { reason = "second" }, TestContext.Current.CancellationToken);
        second.IsSuccessStatusCode.Should().BeTrue("a second revoke call must be a no-op success");
    }

    [Fact]
    public async Task Revoke_Long_Reason_Returns_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        string tooLong = new('x', 201);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/revoke", new { reason = tooLong }, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Legacy_Logout_Alias_Is_Not_Mapped()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/logout", new { reason = "obsolete alias" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        var register = new
        {
            email = $"revoke-{Guid.NewGuid():N}@cardscape.local",
            displayName = "Revoke Test",
            password = "Goodpass123!"
        };
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register, TestContext.Current.CancellationToken);
        if (!r.IsSuccessStatusCode)
        {
            string body = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException($"Register failed: {(int)r.StatusCode} {r.StatusCode} {body}");
        }
        using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string accessToken = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
