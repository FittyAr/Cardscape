using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Security.Commands;
using Cardscape.Application.Security.Queries;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the API-token lifecycle over HTTP: a
/// user mints a token via the <c>POST /api/security/api-tokens</c>
/// endpoint, lists the tokens they own, revokes one, and a freshly
/// minted token survives a revoke of an older one.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class ApiTokenLifecycleTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ApiTokenLifecycleTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Issue_List_Revoke_Roundtrip()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        // Issue one token.
        HttpResponseMessage issue = await client.PostAsJsonAsync(
            "api/security/api-tokens/",
            new { name = "laptop", scopes = new[] { "read", "write" } });
        issue.IsSuccessStatusCode.Should().BeTrue();
        ApiTokenIssuanceDto? issued = await issue.Content.ReadFromJsonAsync<ApiTokenIssuanceDto>();
        issued.Should().NotBeNull();
        issued!.CleartextSecret.Should().NotBeNullOrWhiteSpace();

        // List should return one token (no cleartext in the list).
        HttpResponseMessage list = await client.GetAsync("api/security/api-tokens/");
        list.IsSuccessStatusCode.Should().BeTrue();
        ApiTokenSummaryDto[]? rows = await list.Content.ReadFromJsonAsync<ApiTokenSummaryDto[]>();
        rows.Should().NotBeNull().And.HaveCount(1);
        rows![0].Id.Should().Be(issued.Id);
        rows[0].Name.Should().Be("laptop");
        rows[0].SecretPrefix.Should().NotBeNullOrWhiteSpace();

        // Revoke.
        HttpResponseMessage revoke = await client.PostAsync(
            $"api/security/api-tokens/{issued.Id}/revoke",
            content: JsonContent.Create(new { reason = "rotated to a new laptop" }));
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // List now shows the revoked flag.
        ApiTokenSummaryDto[]? afterRevoke =
            await (await client.GetAsync("api/security/api-tokens/"))
                .Content.ReadFromJsonAsync<ApiTokenSummaryDto[]>();
        afterRevoke.Should().NotBeNull();
        afterRevoke!.Single().RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Revoke_As_Different_User_Returns_NotFound()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        HttpResponseMessage issue = await owner.PostAsJsonAsync(
            "api/security/api-tokens/",
            new { name = "laptop", scopes = new[] { "read" } });
        ApiTokenIssuanceDto? issued = (await issue.Content.ReadFromJsonAsync<ApiTokenIssuanceDto>())!;

        HttpClient intruder = await CreateAuthenticatedClientAsync();
        HttpResponseMessage revoke = await intruder.PostAsync(
            $"api/security/api-tokens/{issued.Id}/revoke",
            content: JsonContent.Create(new { reason = "hijack" }));
        revoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Issue_Without_Token_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage issue = await client.PostAsJsonAsync(
            "api/security/api-tokens/",
            new { name = "laptop", scopes = new[] { "read" } });
        issue.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Issue_With_Empty_Name_Returns_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage issue = await client.PostAsJsonAsync(
            "api/security/api-tokens/",
            new { name = "", scopes = new[] { "read" } });
        issue.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"apikey-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "API Key User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
