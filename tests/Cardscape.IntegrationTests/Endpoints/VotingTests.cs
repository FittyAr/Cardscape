using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class VotingTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public VotingTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Toggle_Vote_Adds_One_And_Returns_State()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Toggle first");

        HttpResponseMessage resp = await client.PostAsync(
            $"api/cards/{seed.CardId}/votes/", content: null, TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();

        CardVoteStateDto? state = await resp.Content.ReadFromJsonAsync<CardVoteStateDto>(TestContext.Current.CancellationToken);
        state.Should().NotBeNull();
        state!.VoteCount.Should().Be(1);
        state.CurrentUserHasVoted.Should().BeTrue();
    }

    [Fact]
    public async Task Toggle_Vote_Twice_Removes_The_Vote()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Toggle twice");

        await client.PostAsync($"api/cards/{seed.CardId}/votes/", content: null, TestContext.Current.CancellationToken);
        HttpResponseMessage resp = await client.PostAsync(
            $"api/cards/{seed.CardId}/votes/", content: null, TestContext.Current.CancellationToken);

        resp.IsSuccessStatusCode.Should().BeTrue();
        CardVoteStateDto? state = await resp.Content.ReadFromJsonAsync<CardVoteStateDto>(TestContext.Current.CancellationToken);
        state!.VoteCount.Should().Be(0);
        state.CurrentUserHasVoted.Should().BeFalse();
    }

    [Fact]
    public async Task Get_Votes_Returns_Initial_State_For_Fresh_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Get state");

        HttpResponseMessage resp = await client.GetAsync($"api/cards/{seed.CardId}/votes/", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        CardVoteStateDto? state = await resp.Content.ReadFromJsonAsync<CardVoteStateDto>(TestContext.Current.CancellationToken);
        state!.VoteCount.Should().Be(0);
        state.CurrentUserHasVoted.Should().BeFalse();
    }

    [Fact]
    public async Task Vote_On_Unknown_Card_Returns_NotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.PostAsync(
            $"api/cards/{Guid.NewGuid()}/votes/", content: null, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Vote_Without_Auth_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.PostAsync(
            $"api/cards/{Guid.NewGuid()}/votes/", content: null, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"voter-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Voter", "Password123!");
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

    public sealed record CardVoteStateDto(
        Guid CardId,
        int VoteCount,
        bool CurrentUserHasVoted);
}
