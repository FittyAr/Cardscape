using Cardscape.Domain.Workspaces;
using Cardscape.Tests.Common.Fixtures;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// <c>POST /api/workspaces/{id}/transfer-ownership</c> (owner or
/// instance admin only) and the orphan protection that refuses to
/// deactivate, delete or anonymise an account that still owns a
/// workspace other active members use.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class WorkspaceOwnershipTransferTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public async Task Owner_TransfersOwnership_AndStaysAsAdmin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "xfer-owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Transfer me");
        Account admin = await JoinAsync(owner.Client, ws.Id, "xfer-admin", "admin");
        Account member = await JoinAsync(owner.Client, ws.Id, "xfer-member", "member");
        Account stranger = await RegisterAsync(factory, "xfer-stranger");

        // A workspace Admin who is not the owner may not transfer.
        (await TransferAsync(admin.Client, ws.Id, admin.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Only members can become the owner.
        HttpResponseMessage notMember = await TransferAsync(owner.Client, ws.Id, stranger.Id);
        notMember.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodeAsync(notMember)).Should().Be("workspaces.ownership.not_member");

        HttpResponseMessage same = await TransferAsync(owner.Client, ws.Id, owner.Id);
        same.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(same)).Should().Be("workspaces.ownership.same_owner");

        HttpResponseMessage transfer = await TransferAsync(owner.Client, ws.Id, member.Id);
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);
        WorkspaceDto updated = (await transfer.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, ct))!;
        updated.OwnerId.Should().Be(member.Id);

        IReadOnlyList<MemberRow> members = await MembersAsync(member.Client, ws.Id);
        members.Single(m => m.UserId == member.Id).Role.Should().Be(WorkspaceRole.Admin);
        members.Single(m => m.UserId == owner.Id).Role.Should().Be(WorkspaceRole.Admin);

        // The previous owner is now an ordinary Admin: no more transfers.
        (await TransferAsync(owner.Client, ws.Id, admin.Id)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // ...and the new owner is protected from removal.
        (await owner.Client.DeleteAsync($"api/workspaces/{ws.Id}/members/{member.Id}", ct))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task InstanceAdmin_CanTransferAWorkspaceTheyDoNotBelongTo()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "xfer-owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Rescued");
        Account member = await JoinAsync(owner.Client, ws.Id, "xfer-member", "observer");
        Account instanceAdmin = await RegisterAsync(factory, "xfer-root");
        await factory.Services.PromoteUserToAdminAsync(instanceAdmin.Email, ct);

        HttpResponseMessage response = await TransferAsync(instanceAdmin.Client, ws.Id, member.Id);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await MembersAsync(owner.Client, ws.Id)).Single(m => m.UserId == member.Id).Role
            .Should().Be(WorkspaceRole.Admin);
    }

    [Fact]
    public async Task OwnerOfASharedWorkspace_CannotBeDeactivatedDeletedOrAnonymised_UntilOwnershipMoves()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "orphan-owner");
        string sharedName = $"Shared {Guid.NewGuid():N}"[..20];
        WorkspaceDto shared = await CreateWorkspaceAsync(owner.Client, sharedName);
        await CreateWorkspaceAsync(owner.Client, "Solo notes");
        Account member = await JoinAsync(owner.Client, shared.Id, "orphan-member", "member");
        Account instanceAdmin = await RegisterAsync(factory, "orphan-root");
        await factory.Services.PromoteUserToAdminAsync(instanceAdmin.Email, ct);

        HttpResponseMessage[] refused =
        [
            await instanceAdmin.Client.PostAsync($"api/admin/users/{owner.Id}/deactivate", null, ct),
            await instanceAdmin.Client.DeleteAsync($"api/admin/users/{owner.Id}", ct),
            await instanceAdmin.Client.PostAsync($"api/admin/users/{owner.Id}/anonymise", null, ct),
            await owner.Client.DeleteAsync("api/users/me", ct),
        ];
        foreach (HttpResponseMessage response in refused)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            doc.RootElement.GetProperty("code").GetString().Should().Be("users.owns_workspaces");
            doc.RootElement.GetProperty("detail").GetString().Should().Contain(sharedName);
            doc.RootElement.GetProperty("workspaces").EnumerateArray().Select(e => e.GetString())
                .Should().Equal(sharedName);
        }

        // The account is untouched and still works.
        (await owner.Client.GetAsync("api/workspaces", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await TransferAsync(owner.Client, shared.Id, member.Id)).StatusCode.Should().Be(HttpStatusCode.OK);

        // With only a sole-member workspace left, self-deletion goes through.
        (await owner.Client.DeleteAsync("api/users/me", ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MembersAsync(member.Client, shared.Id)).Should().NotContain(m => m.UserId == owner.Id,
            "the soft-delete drops memberships of workspaces the user no longer owns");
    }

    [Fact]
    public async Task OwnerOfOnlySoleMemberWorkspaces_CanStillBeDeleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "solo-owner");
        await CreateWorkspaceAsync(owner.Client, "Just me");
        Account instanceAdmin = await RegisterAsync(factory, "solo-root");
        await factory.Services.PromoteUserToAdminAsync(instanceAdmin.Email, ct);

        (await instanceAdmin.Client.PostAsync($"api/admin/users/{owner.Id}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await instanceAdmin.Client.DeleteAsync($"api/admin/users/{owner.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<Account> JoinAsync(HttpClient manager, Guid workspaceId, string prefix, string role)
    {
        Account account = await RegisterAsync(factory, prefix);
        string token = await InviteAsync(manager, workspaceId, account.Email, role);
        (await account.Client.PostAsJsonAsync("api/invitations/accept", new { token }, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
        return account;
    }

    private static Task<HttpResponseMessage> TransferAsync(HttpClient client, Guid workspaceId, Guid userId) =>
        client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/transfer-ownership", new { userId }, TestContext.Current.CancellationToken);

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static async Task<IReadOnlyList<MemberRow>> MembersAsync(HttpClient client, Guid workspaceId) =>
        (await client.GetFromJsonAsync<MemberRow[]>(
            $"api/workspaces/{workspaceId}/members", TestJson.Options, TestContext.Current.CancellationToken))!;

    private sealed record MemberRow(Guid UserId, string Email, string DisplayName, WorkspaceRole Role, DateTimeOffset JoinedAt);
}
