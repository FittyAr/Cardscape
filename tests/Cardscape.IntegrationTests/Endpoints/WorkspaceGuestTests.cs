using Cardscape.Domain.Boards;
using Cardscape.Domain.Workspaces;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Trello-style workspace guests end to end: a guest joins through an
/// invitation with the <c>guest</c> role, sees only the boards they were
/// added to (board grid, board reads, search, calendar), cannot create
/// boards, invite people or read the full roster, and can be at most a
/// board Member. Managers convert members to guests and back.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class WorkspaceGuestTests(CardscapeWebApplicationFactory factory)
{
    private const string SearchWord = "quokkaguest";

    private sealed record Setup(
        Account Owner,
        Account Member,
        Account Guest,
        WorkspaceDto Workspace,
        Guid SharedBoard,
        Guid WorkspaceBoard,
        Guid PublicBoard);

    /// <summary>
    /// Owner + full member + guest. The guest is a board Member of the
    /// private "Shared" board only; "Everyone" is workspace-visible and
    /// "Open" public, both without the guest.
    /// </summary>
    private async Task<Setup> SeedAsync(string name)
    {
        Account owner = await RegisterAsync(factory, "owner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner.Client, name);
        Account member = await JoinAsync(owner.Client, ws.Id, "member", "member");
        Account guest = await JoinAsync(owner.Client, ws.Id, "guest", "guest");
        Guid shared = await CreateBoardAsync(owner.Client, ws.Id, "Shared", "private");
        Guid everyone = await CreateBoardAsync(owner.Client, ws.Id, "Everyone", "workspace");
        Guid open = await CreateBoardAsync(owner.Client, ws.Id, "Open", "public");
        (await AddBoardMemberAsync(owner.Client, shared, guest.Id, "member")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        return new Setup(owner, member, guest, ws, shared, everyone, open);
    }

    [Fact]
    public async Task InvitedGuest_JoinsAsGuest_AndTheWorkspaceReportsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest invite");

        WorkspaceDto asGuest = (await s.Guest.Client.GetFromJsonAsync<WorkspaceDto>(
            $"api/workspaces/{s.Workspace.Id}", TestJson.Options, ct))!;
        asGuest.CallerRole.Should().Be(WorkspaceRole.Guest);

        WorkspaceDto[] listed = (await s.Guest.Client.GetFromJsonAsync<WorkspaceDto[]>("api/workspaces/", TestJson.Options, ct))!;
        listed.Single(w => w.Id == s.Workspace.Id).CallerRole.Should().Be(WorkspaceRole.Guest);

        IReadOnlyList<MemberRow> roster = await WorkspaceMembersAsync(s.Owner.Client, s.Workspace.Id);
        roster.Single(m => m.UserId == s.Guest.Id).Role.Should().Be(WorkspaceRole.Guest);

        // The guest badge on the board roster comes from the server.
        BoardMemberDto[] boardRoster = (await s.Owner.Client.GetFromJsonAsync<BoardMemberDto[]>(
            $"api/boards/{s.SharedBoard}/members", TestJson.Options, ct))!;
        boardRoster.Single(m => m.UserId == s.Guest.Id).IsWorkspaceGuest.Should().BeTrue();
        boardRoster.Single(m => m.UserId == s.Owner.Id).IsWorkspaceGuest.Should().BeFalse();
    }

    [Fact]
    public async Task Guest_SeesOnlyTheirBoards_InTheWorkspaceGridAndBoardReads()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest boards");

        BoardSummaryDto[] guestGrid = (await s.Guest.Client.GetFromJsonAsync<BoardSummaryDto[]>(
            $"api/boards/?workspaceId={s.Workspace.Id}", TestJson.Options, ct))!;
        guestGrid.Select(b => b.Id).Should().BeEquivalentTo([s.SharedBoard]);

        BoardSummaryDto[] memberGrid = (await s.Member.Client.GetFromJsonAsync<BoardSummaryDto[]>(
            $"api/boards/?workspaceId={s.Workspace.Id}", TestJson.Options, ct))!;
        memberGrid.Select(b => b.Id).Should().Contain([s.SharedBoard, s.WorkspaceBoard, s.PublicBoard]);

        // Workspace-visible boards are open to full members, not to guests.
        (await s.Guest.Client.GetAsync($"api/boards/{s.WorkspaceBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s.Member.Client.GetAsync($"api/boards/{s.WorkspaceBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await s.Guest.Client.GetAsync($"api/boards/{s.SharedBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await s.Guest.Client.GetAsync($"api/lists/?boardId={s.WorkspaceBoard}", ct)).IsSuccessStatusCode.Should().BeFalse();

        // Public boards stay readable for anyone signed in.
        (await s.Guest.Client.GetAsync($"api/boards/{s.PublicBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        // A stranger does not read a workspace-visible board either.
        Account stranger = await RegisterAsync(factory, "stranger");
        (await stranger.Client.GetAsync($"api/boards/{s.WorkspaceBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Guest_CannotCreateBoards_InviteOrManagePeople()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest limits");

        HttpResponseMessage create = await s.Guest.Client.PostAsJsonAsync(
            "api/boards/", new { workspaceId = s.Workspace.Id, name = "Mine", description = (string?)null, visibility = "private" }, ct);
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await create.Content.ReadAsStringAsync(ct)).Should().Contain("workspaces.guest_forbidden");

        (await s.Guest.Client.PostAsJsonAsync(
                $"api/workspaces/{s.Workspace.Id}/invitations/", new { email = NewEmail("friend"), role = "guest" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s.Guest.Client.PatchAsJsonAsync(
                $"api/workspaces/{s.Workspace.Id}/members/{s.Member.Id}", new { role = "observer" }, ct))
            .IsSuccessStatusCode.Should().BeFalse();
        (await s.Guest.Client.PostAsJsonAsync($"api/boards/{s.SharedBoard}/members", new { userId = s.Member.Id, role = "member" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s.Guest.Client.PostAsJsonAsync($"api/workspaces/{s.Workspace.Id}/rename", new { name = "Hijacked" }, ct))
            .IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Guest_SeesOnlyPeopleOnSharedBoards()
    {
        Setup s = await SeedAsync("Guest roster");

        IReadOnlyList<MemberRow> guestView = await WorkspaceMembersAsync(s.Guest.Client, s.Workspace.Id);
        IReadOnlyList<MemberRow> memberView = await WorkspaceMembersAsync(s.Member.Client, s.Workspace.Id);

        guestView.Select(m => m.UserId).Should().BeEquivalentTo([s.Owner.Id, s.Guest.Id]);
        memberView.Select(m => m.UserId).Should().Contain([s.Owner.Id, s.Member.Id, s.Guest.Id]);
    }

    [Fact]
    public async Task Search_ForAGuest_OnlyHitsTheirBoards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest search");
        Guid sharedCard = await CreateCardAsync(s.Owner.Client, s.SharedBoard, $"{SearchWord} shared");
        Guid hiddenCard = await CreateCardAsync(s.Owner.Client, s.WorkspaceBoard, $"{SearchWord} hidden");

        HashSet<Guid> guestHits = await SearchCardIdsAsync(s.Guest.Client, ct);
        HashSet<Guid> ownerHits = await SearchCardIdsAsync(s.Owner.Client, ct);

        guestHits.Should().Contain(sharedCard).And.NotContain(hiddenCard);
        ownerHits.Should().Contain([sharedCard, hiddenCard]);
    }

    [Fact]
    public async Task Calendar_ForAGuest_OnlyShowsTheirBoards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest calendar");
        DateTimeOffset from = new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddMonths(1);
        Guid sharedCard = await CreateCardAsync(s.Owner.Client, s.SharedBoard, "Due shared");
        Guid hiddenCard = await CreateCardAsync(s.Owner.Client, s.WorkspaceBoard, "Due hidden");
        foreach (Guid card in new[] { sharedCard, hiddenCard })
        {
            (await s.Owner.Client.PostAsJsonAsync($"api/cards/{card}/due-date", new { dueDate = from.AddDays(3) }, ct))
                .EnsureSuccessStatusCode();
        }

        string url = $"api/cards/calendar?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
        HashSet<Guid> guestCards = await CalendarCardIdsAsync(s.Guest.Client, url, ct);
        HashSet<Guid> ownerCards = await CalendarCardIdsAsync(s.Owner.Client, url, ct);

        guestCards.Should().Contain(sharedCard).And.NotContain(hiddenCard);
        ownerCards.Should().Contain([sharedCard, hiddenCard]);

        // Asking for the hidden board directly is refused as well.
        (await s.Guest.Client.GetAsync($"{url}&boardId={s.WorkspaceBoard}", ct)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task Guest_BoardRole_IsCappedAtMember()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest caps");

        HttpResponseMessage asAdmin = await AddBoardMemberAsync(s.Owner.Client, s.WorkspaceBoard, s.Guest.Id, "admin");
        asAdmin.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await asAdmin.Content.ReadAsStringAsync(ct)).Should().Contain("boards.members.guest_cannot_be_admin");

        (await s.Owner.Client.PatchAsJsonAsync($"api/boards/{s.SharedBoard}/members/{s.Guest.Id}", new { role = "admin" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Adding the guest to another board as a Member works, and the
        // board then shows up for them.
        (await AddBoardMemberAsync(s.Owner.Client, s.WorkspaceBoard, s.Guest.Id, "observer")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await s.Guest.Client.GetAsync($"api/boards/{s.WorkspaceBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MakingAMemberAGuest_CapsBoardAdmin_AndGuestsCanBePromotedBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest conversion");
        Guid memberBoard = await CreateBoardAsync(s.Member.Client, s.Workspace.Id, "Member board", "private");

        // Sole admin of a board: refused until someone else is an admin.
        HttpResponseMessage refused = await s.Owner.Client.PatchAsJsonAsync(
            $"api/workspaces/{s.Workspace.Id}/members/{s.Member.Id}", new { role = "guest" }, ct);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync(ct)).Should().Contain("workspaces.guest_last_board_admin");

        (await AddBoardMemberAsync(s.Owner.Client, memberBoard, s.Owner.Id, "admin")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await s.Owner.Client.PatchAsJsonAsync($"api/workspaces/{s.Workspace.Id}/members/{s.Member.Id}", new { role = "guest" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        BoardMemberDto[] roster = (await s.Owner.Client.GetFromJsonAsync<BoardMemberDto[]>(
            $"api/boards/{memberBoard}/members", TestJson.Options, ct))!;
        roster.Single(m => m.UserId == s.Member.Id).Role.Should().Be(BoardMemberRole.Member, "explicit memberships are kept, capped at Member");

        // As a guest they lose the workspace-visible board.
        BoardSummaryDto[] grid = (await s.Member.Client.GetFromJsonAsync<BoardSummaryDto[]>(
            $"api/boards/?workspaceId={s.Workspace.Id}", TestJson.Options, ct))!;
        grid.Select(b => b.Id).Should().BeEquivalentTo([memberBoard]);

        // Back to a full member.
        (await s.Owner.Client.PatchAsJsonAsync($"api/workspaces/{s.Workspace.Id}/members/{s.Guest.Id}", new { role = "member" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await s.Guest.Client.GetAsync($"api/boards/{s.WorkspaceBoard}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BoardMemberAccess_OffersGuestInvitesToWhoeverManagesTheBoard()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Setup s = await SeedAsync("Guest access");
        Guid memberBoard = await CreateBoardAsync(s.Member.Client, s.Workspace.Id, "Member board", "private");

        BoardMemberAccessDto owner = (await s.Owner.Client.GetFromJsonAsync<BoardMemberAccessDto>(
            $"api/boards/{memberBoard}/members/access", TestJson.Options, ct))!;
        BoardMemberAccessDto boardAdmin = (await s.Member.Client.GetFromJsonAsync<BoardMemberAccessDto>(
            $"api/boards/{memberBoard}/members/access", TestJson.Options, ct))!;

        BoardMemberAccessDto guest = (await s.Guest.Client.GetFromJsonAsync<BoardMemberAccessDto>(
            $"api/boards/{s.SharedBoard}/members/access", TestJson.Options, ct))!;

        owner.CanInviteGuests.Should().BeTrue();
        boardAdmin.CanManageMembers.Should().BeTrue();
        boardAdmin.CanInviteGuests.Should().BeTrue("a board Admin may invite guests to their board");
        guest.CanInviteGuests.Should().BeFalse();
    }

    // ── helpers ────────────────────────────────────────────────

    private sealed record MemberRow(Guid UserId, string Email, string DisplayName, WorkspaceRole Role, DateTimeOffset JoinedAt);

    private async Task<Account> JoinAsync(HttpClient manager, Guid workspaceId, string prefix, string role)
    {
        Account account = await RegisterAsync(factory, prefix);
        string token = await InviteAsync(manager, workspaceId, account.Email, role);
        (await account.Client.PostAsJsonAsync("api/invitations/accept", new { token }, TestContext.Current.CancellationToken))
            .EnsureSuccessStatusCode();
        return account;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId, string name, string visibility)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name, description = (string?)null, visibility }, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, TestContext.Current.CancellationToken))!.Id;
    }

    private static async Task<Guid> CreateCardAsync(HttpClient client, Guid boardId, string title)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpResponseMessage list = await client.PostAsJsonAsync("api/lists/", new { boardId, name = "Todo" }, ct);
        list.IsSuccessStatusCode.Should().BeTrue();
        Guid listId = (await list.Content.ReadFromJsonAsync<BoardListDto>(TestJson.Options, ct))!.Id;
        HttpResponseMessage card = await client.PostAsJsonAsync("api/cards/", new { listId, title, description = (string?)null }, ct);
        card.IsSuccessStatusCode.Should().BeTrue();
        return (await card.Content.ReadFromJsonAsync<CardDto>(TestJson.Options, ct))!.Id;
    }

    private static Task<HttpResponseMessage> AddBoardMemberAsync(HttpClient client, Guid boardId, Guid userId, string role) =>
        client.PostAsJsonAsync($"api/boards/{boardId}/members", new { userId, role }, TestContext.Current.CancellationToken);

    private static async Task<IReadOnlyList<MemberRow>> WorkspaceMembersAsync(HttpClient client, Guid workspaceId) =>
        (await client.GetFromJsonAsync<MemberRow[]>(
            $"api/workspaces/{workspaceId}/members", TestJson.Options, TestContext.Current.CancellationToken))!;

    private static async Task<HashSet<Guid>> SearchCardIdsAsync(HttpClient client, CancellationToken ct)
    {
        HttpResponseMessage response = await client.GetAsync($"api/search/?q={SearchWord}&kind=card&pageSize=50", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("items").EnumerateArray()
            .Where(hit => hit.TryGetProperty("cardId", out JsonElement id) && id.ValueKind == JsonValueKind.String)
            .Select(hit => hit.GetProperty("cardId").GetGuid())
            .ToHashSet();
    }

    private static async Task<HashSet<Guid>> CalendarCardIdsAsync(HttpClient client, string url, CancellationToken ct)
    {
        HttpResponseMessage response = await client.GetAsync(url, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("cardId").GetGuid()).ToHashSet();
    }
}
