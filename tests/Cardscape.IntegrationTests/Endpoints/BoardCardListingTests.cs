using System.Net.Http.Headers;
using WebCardSummary = Cardscape.Web.Shared.CardSummaryDto;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// The board card listing (GET /api/cards?boardId=) carries everything
/// a kanban card front shows — labels, assignees, checklist progress
/// and comment count — and honours the fractional positions the Web
/// client sends when cards are reordered by drag and drop.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardCardListingTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public BoardCardListingTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task List_Includes_Labels_Members_Checklist_Progress_And_Comment_Count()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, UserSummary user) = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, cardCount: 2);
        Guid cardId = seed.CardIds[0];

        HttpResponseMessage labelResp = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/labels/", new { name = "Bug", color = "#d73a4a" }, ct);
        labelResp.IsSuccessStatusCode.Should().BeTrue();
        IdDto label = (await labelResp.Content.ReadFromJsonAsync<IdDto>(ct))!;
        (await client.PostAsync($"api/cards/{cardId}/labels/{label.Id}", content: null, ct))
            .IsSuccessStatusCode.Should().BeTrue();
        (await client.PostAsync($"api/cards/{cardId}/assign/{user.Id}", content: null, ct))
            .IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage checklistResp = await client.PostAsJsonAsync(
            $"api/cards/{cardId}/checklists/", new { title = "Todo" }, ct);
        IdDto checklist = (await checklistResp.Content.ReadFromJsonAsync<IdDto>(ct))!;
        IdDto first = (await (await client.PostAsJsonAsync(
            $"api/checklists/{checklist.Id}/items/", new { text = "one" }, ct)).Content.ReadFromJsonAsync<IdDto>(ct))!;
        (await client.PostAsJsonAsync($"api/checklists/{checklist.Id}/items/", new { text = "two" }, ct))
            .IsSuccessStatusCode.Should().BeTrue();
        (await client.PatchAsync($"api/checklists/{checklist.Id}/items/{first.Id}/toggle", content: null, ct))
            .IsSuccessStatusCode.Should().BeTrue();

        for (int i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync($"api/cards/{cardId}/comments/", new { body = $"comment {i}" }, ct))
                .IsSuccessStatusCode.Should().BeTrue();
        }

        WebCardSummary[] cards = await ListAsync(client, seed.BoardId);

        WebCardSummary busy = cards.Single(c => c.Id == cardId);
        busy.Labels.Should().ContainSingle().Which.Should().BeEquivalentTo(new { label.Id, Name = "Bug", Color = "#d73a4a" });
        busy.Members.Should().ContainSingle().Which.Should().BeEquivalentTo(new { UserId = user.Id, user.DisplayName });
        busy.ChecklistCompleted.Should().Be(1);
        busy.ChecklistTotal.Should().Be(2);
        busy.CommentCount.Should().Be(3);

        WebCardSummary bare = cards.Single(c => c.Id == seed.CardIds[1]);
        bare.Labels.Should().BeEmpty();
        bare.Members.Should().BeEmpty();
        bare.ChecklistTotal.Should().Be(0);
        bare.CommentCount.Should().Be(0);
    }

    [Fact]
    public async Task Move_With_Fractional_Position_Reorders_Within_The_List()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        (HttpClient client, _) = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, cardCount: 3);

        WebCardSummary[] before = await ListAsync(client, seed.BoardId);
        before.Select(c => c.Id).Should().Equal(seed.CardIds);

        // Drop the last card between the first two, as the board's
        // drag and drop does: the average of its new neighbours.
        double between = (before[0].Position + before[1].Position) / 2;
        HttpResponseMessage move = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardIds[2]}/move", new { listId = seed.ListId, position = between }, ct);
        move.IsSuccessStatusCode.Should().BeTrue();

        WebCardSummary[] after = await ListAsync(client, seed.BoardId);
        after.Select(c => c.Id).Should().Equal(seed.CardIds[0], seed.CardIds[2], seed.CardIds[1]);
    }

    private static async Task<WebCardSummary[]> ListAsync(HttpClient client, Guid boardId)
    {
        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards?boardId={boardId}", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<WebCardSummary[]>(TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private async Task<(HttpClient Client, UserSummary User)> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"cards-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage r = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, "Grace Hopper", "Password123!"));
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    private static async Task<Seed> CreateSeedAsync(HttpClient client, int cardCount)
    {
        IdDto ws = await PostAsync(client, "api/workspaces/", new { name = "Card listing WS" });
        IdDto board = await PostAsync(client, "api/boards/",
            new { workspaceId = ws.Id, name = "Card listing", description = (string?)null, visibility = "private" });
        IdDto list = await PostAsync(client, "api/lists/", new { boardId = board.Id, name = "Todo" });

        List<Guid> cardIds = [];
        for (int i = 0; i < cardCount; i++)
        {
            IdDto card = await PostAsync(client, "api/cards/",
                new { listId = list.Id, title = $"Card {i}", description = (string?)null });
            cardIds.Add(card.Id);
        }

        return new Seed(board.Id, list.Id, cardIds);
    }

    private static async Task<IdDto> PostAsync(HttpClient client, string url, object body)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(url, body);
        resp.IsSuccessStatusCode.Should().BeTrue($"POST {url} should succeed");
        return (await resp.Content.ReadFromJsonAsync<IdDto>())!;
    }

    private sealed record Seed(Guid BoardId, Guid ListId, IReadOnlyList<Guid> CardIds);

    private sealed record IdDto(Guid Id);
}
