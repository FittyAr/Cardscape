using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Cross-user access control on the card / list / board surface.
///
/// These tests close the loop that the read-side and write-side
/// guards in <c>MembershipGuards</c> introduce: a user that isn't a
/// member of a board (or that the board is private and they can't
/// see) must get <see cref="HttpStatusCode.Forbidden"/> on every
/// read and write that targets that board's data.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class CardscapeAccessControlTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public CardscapeAccessControlTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Read_Private_Card_As_Outsider_Returns_403()
    {
        // Owner creates a private board + list + card.
        HttpClient owner = await CreateAuthenticatedClientAsync();
        (BoardDto board, BoardListDto list, CardDto card) = await SeedBoardListCardAsync(owner);

        // A different user registers and tries to read the card.
        HttpClient outsider = await CreateAuthenticatedClientAsync();

        HttpResponseMessage getCard = await outsider.GetAsync($"api/cards/{card.Id}", TestContext.Current.CancellationToken);
        getCard.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage getList = await outsider.GetAsync($"api/lists/{list.Id}", TestContext.Current.CancellationToken);
        getList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage getBoard = await outsider.GetAsync($"api/boards/{board.Id}", TestContext.Current.CancellationToken);
        getBoard.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage listCards = await outsider.GetAsync($"api/cards/?boardId={board.Id}&includeArchived=false", TestContext.Current.CancellationToken);
        listCards.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage listLists = await outsider.GetAsync($"api/lists/?boardId={board.Id}&includeArchived=false", TestContext.Current.CancellationToken);
        listLists.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Write_To_Card_As_Outsider_Returns_403()
    {
        // Owner creates a private board + list + card.
        HttpClient owner = await CreateAuthenticatedClientAsync();
        (_, BoardListDto list, CardDto card) = await SeedBoardListCardAsync(owner);

        // Outsider tries to mutate the card.
        HttpClient outsider = await CreateAuthenticatedClientAsync();

        HttpResponseMessage rename = await outsider.PostAsJsonAsync(
            $"api/cards/{card.Id}/rename", new { newTitle = "Hacked" }, TestContext.Current.CancellationToken);
        rename.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage complete = await outsider.PostAsync(
            $"api/cards/{card.Id}/complete", content: null, TestContext.Current.CancellationToken);
        complete.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage move = await outsider.PostAsJsonAsync(
            $"api/cards/{card.Id}/move", new { newListId = list.Id, newPosition = 1024.0 }, TestContext.Current.CancellationToken);
        move.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Outsider tries to add a new card to the owner's list.
        HttpResponseMessage createCard = await outsider.PostAsJsonAsync(
            "api/cards/", new { listId = list.Id, title = "Sneaky", description = (string?)null }, TestContext.Current.CancellationToken);
        createCard.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Outsider tries to add a new list to the owner's board.
        HttpResponseMessage createList = await outsider.PostAsJsonAsync(
            "api/lists/", new { boardId = list.BoardId, name = "Hostile takeover" }, TestContext.Current.CancellationToken);
        createList.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Read_Public_Board_As_Outsider_Returns_200()
    {
        // Owner creates a public board + list + card.
        HttpClient owner = await CreateAuthenticatedClientAsync();
        HttpResponseMessage createBoard = await owner.PostAsJsonAsync(
            "api/boards/", new
            {
                workspaceId = (await SeedWorkspaceAsync(owner)).Id,
                name = "Public",
                description = (string?)null,
                visibility = 2 // BoardVisibility.Public
            }, TestContext.Current.CancellationToken);
        createBoard.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await createBoard.Content.ReadFromJsonAsync<BoardDto>(TestContext.Current.CancellationToken))!;

        HttpResponseMessage createList = await owner.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "Public list" }, TestContext.Current.CancellationToken);
        BoardListDto list = (await createList.Content.ReadFromJsonAsync<BoardListDto>(TestContext.Current.CancellationToken))!;
        CardDto card = await CreateCardAsync(owner, list.Id, "Public card");

        // Outsider should be able to read the public board.
        HttpClient outsider = await CreateAuthenticatedClientAsync();
        (await outsider.GetAsync($"api/cards/{card.Id}", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await outsider.GetAsync($"api/lists/{list.Id}", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await outsider.GetAsync($"api/boards/{board.Id}", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        // But still not write — non-members can never write.
        HttpResponseMessage rename = await outsider.PostAsJsonAsync(
            $"api/cards/{card.Id}/rename", new { newTitle = "Should fail" }, TestContext.Current.CancellationToken);
        rename.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"acl-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "ACL User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<WorkspaceDto> SeedWorkspaceAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS-{Guid.NewGuid():N}" });
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>())!;
    }

    private async Task<(BoardDto Board, BoardListDto List, CardDto Card)> SeedBoardListCardAsync(HttpClient client)
    {
        WorkspaceDto workspace = await SeedWorkspaceAsync(client);
        HttpResponseMessage createBoard = await client.PostAsJsonAsync(
            "api/boards/", new
            {
                workspaceId = workspace.Id,
                name = "Private",
                description = (string?)null,
                visibility = 0 // BoardVisibility.Private
            });
        createBoard.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await createBoard.Content.ReadFromJsonAsync<BoardDto>())!;

        HttpResponseMessage createList = await client.PostAsJsonAsync(
            "api/lists/", new { boardId = board.Id, name = "List" });
        BoardListDto list = (await createList.Content.ReadFromJsonAsync<BoardListDto>())!;
        CardDto card = await CreateCardAsync(client, list.Id, "Card");
        return (board, list, card);
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        response.IsSuccessStatusCode.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<CardDto>())!;
    }
}
