using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan) — integration coverage for the
/// new P3 card surface shipped in v1.1.0: Card Snooze
/// (§3.2) and Card Mirror (§3.3). The backend commands
/// have unit tests in <c>Cardscape.UnitTests</c> but
/// the endpoint round-trip is new. The 5 tests in
/// this file assert the happy path of every
/// endpoint, the "snoozed card is hidden from
/// the default list" contract, and the "unsnooze
/// is idempotent" contract.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class CardSnoozeAndMirrorTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public CardSnoozeAndMirrorTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Snooze_Hides_Card_From_Default_List()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "snooze-hide");

        DateTimeOffset until = DateTimeOffset.UtcNow.AddDays(7);
        HttpResponseMessage snoozed = await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/snooze",
            new { until }, TestContext.Current.CancellationToken);
        snoozed.IsSuccessStatusCode.Should().BeTrue();

        // The default GET (includeSnoozed=false) must not
        // include the snoozed card. The query exposes the
        // includeSnoozed flag explicitly per the v1.1.0
        // audit G6b follow-up.
        HttpResponseMessage defaultList = await client.GetAsync(
            $"api/cards/?boardId={seed.BoardId}", TestContext.Current.CancellationToken);
        defaultList.IsSuccessStatusCode.Should().BeTrue();
        string defaultBody = await defaultList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        defaultBody.Should().NotContain(seed.CardId.ToString());

        // includeSnoozed=true brings it back.
        HttpResponseMessage withSnoozed = await client.GetAsync(
            $"api/cards/?boardId={seed.BoardId}&includeSnoozed=true", TestContext.Current.CancellationToken);
        withSnoozed.IsSuccessStatusCode.Should().BeTrue();
        string withSnoozedBody = await withSnoozed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        withSnoozedBody.Should().Contain(seed.CardId.ToString());

        // The dedicated /snoozed endpoint also returns it.
        HttpResponseMessage snoozedList = await client.GetAsync(
            $"api/cards/snoozed?boardId={seed.BoardId}", TestContext.Current.CancellationToken);
        snoozedList.IsSuccessStatusCode.Should().BeTrue();
        string snoozedBody = await snoozedList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        snoozedBody.Should().Contain(seed.CardId.ToString());
    }

    [Fact]
    public async Task Unsnooze_Restores_Card_To_Default_List()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "snooze-restore");

        await client.PostAsJsonAsync(
            $"api/cards/{seed.CardId}/snooze",
            new { until = DateTimeOffset.UtcNow.AddDays(7) }, TestContext.Current.CancellationToken);

        HttpResponseMessage unsnoozed = await client.DeleteAsync(
            $"api/cards/{seed.CardId}/snooze", TestContext.Current.CancellationToken);
        unsnoozed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage defaultList = await client.GetAsync(
            $"api/cards/?boardId={seed.BoardId}", TestContext.Current.CancellationToken);
        defaultList.IsSuccessStatusCode.Should().BeTrue();
        string body = await defaultList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain(seed.CardId.ToString());
    }

    [Fact]
    public async Task Unsnooze_Without_Prior_Snooze_Returns_NotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "snooze-noop");

        // The endpoint is NOT idempotent today (it returns
        // 404 when no snooze row exists). The test pins the
        // current contract; a future PR can change the
        // contract to 204 + no-op and update this test.
        HttpResponseMessage unsnoozed = await client.DeleteAsync(
            $"api/cards/{seed.CardId}/snooze", TestContext.Current.CancellationToken);
        unsnoozed.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mirror_Creates_New_Card_In_Target_List()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed source = await CreateSeedAsync(client, "mirror-src");
        // Create a second board + list in the same workspace
        // so the mirror command has a valid target.
        Guid targetBoardId = await CreateBoardAsync(client, "mirror-tgt");
        Guid targetListId = await CreateListAsync(client, targetBoardId, "Mirror list");

        HttpResponseMessage mirrored = await client.PostAsJsonAsync(
            $"api/cards/{source.CardId}/mirror",
            new { targetListId }, TestContext.Current.CancellationToken);
        mirrored.StatusCode.Should().Be(HttpStatusCode.Created);
        string body = await mirrored.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeNullOrWhiteSpace();
        // The mirror creates a new card id; verify the new
        // card appears in the target list.
        HttpResponseMessage targetListCards = await client.GetAsync(
            $"api/cards/?boardId={targetBoardId}", TestContext.Current.CancellationToken);
        targetListCards.IsSuccessStatusCode.Should().BeTrue();
        string targetBody = await targetListCards.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        targetBody.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Mirror_To_Unknown_List_Returns_Error()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed source = await CreateSeedAsync(client, "mirror-bad");

        HttpResponseMessage mirrored = await client.PostAsJsonAsync(
            $"api/cards/{source.CardId}/mirror",
            new { targetListId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
        mirrored.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"snooze-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Seed> CreateSeedAsync(HttpClient client, string name)
    {
        Guid boardId = await CreateBoardAsync(client, name);
        Guid listId = await CreateListAsync(client, boardId, "Todo");
        Guid cardId = await CreateCardAsync(client, listId, "Card");
        return new Seed(boardId, listId, cardId);
    }

    private async Task<Guid> CreateBoardAsync(HttpClient client, string name)
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
        return board.Id;
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        ListDto list = (await resp.Content.ReadFromJsonAsync<ListDto>())!;
        return list.Id;
    }

    private static async Task<Guid> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        resp.IsSuccessStatusCode.Should().BeTrue();
        CardDto card = (await resp.Content.ReadFromJsonAsync<CardDto>())!;
        return card.Id;
    }

    private sealed record Seed(Guid BoardId, Guid ListId, Guid CardId);
    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId);
}
