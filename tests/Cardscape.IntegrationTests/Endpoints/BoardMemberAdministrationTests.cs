using Cardscape.Domain.Boards;
using Cardscape.Tests.Common.Fixtures;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Trello-style board membership: board Admins, workspace managers and
/// instance admins manage the roster; plain members and observers
/// cannot add anyone; anyone can leave; the last board Admin is
/// always kept; only workspace members can be added.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardMemberAdministrationTests(CardscapeWebApplicationFactory factory)
{
    [Fact]
    public async Task BoardAdmin_ManagesTheRoster_AndPlainMembersCannot()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Board roster");
        Account boardAdmin = await JoinAsync(owner.Client, ws.Id, "board-admin");
        Account member = await JoinAsync(owner.Client, ws.Id, "board-member");
        Account colleague = await JoinAsync(owner.Client, ws.Id, "colleague");
        Account outsider = await RegisterAsync(factory, "outsider");
        Guid boardId = await CreateBoardAsync(boardAdmin.Client, ws.Id);

        (await AddAsync(boardAdmin.Client, boardId, member.Id, "member")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A plain member cannot add anyone, change roles or remove others.
        (await AddAsync(member.Client, boardId, colleague.Id, "admin")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.PatchAsJsonAsync($"api/boards/{boardId}/members/{member.Id}", new { role = "admin" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.Client.DeleteAsync($"api/boards/{boardId}/members/{boardAdmin.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Only workspace members can be added.
        (await AddAsync(boardAdmin.Client, boardId, outsider.Id, "member")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // The roster lists name, email and role.
        IReadOnlyList<BoardMemberDto> roster = await MembersAsync(member.Client, boardId);
        roster.Should().ContainSingle(m => m.UserId == member.Id && m.Email == member.Email && m.Role == BoardMemberRole.Member);
        roster.Single(m => m.UserId == boardAdmin.Id).DisplayName.Should().Be("board-admin");

        // The board admin changes roles and removes members.
        (await boardAdmin.Client.PatchAsJsonAsync($"api/boards/{boardId}/members/{member.Id}", new { role = "observer" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MembersAsync(boardAdmin.Client, boardId)).Single(m => m.UserId == member.Id).Role
            .Should().Be(BoardMemberRole.Observer);
        (await boardAdmin.Client.DeleteAsync($"api/boards/{boardId}/members/{member.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MembersAsync(boardAdmin.Client, boardId)).Should().NotContain(m => m.UserId == member.Id);

        // Access summary drives the UI.
        BoardMemberAccessDto access = (await boardAdmin.Client.GetFromJsonAsync<BoardMemberAccessDto>(
            $"api/boards/{boardId}/members/access", TestJson.Options, ct))!;
        access.CanManageMembers.Should().BeTrue();
        access.Role.Should().Be(BoardMemberRole.Admin);
    }

    [Fact]
    public async Task LastBoardAdmin_IsProtected_AndMembersCanLeave()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Last admin");
        Account boardAdmin = await JoinAsync(owner.Client, ws.Id, "board-admin");
        Account member = await JoinAsync(owner.Client, ws.Id, "leaver");
        Guid boardId = await CreateBoardAsync(boardAdmin.Client, ws.Id);
        (await AddAsync(boardAdmin.Client, boardId, member.Id, "member")).EnsureSuccessStatusCode();

        // Not even the workspace owner can demote or remove the last board Admin.
        HttpResponseMessage demote = await owner.Client.PatchAsJsonAsync(
            $"api/boards/{boardId}/members/{boardAdmin.Id}", new { role = "member" }, ct);
        demote.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await demote.Content.ReadAsStringAsync(ct)).Should().Contain("boards.members.last_admin");
        (await owner.Client.DeleteAsync($"api/boards/{boardId}/members/{boardAdmin.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await boardAdmin.Client.DeleteAsync($"api/boards/{boardId}/members/{boardAdmin.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A plain member leaves on their own.
        (await member.Client.DeleteAsync($"api/boards/{boardId}/members/{member.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MembersAsync(boardAdmin.Client, boardId)).Should().NotContain(m => m.UserId == member.Id);

        // Removing someone who is not on the board is a 404.
        (await boardAdmin.Client.DeleteAsync($"api/boards/{boardId}/members/{member.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WorkspaceOwnerAndInstanceAdmin_ManageBoardsTheyAreNotOn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Override");
        Account boardAdmin = await JoinAsync(owner.Client, ws.Id, "board-admin");
        Account member = await JoinAsync(owner.Client, ws.Id, "member");
        Account instanceAdmin = await RegisterAsync(factory, "instance-admin");
        await factory.Services.PromoteUserToAdminAsync(instanceAdmin.Email, ct);
        Guid boardId = await CreateBoardAsync(boardAdmin.Client, ws.Id);

        // The workspace owner is not on the board but manages it.
        (await AddAsync(owner.Client, boardId, member.Id, "observer")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.Client.GetAsync($"api/boards/{boardId}/members", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // An instance admin outside the workspace can too.
        (await instanceAdmin.Client.PatchAsJsonAsync($"api/boards/{boardId}/members/{member.Id}", new { role = "admin" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await MembersAsync(boardAdmin.Client, boardId)).Single(m => m.UserId == member.Id).Role
            .Should().Be(BoardMemberRole.Admin);

        // Now that there are two Admins, the creator can be demoted.
        (await instanceAdmin.Client.PatchAsJsonAsync($"api/boards/{boardId}/members/{boardAdmin.Id}", new { role = "member" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Stranger_CannotReadOrChangeTheRoster()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, "Strangers");
        Guid boardId = await CreateBoardAsync(owner.Client, ws.Id);
        Account stranger = await RegisterAsync(factory, "stranger");

        (await stranger.Client.GetAsync($"api/boards/{boardId}/members", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await stranger.Client.GetAsync($"api/boards/{boardId}/members/access", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await stranger.Client.DeleteAsync($"api/boards/{boardId}/members/{owner.Id}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<Account> JoinAsync(HttpClient manager, Guid workspaceId, string prefix)
    {
        Account account = await RegisterAsync(factory, prefix);
        string token = await InviteAsync(manager, workspaceId, account.Email, "member");
        (await account.Client.PostAsJsonAsync("api/invitations/accept", new { token }, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
        return account;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId, name = "Roster board", description = (string?)null, visibility = "private" },
            TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await response.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        return board.Id;
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid boardId, Guid userId, string role) =>
        client.PostAsJsonAsync($"api/boards/{boardId}/members", new { userId, role }, TestContext.Current.CancellationToken);

    private static async Task<IReadOnlyList<BoardMemberDto>> MembersAsync(HttpClient client, Guid boardId) =>
        (await client.GetFromJsonAsync<BoardMemberDto[]>(
            $"api/boards/{boardId}/members", TestJson.Options, TestContext.Current.CancellationToken))!;
}
