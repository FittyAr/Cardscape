using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Workspaces;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the workspace-invitation lifecycle over
/// HTTP: a workspace owner mints an invitation, the invitee sees it
/// in their pending list, redeems the cleartext token, and ends up
/// as a member of the workspace. The owner can also revoke a pending
/// invitation; redeeming a revoked/expired/wrong-email invitation
/// returns the right error.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class WorkspaceInvitationTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public WorkspaceInvitationTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Invite_Accept_Lifecycle_Adds_Invitee_As_Member()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Invites WS");

        string inviteeEmail = $"invitee-{Guid.NewGuid():N}@cardscape.local";

        HttpResponseMessage issueResp = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/",
            new { email = inviteeEmail, role = 1 }, TestContext.Current.CancellationToken);
        issueResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceInvitationIssuanceDto issued = (await issueResp.Content.ReadFromJsonAsync<WorkspaceInvitationIssuanceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        issued.CleartextToken.Should().NotBeNullOrWhiteSpace();

        // The invitee registers with the same email the invite was
        // sent to.
        HttpClient invitee = await CreateAuthenticatedClientAsync("Invitee", inviteeEmail);

        // Accept via the email-link-shaped endpoint.
        HttpResponseMessage acceptResp = await invitee.PostAsJsonAsync(
            "api/invitations/accept", new { token = issued.CleartextToken }, TestContext.Current.CancellationToken);
        acceptResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto acceptedWs = (await acceptResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        acceptedWs.Id.Should().Be(ws.Id);

        // The invitee is now a member.
        HttpResponseMessage membersResp = await invitee.GetAsync($"api/workspaces/{ws.Id}/members", TestContext.Current.CancellationToken);
        membersResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceMemberDto[]? members = await membersResp.Content.ReadFromJsonAsync<WorkspaceMemberDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        members.Should().NotBeNull().And.HaveCount(2);
        members!.Select(m => m.Email).Should().Contain(inviteeEmail);

        // Accepting a second time is idempotent (already a member).
        HttpResponseMessage second = await invitee.PostAsJsonAsync(
            "api/invitations/accept", new { token = issued.CleartextToken }, TestContext.Current.CancellationToken);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Accept_With_Wrong_Email_Returns_Forbidden()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "WrongEmail WS");

        HttpResponseMessage issueResp = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/",
            new { email = $"target-{Guid.NewGuid():N}@cardscape.local", role = 1 }, TestContext.Current.CancellationToken);
        WorkspaceInvitationIssuanceDto issued = (await issueResp.Content.ReadFromJsonAsync<WorkspaceInvitationIssuanceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        // A different user (different email) tries to redeem it.
        HttpClient wrongUser = await CreateAuthenticatedClientAsync("Wrong", $"wrong-{Guid.NewGuid():N}@cardscape.local");
        HttpResponseMessage acceptResp = await wrongUser.PostAsJsonAsync(
            "api/invitations/accept", new { token = issued.CleartextToken }, TestContext.Current.CancellationToken);
        acceptResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_Pending_Returns_Invitations_For_Current_User_Only()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Inbox WS");

        string targetEmail = $"target-{Guid.NewGuid():N}@cardscape.local";
        await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/",
            new { email = targetEmail, role = 1 }, TestContext.Current.CancellationToken);

        // A second, unrelated user shouldn't see anything.
        HttpClient stranger = await CreateAuthenticatedClientAsync("Stranger", $"stranger-{Guid.NewGuid():N}@cardscape.local");
        HttpResponseMessage strangerInbox = await stranger.GetAsync("api/invitations/pending", TestContext.Current.CancellationToken);
        strangerInbox.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceInvitationDto[]? strangerRows =
            await strangerInbox.Content.ReadFromJsonAsync<WorkspaceInvitationDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        strangerRows.Should().BeEmpty();

        // The target user does see it.
        HttpClient target = await CreateAuthenticatedClientAsync("Target", targetEmail);
        HttpResponseMessage targetInbox = await target.GetAsync("api/invitations/pending", TestContext.Current.CancellationToken);
        targetInbox.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceInvitationDto[]? targetRows =
            await targetInbox.Content.ReadFromJsonAsync<WorkspaceInvitationDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        targetRows.Should().NotBeNull().And.HaveCount(1);
        targetRows![0].Email.Should().Be(targetEmail);
        targetRows[0].WorkspaceId.Should().Be(ws.Id);
    }

    [Fact]
    public async Task Non_Owner_Cannot_Invite()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "NoMemberInvite WS");

        HttpClient other = await CreateAuthenticatedClientAsync("Other", $"other-{Guid.NewGuid():N}@cardscape.local");
        HttpResponseMessage issue = await other.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/",
            new { email = $"x-{Guid.NewGuid():N}@cardscape.local", role = 1 }, TestContext.Current.CancellationToken);
        issue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Revoke_Prevents_Accept()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("Owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Revoke WS");

        string targetEmail = $"target-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage issueResp = await owner.PostAsJsonAsync(
            $"api/workspaces/{ws.Id}/invitations/",
            new { email = targetEmail, role = 1 }, TestContext.Current.CancellationToken);
        WorkspaceInvitationIssuanceDto issued = (await issueResp.Content.ReadFromJsonAsync<WorkspaceInvitationIssuanceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpResponseMessage revoke = await owner.DeleteAsync(
            $"api/workspaces/{ws.Id}/invitations/{issued.Id}", TestContext.Current.CancellationToken);
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpClient target = await CreateAuthenticatedClientAsync("Target", targetEmail);
        HttpResponseMessage accept = await target.PostAsJsonAsync(
            "api/invitations/accept", new { token = issued.CleartextToken }, TestContext.Current.CancellationToken);
        accept.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Invite_Without_Auth_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"api/workspaces/{Guid.NewGuid()}/invitations/",
            new { email = "x@example.com", role = 1 }, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string displayNamePrefix, string? emailOverride = null)
    {
        HttpClient client = _factory.CreateApiClient();
        string email = emailOverride ?? $"{displayNamePrefix}-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, $"{displayNamePrefix} User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync("api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
    }

    // ── DTOs (local; mirror the API surface) ───────────────────

    public sealed record WorkspaceInvitationIssuanceDto(Guid Id, Guid WorkspaceId, string CleartextToken);
    public sealed record WorkspaceInvitationDto(
        Guid Id, Guid WorkspaceId, string WorkspaceName, string Email,
        WorkspaceRole Role, Guid InvitedBy, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt, string TokenPrefix);
    public sealed record WorkspaceMemberDto(Guid UserId, string Email, string DisplayName, WorkspaceRole Role, DateTimeOffset JoinedAt);
}
