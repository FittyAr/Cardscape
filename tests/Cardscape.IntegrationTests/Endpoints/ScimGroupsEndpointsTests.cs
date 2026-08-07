using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the SCIM v2 <c>/scim/v2/Groups</c>
/// surface (gap G3, plan §4.4). The per-workspace
/// <c>ScimToken</c> scopes the IdP to a single workspace, so
/// the list is always exactly one group and the
/// create-then-persist round-trip is the cleanest way to
/// assert the Groups side is actually wired (the audit
/// flagged the one-sided Users-only implementation as
/// non-conformant; these two tests close the gap).
/// </summary>
[Collection(CardscapeApi.Name)]
public class ScimGroupsEndpointsTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ScimGroupsEndpointsTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ListGroups_ForWorkspace_Returns200_WithWorkspaceGroup()
    {
        HttpClient admin = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(admin);
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Create a fresh workspace for this test.
        HttpResponseMessage wsResp = await admin.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("SCIM Groups List WS"), TestContext.Current.CancellationToken);
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // Issue a SCIM token scoped to that workspace.
        HttpResponseMessage issueResp = await admin.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", new { name = "Okta Groups" }, TestContext.Current.CancellationToken);
        issueResp.IsSuccessStatusCode.Should().BeTrue();
        ScimIssueResponseDto issue =
            (await issueResp.Content.ReadFromJsonAsync<ScimIssueResponseDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // The IdP now calls /scim/v2/Groups with the SCIM
        // bearer; the workspace id is the only group it
        // should see.
        HttpClient idp = _factory.CreateApiClient();
        idp.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issue.PlaintextToken);

        HttpResponseMessage listResp = await idp.GetAsync("scim/v2/Groups", TestContext.Current.CancellationToken);
        listResp.IsSuccessStatusCode.Should().BeTrue();
        ScimListResponseBody? list =
            (await listResp.Content.ReadFromJsonAsync<ScimListResponseBody>(TestJson.Options, TestContext.Current.CancellationToken))!;
        list.Should().NotBeNull();
        list!.TotalResults.Should().Be(1);
        list.Resources.Should().HaveCount(1);
        list.Resources[0].Id.Should().Be($"workspace-{ws.Id:D}");
        list.Resources[0].DisplayName.Should().Be("SCIM Groups List WS");
        list.Resources[0].Schemas
            .Should().Contain("urn:ietf:params:scim:schemas:core:2.0:Group");
        // The owner is the only member right after creation.
        list.Resources[0].Members.Should().HaveCount(1);
        list.Resources[0].Members[0].Value.Should().Be(auth.User.Id.ToString("D"));
    }

    [Fact]
    public async Task CreateGroup_WithMinimalPayload_Returns201_AndPersists()
    {
        HttpClient admin = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(admin);
        admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage wsResp = await admin.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("SCIM Groups Create WS"), TestContext.Current.CancellationToken);
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpResponseMessage issueResp = await admin.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/scim/tokens", new { name = "Okta Groups" }, TestContext.Current.CancellationToken);
        ScimIssueResponseDto issue =
            (await issueResp.Content.ReadFromJsonAsync<ScimIssueResponseDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpClient idp = _factory.CreateApiClient();
        idp.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", issue.PlaintextToken);

        // POST a minimal Group payload — only displayName
        // is required per RFC 7644 §3.3. The service
        // provisions a new workspace and returns 201.
        HttpResponseMessage createResp = await idp.PostAsJsonAsync(
            "scim/v2/Groups", new { displayName = "Provisioned From IdP" }, TestContext.Current.CancellationToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        ScimGroupBody? created =
            (await createResp.Content.ReadFromJsonAsync<ScimGroupBody>(TestJson.Options, TestContext.Current.CancellationToken))!;
        created.Should().NotBeNull();
        created!.DisplayName.Should().Be("Provisioned From IdP");
        created.Id.Should().StartWith("workspace-");
        // The new group is owned by the same user that
        // owned the token's workspace (see
        // ScimService.CreateGroupAsync), so they show up
        // as the only member.
        created.Members.Should().HaveCount(1);
        created.Members[0].Value.Should().Be(auth.User.Id.ToString("D"));

        // Persistence is verified by reading the new
        // workspace back through the admin API. The
        // per-workspace SCIM token would 404 on a GET of
        // the new group because the new group lives in a
        // different workspace than the token is scoped to
        // — that's by design. The 201 above is itself
        // proof the row was committed (CreateGroupAsync
        // calls SaveChangesAsync before returning), but
        // the admin GET is the end-to-end check.
        HttpResponseMessage wsListResp = await admin.GetAsync("api/workspaces/", TestContext.Current.CancellationToken);
        wsListResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto[]? all = await wsListResp.Content.ReadFromJsonAsync<WorkspaceDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        all.Should().NotBeNull();
        all!.Select(w => w.Name).Should().Contain("Provisioned From IdP");
    }

    // ── helpers ────────────────────────────────────────────────

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"scim-groups-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Scim Groups User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    // ── DTOs (local; mirror the API surface) ───────────────────

    public sealed record ScimTokenListItemDto(
        Guid Id, Guid WorkspaceId, string Name, string TokenPrefix,
        DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, bool IsRevoked);

    public sealed record ScimIssueResponseDto(ScimTokenListItemDto Token, string PlaintextToken);

    public sealed record ScimGroupBody(
        string Id,
        IReadOnlyList<string> Schemas,
        string DisplayName,
        IReadOnlyList<ScimGroupMemberBody> Members);

    public sealed record ScimGroupMemberBody(string Value, string? Display);

    public sealed record ScimListResponseBody(
        IReadOnlyList<string> Schemas,
        int TotalResults,
        int ItemsPerPage,
        int StartIndex,
        IReadOnlyList<ScimGroupBody> Resources);
}
