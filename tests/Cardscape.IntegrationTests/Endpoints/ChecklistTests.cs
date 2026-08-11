using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class ChecklistTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public ChecklistTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_Checklist_Adds_It_To_The_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "create cl");

        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "Today" }, TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        ChecklistDto? created = await resp.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);
        created!.Title.Should().Be("Today");
        created.CardId.Should().Be(seed.CardId);
    }

    [Fact]
    public async Task List_Returns_Empty_For_Fresh_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "list empty");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        ChecklistDto[]? arr =
            (await resp.Content.ReadFromJsonAsync<ChecklistDto[]>(TestContext.Current.CancellationToken))!;
        arr.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_Item_Then_Toggle_Updates_Progress()
    {
        // BETA-8-API-#3 — see test-results/r8/r8-report.md.
        // The endpoint POST /api/checklists/{id}/items now
        // returns the single ChecklistItemDto (the resource
        // that was just created), not the parent ChecklistDto.
        // To assert on the parent's CompletedCount /
        // TotalCount after the toggle, we re-read the
        // checklist via the GET (which still returns the
        // full ChecklistDto).
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "add+toggle");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "todos" }, TestContext.Current.CancellationToken);
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);

        HttpResponseMessage withItem = await client.PostAsJsonAsync(
            $"api/checklists/{cl!.Id}/items/", new { text = "first" }, TestContext.Current.CancellationToken);
        ChecklistItemDto? addedItem = await withItem.Content.ReadFromJsonAsync<ChecklistItemDto>(TestContext.Current.CancellationToken);
        Guid itemId = addedItem!.Id;

        HttpResponseMessage toggled = await client.PatchAsync(
            $"api/checklists/{cl.Id}/items/{itemId}/toggle", content: null, TestContext.Current.CancellationToken);
        toggled.IsSuccessStatusCode.Should().BeTrue();

        // Re-read the checklist to assert the parent's
        // progress counters reflect the toggle.
        HttpResponseMessage listed = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/", TestContext.Current.CancellationToken);
        ChecklistDto[]? arr =
            (await listed.Content.ReadFromJsonAsync<ChecklistDto[]>(TestContext.Current.CancellationToken))!;
        ChecklistDto after = arr.Should().ContainSingle().Subject;
        after.CompletedCount.Should().Be(1);
        after.TotalCount.Should().Be(1);
        after.Items[0].IsCompleted.Should().BeTrue();
        after.Items[0].Id.Should().Be(itemId);
    }

    [Fact]
    public async Task Delete_Checklist_Removes_It()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "delete cl");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "t" }, TestContext.Current.CancellationToken);
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);

        HttpResponseMessage deleted = await client.DeleteAsync(
            $"api/checklists/{cl!.Id}/", TestContext.Current.CancellationToken);
        deleted.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage listed = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/", TestContext.Current.CancellationToken);
        ChecklistDto[]? arr =
            (await listed.Content.ReadFromJsonAsync<ChecklistDto[]>(TestContext.Current.CancellationToken))!;
        arr.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Item_Removes_Single_Item()
    {
        // BETA-8-API-#3 — see test-results/r8/r8-report.md.
        // The endpoint POST /api/checklists/{id}/items now
        // returns the single ChecklistItemDto. The DELETE
        // endpoint returns the parent ChecklistDto (no
        // shape change there). This test re-reads via the
        // GET to assert the parent is now empty.
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "delete item");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "t" }, TestContext.Current.CancellationToken);
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);
        HttpResponseMessage withItem = await client.PostAsJsonAsync(
            $"api/checklists/{cl!.Id}/items/", new { text = "x" }, TestContext.Current.CancellationToken);
        ChecklistItemDto? addedItem = await withItem.Content.ReadFromJsonAsync<ChecklistItemDto>(TestContext.Current.CancellationToken);

        HttpResponseMessage delItem = await client.DeleteAsync(
            $"api/checklists/{cl.Id}/items/{addedItem!.Id}", TestContext.Current.CancellationToken);
        delItem.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage listed = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/", TestContext.Current.CancellationToken);
        ChecklistDto[]? arr =
            (await listed.Content.ReadFromJsonAsync<ChecklistDto[]>(TestContext.Current.CancellationToken))!;
        ChecklistDto after = arr.Should().ContainSingle().Subject;
        after.Items.Should().BeEmpty();
        after.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Rename_Checklist_Updates_The_Title()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "rename cl");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "old" }, TestContext.Current.CancellationToken);
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);

        HttpResponseMessage renamed = await client.PatchAsJsonAsync(
            $"api/checklists/{cl!.Id}/", new { title = "new" }, TestContext.Current.CancellationToken);
        ChecklistDto? after = await renamed.Content.ReadFromJsonAsync<ChecklistDto>(TestContext.Current.CancellationToken);
        after!.Title.Should().Be("new");
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"cl-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Seed> CreateSeedAsync(HttpClient client, string name)
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS for {name}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>())!;

        HttpResponseMessage boardResp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId = ws.Id, name, description = (string?)null, visibility = "private" });
        boardResp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await boardResp.Content.ReadFromJsonAsync<BoardDto>())!;

        HttpResponseMessage listResp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "Todo" });
        listResp.IsSuccessStatusCode.Should().BeTrue();
        ListDto list = (await listResp.Content.ReadFromJsonAsync<ListDto>())!;

        HttpResponseMessage cardResp = await client.PostAsJsonAsync(
            "api/cards/", new { listId = list.Id, title = "Card", description = (string?)null });
        cardResp.IsSuccessStatusCode.Should().BeTrue();
        CardDto card = (await cardResp.Content.ReadFromJsonAsync<CardDto>())!;

        return new Seed(board.Id, card.Id);
    }

    private sealed record Seed(Guid BoardId, Guid CardId);
    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId);

    public sealed record ChecklistItemDto(
        Guid Id, Guid ChecklistId, string Text, bool IsCompleted, int Position, Guid? AssignedTo);

    public sealed record ChecklistDto(
        Guid Id, Guid CardId, string Title, IReadOnlyList<ChecklistItemDto> Items,
        int CompletedCount, int TotalCount);
}
