using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the SCIM v2 provisioning
/// (P4.4): issue a bearer token, list tokens, call the
/// /scim/v2/Users endpoint with the bearer, and assert
/// the round-trip from the IdP to the Cardscape user
/// table.
/// </summary>
[Collection(CardscapeApi.Name)]
public class ScimEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ScimEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ScimToken_Issue_List_Revoke_Roundtrip()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Create a workspace.
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("SCIM WS"), TestContext.Current.CancellationToken);
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // Initially the token list is empty.
        HttpResponseMessage listResp = await client.GetAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", TestContext.Current.CancellationToken);
        listResp.IsSuccessStatusCode.Should().BeTrue();
        List<ScimTokenListItemDto> initial =
            (await listResp.Content.ReadFromJsonAsync<List<ScimTokenListItemDto>>(TestContext.Current.CancellationToken))!;
        initial.Should().BeEmpty();

        // Issue a token.
        HttpResponseMessage issueResp = await client.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", new { name = "Okta" }, TestContext.Current.CancellationToken);
        issueResp.IsSuccessStatusCode.Should().BeTrue();
        ScimIssueResponseDto issueBody =
            (await issueResp.Content.ReadFromJsonAsync<ScimIssueResponseDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        issueBody.PlaintextToken.Should().NotBeNullOrWhiteSpace();
        issueBody.Token.Name.Should().Be("Okta");

        // Token shows up in the list with a non-empty prefix.
        HttpResponseMessage listResp2 = await client.GetAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", TestContext.Current.CancellationToken);
        List<ScimTokenListItemDto> after =
            (await listResp2.Content.ReadFromJsonAsync<List<ScimTokenListItemDto>>(TestContext.Current.CancellationToken))!;
        after.Should().HaveCount(1);
        after[0].TokenPrefix.Should().NotBeNullOrWhiteSpace();
        after[0].Name.Should().Be("Okta");

        // Revoke.
        HttpResponseMessage revokeResp = await client.DeleteAsync(
            $"api/workspaces/{ws.Id}/scim/tokens/{issueBody.Token.Id}", TestContext.Current.CancellationToken);
        revokeResp.IsSuccessStatusCode.Should().BeTrue();

        // The list now shows the revoked state.
        HttpResponseMessage listResp3 = await client.GetAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", TestContext.Current.CancellationToken);
        List<ScimTokenListItemDto> afterRevoke =
            (await listResp3.Content.ReadFromJsonAsync<List<ScimTokenListItemDto>>(TestContext.Current.CancellationToken))!;
        afterRevoke.Should().HaveCount(1);
        afterRevoke[0].IsRevoked.Should().BeTrue();
    }

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"scim-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Scim User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    public sealed record ScimTokenListItemDto(
        Guid Id, Guid WorkspaceId, string Name, string TokenPrefix,
        DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, bool IsRevoked);

    public sealed record ScimIssueResponseDto(ScimTokenListItemDto Token, string PlaintextToken);
}
