using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Dashboards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.Domain.Dashboards;
using FluentAssertions;
using Xunit;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Integration coverage for the Dashboards bounded context
/// (P3.5). Round-trip: create a workspace + board, post a
/// dashcard, list the board's dashcards, delete it.
/// </summary>
[Collection(CardscapeApi.Name)]
public class DashboardsEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public DashboardsEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashcard_Crud_Roundtrip()
    {
        HttpClient client = _factory.CreateApiClient();
        AuthResponse auth = await RegisterAndLogin(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        HttpResponseMessage wsResp = await client.PostAsJsonAsync(
            "api/workspaces/", new CreateWorkspaceRequest("Dashboards WS"));
        Cardscape.Application.Workspaces.DTOs.WorkspaceDto ws =
            (await wsResp.Content.ReadFromJsonAsync<Cardscape.Application.Workspaces.DTOs.WorkspaceDto>())!;

        HttpResponseMessage bdResp = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId = ws.Id, name = "Board", description = (string?)null, visibility = 0 });
        Cardscape.Application.Boards.DTOs.BoardDto board =
            (await bdResp.Content.ReadFromJsonAsync<Cardscape.Application.Boards.DTOs.BoardDto>())!;

        // Initial list is empty.
        HttpResponseMessage listResp = await client.GetAsync(
            $"api/boards/{board.Id}/dashcards/");
        listResp.IsSuccessStatusCode.Should().BeTrue();
        List<DashcardDto> initial =
            (await listResp.Content.ReadFromJsonAsync<List<DashcardDto>>())!;
        initial.Should().BeEmpty();

        // Create.
        HttpResponseMessage createResp = await client.PostAsJsonAsync(
            $"api/boards/{board.Id}/dashcards/",
            new CreateDashcardRequest(board.Id, DashcardKind.OverdueCount, "Overdue", "{}", 0));
        createResp.IsSuccessStatusCode.Should().BeTrue();
        DashcardDto created = (await createResp.Content.ReadFromJsonAsync<DashcardDto>())!;
        created.Kind.Should().Be(DashcardKind.OverdueCount);
        created.Title.Should().Be("Overdue");

        // List shows it.
        HttpResponseMessage listResp2 = await client.GetAsync(
            $"api/boards/{board.Id}/dashcards/");
        List<DashcardDto> afterCreate =
            (await listResp2.Content.ReadFromJsonAsync<List<DashcardDto>>())!;
        afterCreate.Should().ContainSingle().Which.Id.Should().Be(created.Id);

        // Delete.
        HttpResponseMessage deleteResp = await client.DeleteAsync(
            $"api/boards/{board.Id}/dashcards/{created.Id}");
        deleteResp.IsSuccessStatusCode.Should().BeTrue();

        // List is empty again.
        HttpResponseMessage listResp3 = await client.GetAsync(
            $"api/boards/{board.Id}/dashcards/");
        List<DashcardDto> afterDelete =
            (await listResp3.Content.ReadFromJsonAsync<List<DashcardDto>>())!;
        afterDelete.Should().BeEmpty();
    }

    private static async Task<AuthResponse> RegisterAndLogin(HttpClient client)
    {
        string email = $"dash-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Dash User", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        return (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
}
