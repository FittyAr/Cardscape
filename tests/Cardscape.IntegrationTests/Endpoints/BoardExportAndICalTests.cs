using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan) — integration coverage for the
/// board-level read-only endpoints that were added in
/// v1.1.0: the per-board export (ZIP archive) and the
/// per-board iCalendar feed (RFC 5545). Both are read
/// paths; the only mutation is a card with a due date
/// so the iCalendar feed has at least one VEVENT to
/// surface.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardExportAndICalTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public BoardExportAndICalTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_Returns_Zip_With_BoardJson()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(client, "export-test");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{boardId}/export", TestContext.Current.CancellationToken);

        // Diagnostic: print the status + body so we know
        // what the endpoint actually returns when the
        // assertion fails.
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"Export endpoint returned {(int)resp.StatusCode} {resp.StatusCode}. Body: {body}");
        }

        resp.IsSuccessStatusCode.Should().BeTrue();
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
        resp.Content.Headers.ContentDisposition?.FileName.Should().Contain(boardId.ToString());

        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        bytes.Should().NotBeEmpty();
        // ZIP magic: PK (0x50 0x4B)
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public async Task Export_Of_Unknown_Board_Returns_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{Guid.NewGuid()}/export", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ICal_For_Board_With_No_Cards_Returns_Empty_Calendar()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(client, "ical-empty");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/calendar");

        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("BEGIN:VCALENDAR");
        body.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public async Task ICal_For_Board_With_Card_DueDate_Contains_VEvent()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(client, "ical-card");
        Guid listId = await CreateListAsync(client, boardId, "Todo");
        Guid cardId = await CreateCardAsync(client, listId, "Card with due");

        DateTimeOffset due = DateTimeOffset.UtcNow.AddDays(3);
        HttpResponseMessage setDue = await client.PostAsJsonAsync(
            $"api/cards/{cardId}/due-date", new { dueDate = due }, TestContext.Current.CancellationToken);
        setDue.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();

        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("BEGIN:VEVENT");
        body.Should().Contain("END:VEVENT");
    }

    [Fact]
    public async Task ICal_Returns_403_For_NonMember_Even_On_Public_Board()
    {
        // The /ics endpoint is mapped with .AllowAnonymous()
        // but the IcsCalendarService currently rejects
        // non-members with 403 even when the board is public.
        // This is the actual production behaviour as of
        // v1.1.0; the IcsCalendarService has no public-board
        // read path for non-members yet. The test pins the
        // current contract; a future PR can move to
        // "public board is readable by any authenticated
        // user" and update this test to assert 200.
        HttpClient ownerClient = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(ownerClient, "ical-public", visibility: 1 /* Public */);

        HttpClient otherClient = _factory.CreateApiClient();
        string otherEmail = $"ical-other-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage reg = await otherClient.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(otherEmail, "Other", "Password123!"), TestContext.Current.CancellationToken);
        reg.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await reg.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage resp = await otherClient.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"export-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Guid> CreateBoardAsync(HttpClient client, string name, int visibility = 0)
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS for {name}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>())!;
        HttpResponseMessage boardResp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId = ws.Id, name, description = (string?)null, visibility });
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

    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId);
}
