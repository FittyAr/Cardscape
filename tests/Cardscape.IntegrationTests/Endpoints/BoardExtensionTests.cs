using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the board-extension endpoints over HTTP:
/// list, enable, update config, disable, idempotency, and access
/// control. Each test creates its own workspace + board so the
/// suite is order-independent.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class BoardExtensionTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public BoardExtensionTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Enable_List_Disable_Lifecycle()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Ext lifecycle");

        HttpResponseMessage enable = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/extensions/",
            new { kind = "customFields", configJson = (string?)null }, TestContext.Current.CancellationToken);
        enable.IsSuccessStatusCode.Should().BeTrue();
        BoardExtensionDto enabled = (await enable.Content.ReadFromJsonAsync<BoardExtensionDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        enabled.IsEnabled.Should().BeTrue();
        enabled.Kind.Should().Be(0);

        HttpResponseMessage list = await client.GetAsync($"api/boards/{board.Id}/extensions/", TestContext.Current.CancellationToken);
        list.IsSuccessStatusCode.Should().BeTrue();
        BoardExtensionDto[]? rows = await list.Content.ReadFromJsonAsync<BoardExtensionDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        rows.Should().NotBeNull().And.HaveCount(1);

        HttpResponseMessage disable = await client.DeleteAsync(
            $"api/boards/{board.Id}/extensions/customFields", TestContext.Current.CancellationToken);
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage listAfter = await client.GetAsync(
            $"api/boards/{board.Id}/extensions/", TestContext.Current.CancellationToken);
        BoardExtensionDto[]? after =
            await listAfter.Content.ReadFromJsonAsync<BoardExtensionDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        after!.Should().ContainSingle()
              .Which.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Enable_Is_Idempotent()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Idempotent");

        HttpResponseMessage first = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/extensions/",
            new { kind = "voting", configJson = (string?)null }, TestContext.Current.CancellationToken);
        first.IsSuccessStatusCode.Should().BeTrue();

        // Second call with the same kind must not create a duplicate row.
        HttpResponseMessage second = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/extensions/",
            new { kind = "voting", configJson = (string?)null }, TestContext.Current.CancellationToken);
        second.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage list = await client.GetAsync(
            $"api/boards/{board.Id}/extensions/", TestContext.Current.CancellationToken);
        BoardExtensionDto[]? rows = await list.Content.ReadFromJsonAsync<BoardExtensionDto[]>(TestJson.Options, TestContext.Current.CancellationToken);
        rows.Should().NotBeNull().And.HaveCount(1);
    }

    [Fact]
    public async Task UpdateConfig_Replaces_Json_For_Enabled_Extension()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Config");

        await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/extensions/",
            new { kind = "customFields", configJson = """{"theme":"dark"}""" }, TestContext.Current.CancellationToken);

        HttpResponseMessage put = await client.PutAsJsonAsync(
            $"api/boards/{board.Id}/extensions/customFields/config",
            new { configJson = """{"theme":"light"}""" }, TestContext.Current.CancellationToken);
        put.IsSuccessStatusCode.Should().BeTrue();
        BoardExtensionDto updated = (await put.Content.ReadFromJsonAsync<BoardExtensionDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        updated.ConfigJson.Should().Contain("light");
    }

    [Fact]
    public async Task Disable_Unknown_Kind_Returns_NotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Disable missing");

        HttpResponseMessage resp = await client.DeleteAsync(
            $"api/boards/{board.Id}/extensions/cardRepeater", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Numeric_Kind_Route_Is_Rejected()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Numeric route");

        HttpResponseMessage response = await client.DeleteAsync(
            $"api/boards/{board.Id}/extensions/0", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("extensions.kind_invalid");
    }

    [Fact]
    public async Task Non_Member_Cannot_List_Extensions()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(owner, "Private ext");

        HttpClient stranger = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await stranger.GetAsync(
            $"api/boards/{board.Id}/extensions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enable_Rejects_Unknown_Kind()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        BoardDto board = await CreateBoardAsync(client, "Bad kind");

        HttpResponseMessage resp = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/extensions/",
            new { kind = "notAnExtension", configJson = (string?)null }, TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Without_Auth_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{Guid.NewGuid()}/extensions/", TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ────────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"ext-user-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Ext User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<BoardDto> CreateBoardAsync(HttpClient client, string name)
    {
        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS for {name}" });
        wsResp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await wsResp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;

        HttpResponseMessage boardResp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId = ws.Id, name, description = (string?)null, visibility = "private" });
        boardResp.IsSuccessStatusCode.Should().BeTrue();
        return (await boardResp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;
    }

    public sealed record BoardExtensionDto(
        Guid Id,
        Guid BoardId,
        int Kind,
        string? ConfigJson,
        bool IsEnabled);
}
