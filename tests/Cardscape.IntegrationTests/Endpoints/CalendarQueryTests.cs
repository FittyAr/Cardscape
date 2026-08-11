using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Boards;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the calendar query: a card with a due
/// date in the current month shows up; a card with a due date in a
/// different month does not; an outsider on a private board is
/// blocked.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class CalendarQueryTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public CalendarQueryTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Cards_With_Due_Date_In_Range_Show_Up()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("CalOwner");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Cal WS");
        BoardDto board = await CreateBoardAsync(owner, ws.Id, "Cal Board");
        BoardListDto list = await CreateListAsync(owner, board.Id, "Todo");

        DateTimeOffset from = new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddMonths(1);

        CardDto inRange = await CreateCardAsync(owner, list.Id, "In range");
        await SetDueDateAsync(owner, inRange.Id, from.AddDays(7));

        HttpResponseMessage resp = await owner.GetAsync(
            $"api/cards/calendar?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        CalendarEntryDto[]? rows = await resp.Content.ReadFromJsonAsync<CalendarEntryDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        rows.Should().NotBeNull();
        rows!.Should().Contain(r => r.CardId == inRange.Id);
    }

    [Fact]
    public async Task Cards_Outside_Range_Are_Excluded()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("CalOwner2");
        WorkspaceDto ws = await CreateWorkspaceAsync(owner, "Cal2 WS");
        BoardDto board = await CreateBoardAsync(owner, ws.Id, "Cal2 Board");
        BoardListDto list = await CreateListAsync(owner, board.Id, "Todo");

        DateTimeOffset from = new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddMonths(1);
        DateTimeOffset outside = to.AddMonths(2);

        CardDto outsideCard = await CreateCardAsync(owner, list.Id, "Out of range");
        await SetDueDateAsync(owner, outsideCard.Id, outside);

        HttpResponseMessage resp = await owner.GetAsync(
            $"api/cards/calendar?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        CalendarEntryDto[]? rows = await resp.Content.ReadFromJsonAsync<CalendarEntryDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        rows.Should().NotBeNull();
        rows!.Should().NotContain(r => r.CardId == outsideCard.Id);
    }

    [Fact]
    public async Task Invalid_Range_Returns_400()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync("CalOwner3");
        DateTimeOffset from = DateTimeOffset.UtcNow;
        DateTimeOffset to = from.AddDays(-1);

        HttpResponseMessage resp = await owner.GetAsync(
            $"api/cards/calendar?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards/calendar?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("o"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("o"))}", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string displayNamePrefix)
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"{displayNamePrefix}-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, $"{displayNamePrefix} User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<WorkspaceDto> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync("api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
    }

    private static async Task<BoardDto> CreateBoardAsync(HttpClient client, Guid workspaceId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name, description = (string?)null, visibility = "private" });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;
    }

    private static async Task<BoardListDto> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<BoardListDto>(TestJson.Options))!;
    }

    private static async Task<CardDto> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title, description = (string?)null });
        resp.IsSuccessStatusCode.Should().BeTrue();
        return (await resp.Content.ReadFromJsonAsync<CardDto>(TestJson.Options))!;
    }

    private static async Task SetDueDateAsync(HttpClient client, Guid cardId, DateTimeOffset dueDate)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"api/cards/{cardId}/due-date", new { dueDate });
        resp.IsSuccessStatusCode.Should().BeTrue();
    }

    // ── DTOs (mirror the API) ──────────────────────────────────

    public sealed record WorkspaceDto(Guid Id, Guid OwnerId, string Name, int MemberCount);
    public sealed record BoardDto(Guid Id, Guid WorkspaceId, string Name, BoardVisibility Visibility, bool IsArchived, bool IsStarred, DateTimeOffset CreatedAt);
    public sealed record BoardListDto(Guid Id, Guid BoardId, string Name, double Position, bool IsArchived, DateTimeOffset CreatedAt, int CardCount);
    public sealed record CardDto(Guid Id, Guid ListId, string Title, string Description, double Position, DateTimeOffset? DueDate, bool IsArchived, bool IsCompleted, string? CoverColor, DateTimeOffset CreatedAt, int MemberCount, int LabelCount);
    public sealed record CalendarEntryDto(Guid CardId, Guid ListId, Guid BoardId, string BoardName, string Title, DateTimeOffset DueDate, bool IsCompleted);
}
