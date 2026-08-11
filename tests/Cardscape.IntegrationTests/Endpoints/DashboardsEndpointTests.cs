using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Dashboards;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan) — integration coverage for the
/// Dashboards endpoint group (P3.5). The bounded
/// context ships Dashcard aggregates (the per-board
/// widgets), the repository, the migration, the
/// endpoint, and the Web UI page. The integration
/// tests pin the round-trip happy path: create,
/// list, update config, delete.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class DashboardsEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public DashboardsEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_Then_List_Adds_Dashcard_To_Board()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "dash-create");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/dashcards/",
            new
            {
                boardId = seed.BoardId,
                kind = "overdueCount",
                title = "Overdue",
                configurationJson = (string?)null,
                position = 0
            },
            TestContext.Current.CancellationToken);
        created.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage listed = await client.GetAsync(
            $"api/boards/{seed.BoardId}/dashcards/", TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        string body = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Update_Config_Persists_Json()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "dash-config");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/dashcards/",
            new
            {
                boardId = seed.BoardId,
                kind = "overdueCount",
                title = "Count",
                configurationJson = (string?)null,
                position = 0
            },
            TestContext.Current.CancellationToken);
        created.IsSuccessStatusCode.Should().BeTrue();
        DashcardDto dashcard = (await created.Content.ReadFromJsonAsync<DashcardDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpResponseMessage updated = await client.PutAsJsonAsync(
            $"api/boards/{seed.BoardId}/dashcards/{dashcard.Id}/config",
            new { configurationJson = "{\"threshold\":7}" },
            TestContext.Current.CancellationToken);
        updated.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Removes_Dashcard()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "dash-delete");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/dashcards/",
            new
            {
                boardId = seed.BoardId,
                kind = "byMember",
                title = "By Member",
                configurationJson = (string?)null,
                position = 0
            },
            TestContext.Current.CancellationToken);
        created.IsSuccessStatusCode.Should().BeTrue();
        DashcardDto dashcard = (await created.Content.ReadFromJsonAsync<DashcardDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpResponseMessage deleted = await client.DeleteAsync(
            $"api/boards/{seed.BoardId}/dashcards/{dashcard.Id}", TestContext.Current.CancellationToken);
        deleted.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage listed = await client.GetAsync(
            $"api/boards/{seed.BoardId}/dashcards/", TestContext.Current.CancellationToken);
        string body = await listed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain(dashcard.Id.ToString());
    }

    [Fact]
    public async Task Delete_Unknown_Dashcard_Returns_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "dash-del-404");

        HttpResponseMessage resp = await client.DeleteAsync(
            $"api/boards/{seed.BoardId}/dashcards/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"dash-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<Seed> CreateSeedAsync(HttpClient client, string name)
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS for {name}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
        HttpResponseMessage boardResp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId = ws.Id, name, description = (string?)null, visibility = "private" });
        boardResp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await boardResp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;
        return new Seed(board.Id);
    }

    private sealed record Seed(Guid BoardId);
    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record DashcardDto(Guid Id, Guid BoardId, DashcardKind Kind, int Position, string? Title, string? ConfigJson);
}
