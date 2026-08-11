namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Exercises the full vertical slice that the manual smoke test
/// covered: register -> workspace -> board -> list -> card -> comment
/// -> label -> star. Every step is verified via the real HTTP
/// pipeline (Program + DI + Wolverine + SQLite), so any regression
/// in the wiring, value-object validation, or EF mapping fails fast.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardLifecycleTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public BoardLifecycleTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_Smoke_Registers_And_Creates_Workspace_Board_List_Card()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"smoke-{Guid.NewGuid():N}@cardscape.local";
        AuthResponse auth = await RegisterAndLogin(client, email);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Workspace
        HttpResponseMessage createWs = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = "Smoke WS" }, TestContext.Current.CancellationToken);
        createWs.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto? ws = await createWs.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken);
        ws.Should().NotBeNull();
        ws!.MemberCount.Should().BeGreaterThan(0);

        // Board
        HttpResponseMessage createBoard = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId = ws.Id, name = "Smoke board", description = (string?)null, visibility = "private" }, TestContext.Current.CancellationToken);
        createBoard.IsSuccessStatusCode.Should().BeTrue();
        BoardDto? board = await createBoard.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, TestContext.Current.CancellationToken);
        board.Should().NotBeNull();

        // List
        HttpResponseMessage createList = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board!.Id, name = "Todo" }, TestContext.Current.CancellationToken);
        createList.IsSuccessStatusCode.Should().BeTrue();
        BoardListDto? list = await createList.Content.ReadFromJsonAsync<BoardListDto>(TestJson.Options, TestContext.Current.CancellationToken);
        list.Should().NotBeNull();

        // Card
        HttpResponseMessage createCard = await client.PostAsJsonAsync(
            "api/cards/", new { listId = list!.Id, title = "Hello", description = (string?)null }, TestContext.Current.CancellationToken);
        createCard.IsSuccessStatusCode.Should().BeTrue();
        CardDto? card = await createCard.Content.ReadFromJsonAsync<CardDto>(TestJson.Options, TestContext.Current.CancellationToken);
        card.Should().NotBeNull();
        card!.Title.Should().Be("Hello");
        card.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Card_Complete_And_Reopen_Toggles_IsCompleted()
    {
        HttpClient client = await CreateAuthenticatedClient();
        (_, _, BoardListDto list) = await SeedWorkspaceBoardListAsync(client);
        CardDto card = await CreateCardAsync(client, list.Id, "Complete me");

        HttpResponseMessage complete = await client.PostAsync($"api/cards/{card.Id}/complete", content: null, TestContext.Current.CancellationToken);
        complete.IsSuccessStatusCode.Should().BeTrue();
        CardDto? completed = await complete.Content.ReadFromJsonAsync<CardDto>(TestJson.Options, TestContext.Current.CancellationToken);
        completed!.IsCompleted.Should().BeTrue();

        HttpResponseMessage reopen = await client.PostAsync($"api/cards/{card.Id}/reopen", content: null, TestContext.Current.CancellationToken);
        reopen.IsSuccessStatusCode.Should().BeTrue();
        CardDto? reopened = await reopen.Content.ReadFromJsonAsync<CardDto>(TestJson.Options, TestContext.Current.CancellationToken);
        reopened!.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task Board_Star_And_Unstar_Toggles_IsStarred()
    {
        HttpClient client = await CreateAuthenticatedClient();
        (_, BoardDto board, _) = await SeedWorkspaceBoardListAsync(client);

        HttpResponseMessage star = await client.PostAsync($"api/boards/{board.Id}/star", content: null, TestContext.Current.CancellationToken);
        star.IsSuccessStatusCode.Should().BeTrue();
        (await star.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, TestContext.Current.CancellationToken))!.IsStarred.Should().BeTrue();

        HttpResponseMessage unstar = await client.DeleteAsync($"api/boards/{board.Id}/star", TestContext.Current.CancellationToken);
        unstar.IsSuccessStatusCode.Should().BeTrue();
        (await unstar.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, TestContext.Current.CancellationToken))!.IsStarred.Should().BeFalse();
    }

    [Fact]
    public async Task Card_Comment_Is_Added_And_Returned_In_List()
    {
        HttpClient client = await CreateAuthenticatedClient();
        (_, _, BoardListDto list) = await SeedWorkspaceBoardListAsync(client);
        CardDto card = await CreateCardAsync(client, list.Id, "Comment test");

        HttpResponseMessage add = await client.PostAsJsonAsync(
            $"api/cards/{card.Id}/comments/", new { body = "First comment from integration test" }, TestContext.Current.CancellationToken);
        add.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage list1 = await client.GetAsync($"api/cards/{card.Id}/comments/", TestContext.Current.CancellationToken);
        list1.IsSuccessStatusCode.Should().BeTrue();
        CommentDto[]? comments = await list1.Content.ReadFromJsonAsync<CommentDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        comments.Should().NotBeNull().And.HaveCount(1);
        comments![0].Body.Should().Be("First comment from integration test");
    }

    [Fact]
    public async Task Label_Attach_To_Card_Is_Reflected_In_Card_Dto()
    {
        HttpClient client = await CreateAuthenticatedClient();
        (_, BoardDto board, BoardListDto list) = await SeedWorkspaceBoardListAsync(client);
        CardDto card = await CreateCardAsync(client, list.Id, "Label test");

        HttpResponseMessage createLabel = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/labels/", new { name = "Bug", color = "#d73a4a" }, TestContext.Current.CancellationToken);
        createLabel.IsSuccessStatusCode.Should().BeTrue();
        LabelDto? label = await createLabel.Content.ReadFromJsonAsync<LabelDto>(TestJson.Options, TestContext.Current.CancellationToken);

        HttpResponseMessage attach = await client.PostAsync($"api/cards/{card.Id}/labels/{label!.Id}", content: null, TestContext.Current.CancellationToken);
        attach.IsSuccessStatusCode.Should().BeTrue();
        CardDto? attached = await attach.Content.ReadFromJsonAsync<CardDto>(TestJson.Options, TestContext.Current.CancellationToken);
        attached!.LabelCount.Should().Be(1);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client, $"it-{Guid.NewGuid():N}@cardscape.local");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client, string email)
    {
        RegisterRequest register = new(email, "Lifecycle User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
    }

    private static async Task<(WorkspaceDto, BoardDto, BoardListDto)> SeedWorkspaceBoardListAsync(HttpClient client)
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS-{Guid.NewGuid():N}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;

        HttpResponseMessage bdResp = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId = ws.Id, name = "Board", description = (string?)null, visibility = "private" });
        bdResp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await bdResp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;

        HttpResponseMessage lsResp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "List" });
        lsResp.IsSuccessStatusCode.Should().BeTrue();
        BoardListDto list = (await lsResp.Content.ReadFromJsonAsync<BoardListDto>(TestJson.Options))!;

        return (ws, board, list);
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<CardDto>(TestJson.Options))!;
    }
}
