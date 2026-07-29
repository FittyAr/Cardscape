using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class RecurrenceTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public RecurrenceTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_Recurrence_Returns_Null_For_Fresh_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "fresh");

        HttpResponseMessage resp = await client.GetAsync(
            $"api/cards/{seed.CardId}/recurrence/");
        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"GET recurrence failed: {(int)resp.StatusCode} body={body}");
        }

        string json = await resp.Content.ReadAsStringAsync();
        // null body (not "null" string)
        (string.IsNullOrEmpty(json) || json == "null").Should().BeTrue();
    }

    [Fact]
    public async Task Set_Recurrence_Then_Get_Returns_The_Rule()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "set+get");

        DateTimeOffset firstOccurrence = DateTimeOffset.UtcNow.AddDays(7);
        HttpResponseMessage put = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/recurrence/",
            new { intervalDays = 7, firstOccurrenceAt = firstOccurrence });
        put.IsSuccessStatusCode.Should().BeTrue();
        CardRecurrenceDto? dto = await put.Content.ReadFromJsonAsync<CardRecurrenceDto>();
        dto!.IntervalDays.Should().Be(7);

        HttpResponseMessage get = await client.GetAsync(
            $"api/cards/{seed.CardId}/recurrence/");
        CardRecurrenceDto? state = await get.Content.ReadFromJsonAsync<CardRecurrenceDto>();
        state!.IntervalDays.Should().Be(7);
    }

    [Fact]
    public async Task Set_Recurrence_Twice_Updates_Instead_Of_Creating()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "set twice");

        await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/recurrence/",
            new { intervalDays = 7, firstOccurrenceAt = DateTimeOffset.UtcNow.AddDays(7) });
        HttpResponseMessage put = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/recurrence/",
            new { intervalDays = 14, firstOccurrenceAt = DateTimeOffset.UtcNow.AddDays(14) });
        CardRecurrenceDto? dto = await put.Content.ReadFromJsonAsync<CardRecurrenceDto>();
        dto!.IntervalDays.Should().Be(14);
    }

    [Fact]
    public async Task Delete_Recurrence_Returns_204()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "delete");

        await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/recurrence/",
            new { intervalDays = 7, firstOccurrenceAt = DateTimeOffset.UtcNow.AddDays(7) });
        HttpResponseMessage del = await client.DeleteAsync(
            $"api/cards/{seed.CardId}/recurrence/");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Set_With_Zero_Interval_Returns_BadRequest()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "zero");

        HttpResponseMessage put = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/recurrence/",
            new { intervalDays = 0, firstOccurrenceAt = DateTimeOffset.UtcNow });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"rec-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
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

    public sealed record CardRecurrenceDto(
        Guid CardId, int IntervalDays, DateTimeOffset NextOccurrenceAt, bool IsActive);
}
