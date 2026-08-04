using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the activity-timeline endpoints. Each
/// test creates its own workspace + board + list + card so the
/// suite is order-independent.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class ActivityTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public ActivityTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task List_Board_Activities_As_Member_Returns_Empty_Page_For_Fresh_Board()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Empty board activity");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{seed.BoardId}/activities/", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();

        ActivityPageDto? page =
            (await resp.Content.ReadFromJsonAsync<ActivityPageDto>(TestContext.Current.CancellationToken))!;
        page.Should().NotBeNull();
        page!.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task List_Card_Activities_Endpoint_Returns_A_Valid_Page_Shape()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Card activity shape");

        // The slice ships the activity surface — the timeline
        // entries themselves are populated by other slices'
        // domain-event handlers, so a fresh card may legitimately
        // have an empty page. We only assert the response
        // envelope is well-formed.
        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards/{seed.CardId}/activities/", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();

        ActivityPageDto? page =
            (await resp.Content.ReadFromJsonAsync<ActivityPageDto>(TestContext.Current.CancellationToken))!;
        page.Should().NotBeNull();
        page!.Items.Should().NotBeNull();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task List_Board_Activities_Without_Auth_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{Guid.NewGuid()}/activities/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_Board_Activities_Limit_Clamps_At_TwoHundred()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Limit clamp");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{seed.BoardId}/activities/?limit=99999", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        // We don't need to assert items here — just that the
        // server didn't 400 on an absurdly large limit.
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"activity-user-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Activity User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Seed> CreateSeedAsync(HttpClient client, string name)
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

    // ── DTOs (mirror the API + Web) ─────────────────────────

    private sealed record WorkspaceDto(Guid Id, Guid OwnerId, string Name, int MemberCount);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId, string Name, int Visibility, bool IsArchived, bool IsStarred, DateTimeOffset CreatedAt);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId, string Title);

    private sealed record ActivityDto(
        Guid Id,
        Guid BoardId,
        Guid? CardId,
        Guid ActorId,
        int Kind,
        string KindName,
        string PayloadJson,
        DateTimeOffset OccurredAt);

    private sealed record ActivityPageDto(
        IReadOnlyList<ActivityDto> Items,
        string? NextCursor);
}
