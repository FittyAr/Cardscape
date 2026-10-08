using Cardscape.Application.Audit;
using Cardscape.Domain.Audit;
using Cardscape.Domain.Members;
using Cardscape.Tests.Common.Fixtures;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// The administration audit log: account, workspace and board membership
/// changes leave one entry each with the signed-in caller as actor, the
/// instance log is for instance admins only, a workspace's log is for its
/// managers and never shows other workspaces, and anonymising a person
/// scrubs their name from the log.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class AuditLogEndpointTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public async Task AdminAccountActions_AreRecorded_WithTheAdminAsActor()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account admin = await AdminAsync("audit-admin");
        Account target = await RegisterAsync(factory, "audit-target");

        (await admin.Client.PostAsync($"api/admin/users/{target.Id}/admin", null, ct)).EnsureSuccessStatusCode();
        (await admin.Client.PostAsync($"api/admin/users/{target.Id}/unadmin", null, ct)).EnsureSuccessStatusCode();
        (await admin.Client.PostAsync($"api/admin/users/{target.Id}/deactivate", null, ct)).EnsureSuccessStatusCode();
        (await admin.Client.PostAsync($"api/admin/users/{target.Id}/reactivate", null, ct)).EnsureSuccessStatusCode();

        IReadOnlyList<AuditEntryDto> entries = await AdminLogAsync(admin.Client, $"targetId={target.Id}");
        entries.Select(e => e.Action).Should().ContainInOrder(
            AuditActions.UserReactivated,
            AuditActions.UserDeactivated,
            AuditActions.UserAdminRevoked,
            AuditActions.UserAdminGranted,
            AuditActions.UserRegistered);
        AuditEntryDto granted = entries.Single(e => e.Action == AuditActions.UserAdminGranted);
        granted.ActorUserId.Should().Be(admin.Id);
        granted.ActorName.Should().Be("audit-admin");
        granted.TargetType.Should().Be(AuditTargetTypes.User);
        granted.TargetName.Should().Be("audit-target");

        // A self-service sign-up names the new user as the actor.
        AuditEntryDto registered = entries.Single(e => e.Action == AuditActions.UserRegistered);
        registered.ActorUserId.Should().Be(target.Id);

        // Filters: action prefix, actor, and search.
        (await AdminLogAsync(admin.Client, $"targetId={target.Id}&action=workspace.")).Should().BeEmpty();
        (await AdminLogAsync(admin.Client, $"actorId={admin.Id}&action=user.admin"))
            .Select(e => e.Action).Should().BeEquivalentTo([AuditActions.UserAdminRevoked, AuditActions.UserAdminGranted]);
        (await AdminLogAsync(admin.Client, $"targetId={target.Id}&search=AUDIT-TARG")).Should().NotBeEmpty();
        (await AdminLogAsync(admin.Client, $"targetId={target.Id}&search=nobody-by-that-name")).Should().BeEmpty();
    }

    [Fact]
    public async Task CreatingAnAccount_IsOneEntry_AndOutOfRequestChangesAreBySystem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account admin = await AdminAsync("creator-admin");
        string email = NewEmail("created");

        HttpResponseMessage created = await admin.Client.PostAsJsonAsync(
            "api/admin/users/", new { email, displayName = "Created Person", isAdmin = false }, ct);
        created.EnsureSuccessStatusCode();
        Guid userId = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("userId").GetGuid();

        IReadOnlyList<AuditEntryDto> entries = await AdminLogAsync(admin.Client, $"targetId={userId}");
        AuditEntryDto entry = entries.Should().ContainSingle(
            "the verification and temporary password are part of the creation").Subject;
        entry.Action.Should().Be(AuditActions.UserCreatedByAdmin);
        entry.ActorUserId.Should().Be(admin.Id);
        entry.TargetName.Should().Be("Created Person");

        // A change made outside any request (here: a test fixture) has no actor.
        await factory.Services.PromoteUserToAdminAsync(email, ct);
        AuditEntryDto system = (await AdminLogAsync(admin.Client, $"targetId={userId}&action=user.admin_granted")).Single();
        system.ActorUserId.Should().BeNull();
        system.ActorName.Should().Be(AuditEntry.SystemActorName);
    }

    [Fact]
    public async Task WorkspaceMembership_IsRecorded_WithRolesAndInvitations()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "ws-owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Audited workspace");
        Account member = await RegisterAsync(factory, "ws-joiner");
        string token = await InviteAsync(owner.Client, ws.Id, member.Email, "member");
        (await member.Client.PostAsJsonAsync("api/invitations/accept", new { token }, ct)).EnsureSuccessStatusCode();
        string revokedEmail = NewEmail("never-joined");
        await InviteAsync(owner.Client, ws.Id, revokedEmail, "observer");
        Guid pendingId = await PendingInvitationAsync(owner.Client, ws.Id, revokedEmail);
        (await owner.Client.DeleteAsync($"api/workspaces/{ws.Id}/invitations/{pendingId}", ct)).EnsureSuccessStatusCode();
        (await owner.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{member.Id}", new { role = "admin" }, ct))
            .EnsureSuccessStatusCode();
        (await owner.Client.PostAsJsonAsync($"api/workspaces/{ws.Id}/transfer-ownership", new { userId = member.Id }, ct))
            .EnsureSuccessStatusCode();
        (await member.Client.DeleteAsync($"api/workspaces/{ws.Id}/members/{owner.Id}", ct)).EnsureSuccessStatusCode();

        IReadOnlyList<AuditEntryDto> entries = await WorkspaceLogAsync(member.Client, ws.Id);
        entries.Select(e => e.Action).Should().Equal(
            AuditActions.WorkspaceMemberRemoved,
            AuditActions.WorkspaceOwnershipTransferred,
            AuditActions.WorkspaceMemberRoleChanged,
            AuditActions.WorkspaceInvitationRevoked,
            AuditActions.WorkspaceInvitationIssued,
            AuditActions.WorkspaceInvitationAccepted,
            AuditActions.WorkspaceInvitationIssued);
        entries.Should().OnlyContain(e => e.WorkspaceId == ws.Id && e.WorkspaceName == "Audited workspace");

        AuditEntryDto roleChanged = entries.Single(e => e.Action == AuditActions.WorkspaceMemberRoleChanged);
        roleChanged.ActorUserId.Should().Be(owner.Id);
        roleChanged.TargetName.Should().Be("ws-joiner");
        roleChanged.Details.Should().Contain("from", "Member").And.Contain("to", "Admin");

        AuditEntryDto accepted = entries.Single(e => e.Action == AuditActions.WorkspaceInvitationAccepted);
        accepted.ActorUserId.Should().Be(member.Id);
        accepted.Details.Should().Contain("role", "Member");

        AuditEntryDto revoked = entries.Single(e => e.Action == AuditActions.WorkspaceInvitationRevoked);
        revoked.TargetType.Should().Be(AuditTargetTypes.Invitation);
        revoked.TargetName.Should().Be(revokedEmail);
        revoked.Details.Should().Contain("role", "Observer");

        AuditEntryDto transferred = entries.Single(e => e.Action == AuditActions.WorkspaceOwnershipTransferred);
        transferred.ActorUserId.Should().Be(owner.Id);
        transferred.TargetUserId().Should().Be(member.Id);
        transferred.Details.Should().Contain("previousOwnerName", "ws-owner");

        AuditEntryDto removed = entries.Single(e => e.Action == AuditActions.WorkspaceMemberRemoved);
        removed.ActorUserId.Should().Be(member.Id);
        removed.Details.Should().Contain("role", "Admin");
    }

    [Fact]
    public async Task BoardMembership_IsRecorded_InTheWorkspaceLog()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "board-owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Board audit");
        Account member = await RegisterAsync(factory, "board-joiner");
        string token = await InviteAsync(owner.Client, ws.Id, member.Email, "member");
        (await member.Client.PostAsJsonAsync("api/invitations/accept", new { token }, ct)).EnsureSuccessStatusCode();
        HttpResponseMessage board = await owner.Client.PostAsJsonAsync(
            "api/boards/", new { workspaceId = ws.Id, name = "Audited board", description = (string?)null, visibility = "private" }, ct);
        board.EnsureSuccessStatusCode();
        Guid boardId = (await board.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, ct))!.Id;

        (await owner.Client.PostAsJsonAsync($"api/boards/{boardId}/members", new { userId = member.Id, role = "member" }, ct))
            .EnsureSuccessStatusCode();
        (await owner.Client.PatchAsJsonAsync($"api/boards/{boardId}/members/{member.Id}", new { role = "observer" }, ct))
            .EnsureSuccessStatusCode();
        (await owner.Client.DeleteAsync($"api/boards/{boardId}/members/{member.Id}", ct)).EnsureSuccessStatusCode();

        IReadOnlyList<AuditEntryDto> entries = await WorkspaceLogAsync(owner.Client, ws.Id, "action=board.");
        entries.Select(e => e.Action).Should().Equal(
            AuditActions.BoardMemberRemoved, AuditActions.BoardMemberRoleChanged, AuditActions.BoardMemberAdded);
        entries.Should().OnlyContain(e => e.BoardId == boardId && e.BoardName == "Audited board"
            && e.WorkspaceId == ws.Id && e.ActorUserId == owner.Id && e.TargetName == "board-joiner");
        entries.Single(e => e.Action == AuditActions.BoardMemberRoleChanged).Details
            .Should().Contain("from", "Member").And.Contain("to", "Observer");
        entries.Single(e => e.Action == AuditActions.BoardMemberRemoved).Details.Should().Contain("role", "Observer");
    }

    [Fact]
    public async Task OnlyAdmins_ReadTheInstanceLog_AndManagers_TheirWorkspaceLog()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "scoped-owner");
        WorkspaceDto mine = await CreateWorkspaceAsync(owner.Client, "Mine");
        Account other = await RegisterAsync(factory, "scoped-other");
        WorkspaceDto theirs = await CreateWorkspaceAsync(other.Client, "Theirs");
        await InviteAsync(other.Client, theirs.Id, NewEmail("their-guest"), "member");
        Account plain = await RegisterAsync(factory, "scoped-plain");
        string token = await InviteAsync(owner.Client, mine.Id, plain.Email, "member");
        (await plain.Client.PostAsJsonAsync("api/invitations/accept", new { token }, ct)).EnsureSuccessStatusCode();

        // The instance log is for instance admins only.
        (await owner.Client.GetAsync("api/admin/audit", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.CreateClient().GetAsync("api/admin/audit", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // A plain member cannot read the workspace log; an outsider neither.
        (await plain.Client.GetAsync($"api/workspaces/{mine.Id}/audit", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.GetAsync($"api/workspaces/{theirs.Id}/audit", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The manager sees only their workspace, whatever they ask for.
        IReadOnlyList<AuditEntryDto> entries = await WorkspaceLogAsync(owner.Client, mine.Id, $"actorId={other.Id}");
        entries.Should().BeEmpty();
        entries = await WorkspaceLogAsync(owner.Client, mine.Id);
        entries.Should().NotBeEmpty().And.OnlyContain(e => e.WorkspaceId == mine.Id);

        // An instance admin can read any workspace's log.
        Account admin = await AdminAsync("scoped-admin");
        (await WorkspaceLogAsync(admin.Client, theirs.Id)).Should().ContainSingle(e => e.Action == AuditActions.WorkspaceInvitationIssued);
    }

    [Fact]
    public async Task Anonymising_ScrubsTheNameAndEmail_FromEarlierEntries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account admin = await AdminAsync("scrub-admin");
        Account owner = await RegisterAsync(factory, "scrub-owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Scrubbed");
        Account person = await RegisterAsync(factory, "forget-me");
        string token = await InviteAsync(owner.Client, ws.Id, person.Email, "member");
        (await person.Client.PostAsJsonAsync("api/invitations/accept", new { token }, ct)).EnsureSuccessStatusCode();
        // The person acts too, so they appear as an actor.
        string invited = NewEmail("invited-by-person");
        (await owner.Client.PatchAsJsonAsync($"api/workspaces/{ws.Id}/members/{person.Id}", new { role = "admin" }, ct))
            .EnsureSuccessStatusCode();
        await InviteAsync(person.Client, ws.Id, invited, "member");

        (await admin.Client.PostAsync($"api/admin/users/{person.Id}/anonymise", null, ct)).EnsureSuccessStatusCode();

        IReadOnlyList<AuditEntryDto> entries = await WorkspaceLogAsync(owner.Client, ws.Id);
        string json = JsonSerializer.Serialize(entries);
        json.Should().NotContain("forget-me");
        entries.Single(e => e.Action == AuditActions.WorkspaceInvitationIssued && e.ActorUserId == person.Id)
            .ActorName.Should().Be(User.AnonymisedDisplayName);
        entries.Single(e => e.Action == AuditActions.WorkspaceInvitationIssued && e.ActorUserId == owner.Id)
            .TargetName.Should().Be(User.AnonymisedDisplayName, "the invitation went to the anonymised person's address");
        entries.Single(e => e.Action == AuditActions.WorkspaceMemberRemoved).TargetName.Should().Be(User.AnonymisedDisplayName);
        (await AdminLogAsync(admin.Client, $"targetId={person.Id}&action=user.anonymised")).Single()
            .ActorUserId.Should().Be(admin.Id);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<Account> AdminAsync(string prefix)
    {
        Account admin = await RegisterAsync(factory, prefix);
        await factory.Services.PromoteUserToAdminAsync(admin.Email, TestContext.Current.CancellationToken);
        return admin;
    }

    private static async Task<IReadOnlyList<AuditEntryDto>> AdminLogAsync(HttpClient client, string query)
    {
        HttpResponseMessage response = await client.GetAsync(
            $"api/admin/audit?{query}&pageSize=100", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuditEntryPageDto>(TestJson.Options, TestContext.Current.CancellationToken))!.Items;
    }

    private static async Task<IReadOnlyList<AuditEntryDto>> WorkspaceLogAsync(HttpClient client, Guid workspaceId, string query = "")
    {
        HttpResponseMessage response = await client.GetAsync(
            $"api/workspaces/{workspaceId}/audit?pageSize=100&{query}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuditEntryPageDto>(TestJson.Options, TestContext.Current.CancellationToken))!.Items;
    }

    private static async Task<Guid> PendingInvitationAsync(HttpClient manager, Guid workspaceId, string email)
    {
        using JsonDocument doc = JsonDocument.Parse(await manager.GetStringAsync(
            $"api/workspaces/{workspaceId}/invitations/", TestContext.Current.CancellationToken));
        return doc.RootElement.EnumerateArray()
            .Single(e => string.Equals(e.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase))
            .GetProperty("id").GetGuid();
    }
}

internal static class AuditEntryDtoTestExtensions
{
    public static Guid? TargetUserId(this AuditEntryDto entry) =>
        entry.TargetType == AuditTargetTypes.User ? entry.TargetId : null;
}
