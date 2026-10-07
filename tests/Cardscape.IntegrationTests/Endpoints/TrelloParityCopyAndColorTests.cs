using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage for the board background colour and the
/// "Copy card" / "Copy list" endpoints: routing, Wolverine handler
/// discovery and EF persistence of the new <c>boards.Color</c> column.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class TrelloParityCopyAndColorTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public TrelloParityCopyAndColorTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Board_Color_Can_Be_Set_Read_Back_And_Cleared()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "colour");

        HttpResponseMessage set = await client.PostAsJsonAsync($"api/boards/{seed.BoardId}/color", new { color = "purple" }, ct);
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        (await set.Content.ReadFromJsonAsync<BoardDto>(ct))!.Color.Should().Be("#a97bcf");

        BoardDto reloaded = (await client.GetFromJsonAsync<BoardDto>($"api/boards/{seed.BoardId}", ct))!;
        reloaded.Color.Should().Be("#a97bcf");

        HttpResponseMessage invalid = await client.PostAsJsonAsync($"api/boards/{seed.BoardId}/color", new { color = "chartreuse" }, ct);
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        HttpResponseMessage cleared = await client.DeleteAsync($"api/boards/{seed.BoardId}/color", ct);
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<BoardDto>($"api/boards/{seed.BoardId}", ct))!.Color.Should().BeNull();
    }

    [Fact]
    public async Task Copy_Card_Duplicates_Checklists_Into_The_Target_List()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "copy card");

        HttpResponseMessage checklist = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/checklists/", new { title = "Steps" }, ct);
        ChecklistDto cl = (await checklist.Content.ReadFromJsonAsync<ChecklistDto>(ct))!;
        (await client.PostAsJsonAsync($"api/checklists/{cl.Id}/items/", new { text = "one" }, ct))
            .IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage copied = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/copy", new { targetListId = seed.SecondListId, title = "Card (copy)" }, ct);
        copied.StatusCode.Should().Be(HttpStatusCode.Created);
        CardDto copy = (await copied.Content.ReadFromJsonAsync<CardDto>(ct))!;
        copy.Id.Should().NotBe(seed.CardId);
        copy.ListId.Should().Be(seed.SecondListId);
        copy.Title.Should().Be("Card (copy)");

        ChecklistDto[] copiedChecklists = (await client.GetFromJsonAsync<ChecklistDto[]>(
            $"api/cards/{copy.Id}/checklists/", ct))!;
        copiedChecklists.Should().ContainSingle().Which.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Copy_List_Creates_A_List_With_Copied_Cards()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "copy list");

        HttpResponseMessage copied = await client.PostAsJsonAsync(
            $"api/lists/{seed.FirstListId}/copy", new { name = "Todo again" }, ct);
        copied.StatusCode.Should().Be(HttpStatusCode.Created);
        ListDto list = (await copied.Content.ReadFromJsonAsync<ListDto>(ct))!;
        list.Name.Should().Be("Todo again");
        list.CardCount.Should().Be(1);

        CardDto[] cards = (await client.GetFromJsonAsync<CardDto[]>($"api/cards/?boardId={seed.BoardId}", ct))!;
        cards.Where(card => card.ListId == list.Id).Should().ContainSingle().Which.Title.Should().Be("Card");
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"copy-{Guid.NewGuid():N}@cardscape.local";
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

        HttpResponseMessage firstResp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "Todo" });
        ListDto first = (await firstResp.Content.ReadFromJsonAsync<ListDto>())!;
        HttpResponseMessage secondResp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "Doing" });
        ListDto second = (await secondResp.Content.ReadFromJsonAsync<ListDto>())!;

        HttpResponseMessage cardResp = await client.PostAsJsonAsync(
            "api/cards/", new { listId = first.Id, title = "Card", description = (string?)null });
        cardResp.IsSuccessStatusCode.Should().BeTrue();
        CardDto card = (await cardResp.Content.ReadFromJsonAsync<CardDto>())!;

        return new Seed(board.Id, first.Id, second.Id, card.Id);
    }

    private sealed record Seed(Guid BoardId, Guid FirstListId, Guid SecondListId, Guid CardId);
    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId, string? Color);
    private sealed record ListDto(Guid Id, string Name, int CardCount);
    private sealed record CardDto(Guid Id, Guid ListId, string Title);
    private sealed record ChecklistDto(Guid Id, int TotalCount);
}
