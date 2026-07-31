using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Cards.DTOs;
using Cardscape.Application.Lists.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Boards;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cardscape.FunctionalTests;

/// <summary>
/// Black-box end-to-end smoke test that walks the full golden
/// path described in <c>docs/development/02-vertical-slices.md</c>:
/// register → workspace → board → list → card → move → archive.
///
/// Unlike <c>Cardscape.IntegrationTests</c> (which exercises
/// individual endpoints with a per-test SQLite file), this test
/// exercises the full HTTP pipeline from end to end as a real
/// client would: it never reaches into the database directly,
/// it only talks to the API through the in-process test server.
/// </summary>
public sealed class GoldenPathSmokeTests : IClassFixture<CardscapeWebApplicationFactory>
{
    private readonly CardscapeWebApplicationFactory _factory;

    public GoldenPathSmokeTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GoldenPath_RegisterCreateWorkspaceBoardListCard_MoveAndArchive_AllSucceed()
    {
        using HttpClient client = _factory.CreateApiClient();

        // ── 1. Register ───────────────────────────────────────
        string suffix = Guid.NewGuid().ToString("N")[..8];
        RegisterRequest registerRequest = new(
            Email: $"golden-{suffix}@example.com",
            DisplayName: "Golden Path",
            Password: "Golden-Path-Password-1!");

        HttpResponseMessage registerResponse = await client.PostAsJsonAsync("api/auth/register", registerRequest);
        registerResponse.IsSuccessStatusCode.Should().BeTrue(
            $"register must succeed. Body: {await registerResponse.Content.ReadAsStringAsync()}");

        AuthResponse? auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // ── 2. Workspace ──────────────────────────────────────
        var workspaceRequest = new { Name = $"Golden Workspace {suffix}" };
        HttpResponseMessage wsResponse = await client.PostAsJsonAsync("api/workspaces/", workspaceRequest);
        wsResponse.IsSuccessStatusCode.Should().BeTrue(
            $"workspace create must succeed. Body: {await wsResponse.Content.ReadAsStringAsync()}");

        WorkspaceDto? workspace = await wsResponse.Content.ReadFromJsonAsync<WorkspaceDto>();
        workspace.Should().NotBeNull();
        workspace!.Name.Should().Be(workspaceRequest.Name);

        // ── 3. Board ──────────────────────────────────────────
        var boardRequest = new
        {
            WorkspaceId = workspace.Id,
            Name = $"Golden Board {suffix}",
            Description = "Smoke test board",
            Visibility = BoardVisibility.Private
        };
        HttpResponseMessage boardResponse = await client.PostAsJsonAsync("api/boards/", boardRequest);
        boardResponse.IsSuccessStatusCode.Should().BeTrue(
            $"board create must succeed. Body: {await boardResponse.Content.ReadAsStringAsync()}");

        BoardDto? board = await boardResponse.Content.ReadFromJsonAsync<BoardDto>();
        board.Should().NotBeNull();
        board!.WorkspaceId.Should().Be(workspace.Id);

        // ── 4. List ───────────────────────────────────────────
        var listRequest = new { BoardId = board.Id, Name = "To Do" };
        HttpResponseMessage listResponse = await client.PostAsJsonAsync("api/lists/", listRequest);
        listResponse.IsSuccessStatusCode.Should().BeTrue(
            $"list create must succeed. Body: {await listResponse.Content.ReadAsStringAsync()}");

        BoardListDto? list = await listResponse.Content.ReadFromJsonAsync<BoardListDto>();
        list.Should().NotBeNull();

        // ── 5. Second list (target for the move) ──────────────
        var secondListRequest = new { BoardId = board.Id, Name = "Doing" };
        HttpResponseMessage secondListResponse = await client.PostAsJsonAsync("api/lists/", secondListRequest);
        secondListResponse.IsSuccessStatusCode.Should().BeTrue();
        BoardListDto? secondList = await secondListResponse.Content.ReadFromJsonAsync<BoardListDto>();
        secondList.Should().NotBeNull();

        // ── 6. Card ───────────────────────────────────────────
        var cardRequest = new { ListId = list!.Id, Title = "Investigate the flaky integration test", Description = "Reproduces locally on the .NET 10 LTS SDK." };
        HttpResponseMessage cardResponse = await client.PostAsJsonAsync("api/cards/", cardRequest);
        cardResponse.IsSuccessStatusCode.Should().BeTrue(
            $"card create must succeed. Body: {await cardResponse.Content.ReadAsStringAsync()}");

        CardDto? card = await cardResponse.Content.ReadFromJsonAsync<CardDto>();
        card.Should().NotBeNull();
        card!.Title.Should().Be(cardRequest.Title);
        card.ListId.Should().Be(list.Id);

        // ── 7. Move to the second list ────────────────────────
        var moveRequest = new { NewListId = secondList!.Id, NewPosition = 1.0 };
        HttpResponseMessage moveResponse = await client.PostAsJsonAsync($"api/cards/{card.Id}/move", moveRequest);
        moveResponse.IsSuccessStatusCode.Should().BeTrue(
            $"card move must succeed. Body: {await moveResponse.Content.ReadAsStringAsync()}");

        CardDto? movedCard = await moveResponse.Content.ReadFromJsonAsync<CardDto>();
        movedCard.Should().NotBeNull();
        movedCard!.ListId.Should().Be(secondList.Id);

        // ── 8. Archive the card ───────────────────────────────
        HttpResponseMessage archiveResponse = await client.PostAsync($"api/cards/{card.Id}/archive", content: null);
        archiveResponse.IsSuccessStatusCode.Should().BeTrue(
            $"card archive must succeed. Body: {await archiveResponse.Content.ReadAsStringAsync()}");

        // ── 9. Verify the card is archived ────────────────────
        HttpResponseMessage getCardResponse = await client.GetAsync($"api/cards/{card.Id}");
        getCardResponse.IsSuccessStatusCode.Should().BeTrue();
        CardDto? fetched = await getCardResponse.Content.ReadFromJsonAsync<CardDto>();
        fetched.Should().NotBeNull();
        fetched!.IsArchived.Should().BeTrue();
    }
}
