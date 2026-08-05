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
    public async Task ICal_For_Public_Board_Allows_Anonymous_NonMember()
    {
        // The /ics endpoint is mapped with .AllowAnonymous()
        // and the IcsCalendarService lets a Public board
        // through for any caller (the public contract is
        // "any user with a link, including unauthenticated").
        // Owner creates a public board; an anonymous client
        // reads the calendar and gets 200. Pinned so a future
        // refactor that tightens the auth check on public
        // boards fires here.
        HttpClient ownerClient = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(ownerClient, "ical-public",
            visibility: 2 /* Public — see BoardVisibility enum */);

        // Add a card with a due date so the calendar is non-empty.
        Guid listId = await CreateListAsync(ownerClient, boardId, "List");
        Guid cardId = await CreateCardAsync(ownerClient, listId, "Public due card");
        DateTimeOffset due = DateTimeOffset.UtcNow.AddDays(3);
        HttpResponseMessage setDue = await ownerClient.PostAsJsonAsync(
            $"api/cards/{cardId}/due-date", new { dueDate = due }, TestContext.Current.CancellationToken);
        setDue.IsSuccessStatusCode.Should().BeTrue();

        // Anonymous client (no bearer) reads the calendar.
        HttpClient anonymous = _factory.CreateApiClient();
        HttpResponseMessage resp = await anonymous.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue(
            "public boards must be readable by any caller, including anonymous");
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("BEGIN:VEVENT");
    }

    [Fact]
    public async Task ICal_For_Workspace_Board_Rejects_Outside_Authenticated_NonMember()
    {
        // The /ics endpoint is mapped with .AllowAnonymous()
        // but the IcsCalendarService requires an authenticated
        // workspace member for a Workspace-visibility board.
        // The owner's user id is the only member of the
        // freshly-created board; the second user is
        // authenticated but not a member, so the service
        // returns 403.
        HttpClient ownerClient = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(ownerClient, "ical-workspace",
            visibility: 1 /* Workspace — see BoardVisibility enum */);

        HttpClient otherClient = _factory.CreateApiClient();
        string otherEmail = $"ical-other-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage reg = await otherClient.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(otherEmail, "Other", "Password123!"),
            TestContext.Current.CancellationToken);
        reg.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await reg.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage resp = await otherClient.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a Workspace-visibility board must reject non-workspace-member authenticated callers");
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
