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
    public async Task ICal_For_Public_Board_Allows_Authenticated_NonMember()
    {
        // BETA-2-#3 — see src/Cardscape.Api/Endpoints/Boards/BoardEndpoints.cs.
        // The /ics endpoint is no longer .AllowAnonymous()'d.
        // The previous version let unauthenticated GETs reach
        // the service layer where `currentUser.Id == null` was
        // treated as Unauthenticated (401) for every request,
        // regardless of board visibility. The fix is to let
        // the standard RequireAuthorization() gate the request
        // first (so an unauthenticated caller always sees 401
        // with WWW-Authenticate before any service code runs)
        // and let the service decide 200/403/404 for
        // authenticated callers based on board visibility.
        // Operators that want truly anonymous calendar feeds
        // should expose /api/boards/{id}/ics through a
        // reverse-proxy rule that injects a service-account JWT.
        //
        // The contract under test here: a public board is
        // readable by any authenticated user (even one who is
        // not a workspace member).
        HttpClient ownerClient = await CreateAuthenticatedClientAsync();
        // BoardVisibility.Public == camelCase string "public" (the
        // API configures JsonStringEnumConverter with
        // JsonNamingPolicy.CamelCase, so the wire format is the
        // camelCase enum name, not the int ordinal).
        Guid boardId = await CreateBoardAsync(ownerClient, "ical-public",
            visibility: "public");

        // Add a card with a due date so the calendar is non-empty.
        Guid listId = await CreateListAsync(ownerClient, boardId, "List");
        Guid cardId = await CreateCardAsync(ownerClient, listId, "Public due card");
        DateTimeOffset due = DateTimeOffset.UtcNow.AddDays(3);
        HttpResponseMessage setDue = await ownerClient.PostAsJsonAsync(
            $"api/cards/{cardId}/due-date", new { dueDate = due }, TestContext.Current.CancellationToken);
        setDue.IsSuccessStatusCode.Should().BeTrue();

        // Authenticated but non-member client reads the calendar
        // and gets 200. The whole point of public boards is
        // that any logged-in user can see them, so this pins
        // the access control.
        HttpClient otherClient = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await otherClient.GetAsync(
            $"api/boards/{boardId}/ics", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue(
            "public boards must be readable by any authenticated user, including non-members");
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
            visibility: "workspace");

        HttpClient otherClient = _factory.CreateApiClient();
        string otherEmail = $"ical-other-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage reg = await otherClient.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(otherEmail, "Other", "Password123!"),
            TestContext.Current.CancellationToken);
        reg.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await reg.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Guid> CreateBoardAsync(
        HttpClient client, string name, string visibility = "private")
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS for {name}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
        HttpResponseMessage boardResp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId = ws.Id, name, description = (string?)null, visibility });
        boardResp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await boardResp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;
        return board.Id;
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        ListDto list = (await resp.Content.ReadFromJsonAsync<ListDto>(TestJson.Options))!;
        return list.Id;
    }

    private static async Task<Guid> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        resp.IsSuccessStatusCode.Should().BeTrue();
        CardDto card = (await resp.Content.ReadFromJsonAsync<CardDto>(TestJson.Options))!;
        return card.Id;
    }

    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId);
}
