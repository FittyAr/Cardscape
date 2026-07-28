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
            $"api/cards/{seed.CardId}/checklists/", new { title = "Today" });
        resp.IsSuccessStatusCode.Should().BeTrue();
        ChecklistDto? created = await resp.Content.ReadFromJsonAsync<ChecklistDto>();
        created!.Title.Should().Be("Today");
        created.CardId.Should().Be(seed.CardId);
    }

    [Fact]
    public async Task List_Returns_Empty_For_Fresh_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "list empty");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/");
        resp.IsSuccessStatusCode.Should().BeTrue();
        ChecklistDto[]? arr =
            (await resp.Content.ReadFromJsonAsync<ChecklistDto[]>())!;
        arr.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_Item_Then_Toggle_Updates_Progress()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "add+toggle");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "todos" });
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>();

        HttpResponseMessage withItem = await client.PostAsJsonAsync(
            $"api/checklists/{cl!.Id}/items/", new { text = "first" });
        ChecklistDto? updated = await withItem.Content.ReadFromJsonAsync<ChecklistDto>();
        Guid itemId = updated!.Items[0].Id;

        HttpResponseMessage toggled = await client.PatchAsync(
            $"api/checklists/{cl.Id}/items/{itemId}/toggle", content: null);
        ChecklistDto? after = await toggled.Content.ReadFromJsonAsync<ChecklistDto>();
        after!.CompletedCount.Should().Be(1);
        after.TotalCount.Should().Be(1);
        after.Items[0].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Checklist_Removes_It()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "delete cl");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "t" });
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>();

        HttpResponseMessage deleted = await client.DeleteAsync(
            $"api/checklists/{cl!.Id}/");
        deleted.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage listed = await client.GetAsync(
            $"api/cards/{seed.CardId}/checklists/");
        ChecklistDto[]? arr =
            (await listed.Content.ReadFromJsonAsync<ChecklistDto[]>())!;
        arr.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_Item_Removes_Single_Item()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "delete item");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "t" });
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>();
        HttpResponseMessage withItem = await client.PostAsJsonAsync(
            $"api/checklists/{cl!.Id}/items/", new { text = "x" });
        ChecklistDto? with = await withItem.Content.ReadFromJsonAsync<ChecklistDto>();

        HttpResponseMessage delItem = await client.DeleteAsync(
            $"api/checklists/{cl.Id}/items/{with!.Items[0].Id}");
        ChecklistDto? after = await delItem.Content.ReadFromJsonAsync<ChecklistDto>();
        after!.Items.Should().BeEmpty();
        after.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Rename_Checklist_Updates_The_Title()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "rename cl");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "old" });
        ChecklistDto? cl = await created.Content.ReadFromJsonAsync<ChecklistDto>();

        HttpResponseMessage renamed = await client.PatchAsJsonAsync(
            $"api/checklists/{cl!.Id}/", new { title = "new" });
        ChecklistDto? after = await renamed.Content.ReadFromJsonAsync<ChecklistDto>();
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
            new { workspaceId = ws.Id, name, description = (string?)null, visibility = 0 });
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
