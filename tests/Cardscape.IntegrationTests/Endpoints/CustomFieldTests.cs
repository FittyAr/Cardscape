using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Boards.DTOs;
using Cardscape.Application.Workspaces.DTOs;
using Cardscape.IntegrationTests.Fixtures;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// End-to-end coverage of the custom-field definition + value endpoints
/// over HTTP. Each test creates its own workspace + board + list + card
/// so the suite is order-independent.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class CustomFieldTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public CustomFieldTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_List_Rename_Delete_Definition_Lifecycle()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "CF lifecycle");

        // Create
        HttpResponseMessage create = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "Priority", kind = 0, dropdownOptions = (string[]?)null, position = 0 });
        create.IsSuccessStatusCode.Should().BeTrue();
        CustomFieldDefinitionDto created =
            (await create.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;
        created.Name.Should().Be("Priority");
        created.Kind.Should().Be(0);

        // List
        HttpResponseMessage list = await client.GetAsync(
            $"api/boards/{seed.BoardId}/custom-fields/");
        list.IsSuccessStatusCode.Should().BeTrue();
        CustomFieldDefinitionDto[]? rows =
            await list.Content.ReadFromJsonAsync<CustomFieldDefinitionDto[]>();
        rows.Should().NotBeNull().And.HaveCount(1);

        // Rename
        HttpResponseMessage rename = await client.PatchAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/{created.Id}",
            new { newName = "Importance" });
        rename.IsSuccessStatusCode.Should().BeTrue();
        CustomFieldDefinitionDto renamed =
            (await rename.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;
        renamed.Name.Should().Be("Importance");

        // Delete
        HttpResponseMessage delete = await client.DeleteAsync(
            $"api/boards/{seed.BoardId}/custom-fields/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm gone
        HttpResponseMessage after = await client.GetAsync(
            $"api/boards/{seed.BoardId}/custom-fields/");
        CustomFieldDefinitionDto[]? remaining =
            await after.Content.ReadFromJsonAsync<CustomFieldDefinitionDto[]>();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_Dropdown_Without_Options_Returns_BadRequest()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Dropdown options");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "Severity", kind = 3, dropdownOptions = (string[]?)null, position = 0 });
        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Set_And_List_Values_For_Card()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Values");

        // Text field
        HttpResponseMessage create = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "Priority", kind = 0, dropdownOptions = (string[]?)null, position = 0 });
        CustomFieldDefinitionDto field =
            (await create.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;

        // Set value
        HttpResponseMessage set = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/custom-field-values/{field.Id}",
            new { valueJson = "\"high\"" });
        set.IsSuccessStatusCode.Should().BeTrue();
        CustomFieldValueDto setValue =
            (await set.Content.ReadFromJsonAsync<CustomFieldValueDto>())!;
        setValue.ValueJson.Should().Be("\"high\"");

        // List
        HttpResponseMessage list = await client.GetAsync(
            $"api/cards/{seed.CardId}/custom-field-values/");
        list.IsSuccessStatusCode.Should().BeTrue();
        CustomFieldValueDto[]? values =
            await list.Content.ReadFromJsonAsync<CustomFieldValueDto[]>();
        values.Should().NotBeNull().And.HaveCount(1);
        values![0].ValueJson.Should().Be("\"high\"");

        // Clear
        HttpResponseMessage clear = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/custom-field-values/{field.Id}",
            new { valueJson = (string?)null });
        clear.IsSuccessStatusCode.Should().BeTrue();

        // Confirm cleared
        HttpResponseMessage after = await client.GetAsync(
            $"api/cards/{seed.CardId}/custom-field-values/");
        CustomFieldValueDto[]? remaining =
            await after.Content.ReadFromJsonAsync<CustomFieldValueDto[]>();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Set_Dropdown_Value_Rejects_Unknown_Option()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Dropdown invalid");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "Severity", kind = 3, dropdownOptions = new[] { "Low", "High" }, position = 0 });
        CustomFieldDefinitionDto field =
            (await create.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;

        HttpResponseMessage set = await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/custom-field-values/{field.Id}",
            new { valueJson = "\"Critical\"" });
        set.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_Field_Cascades_To_Values()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "Cascade");

        HttpResponseMessage create = await client.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "Priority", kind = 0, dropdownOptions = (string[]?)null, position = 0 });
        CustomFieldDefinitionDto field =
            (await create.Content.ReadFromJsonAsync<CustomFieldDefinitionDto>())!;

        await client.PutAsJsonAsync(
            $"api/cards/{seed.CardId}/custom-field-values/{field.Id}",
            new { valueJson = "\"high\"" });

        HttpResponseMessage delete = await client.DeleteAsync(
            $"api/boards/{seed.BoardId}/custom-fields/{field.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage after = await client.GetAsync(
            $"api/cards/{seed.CardId}/custom-field-values/");
        CustomFieldValueDto[]? remaining =
            await after.Content.ReadFromJsonAsync<CustomFieldValueDto[]>();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task Non_Member_Cannot_Create_Field()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(owner, "Private");

        HttpClient stranger = await CreateAuthenticatedClientAsync();
        HttpResponseMessage create = await stranger.PostAsJsonAsync(
            $"api/boards/{seed.BoardId}/custom-fields/",
            new { name = "X", kind = 0, dropdownOptions = (string[]?)null, position = 0 });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_Fields_Without_Auth_Returns_Unauthorized()
    {
        HttpClient client = _factory.CreateApiClient();
        HttpResponseMessage resp = await client.GetAsync(
            $"api/boards/{Guid.NewGuid()}/custom-fields/");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"cf-user-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "CF User", "Password123!");
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

    private sealed record CustomFieldDefinitionDto(
        Guid Id,
        Guid BoardId,
        string Name,
        int Kind,
        string OptionsJson,
        int Position);

    private sealed record CustomFieldValueDto(
        Guid FieldDefinitionId,
        Guid CardId,
        int Kind,
        string ValueJson);

    private sealed record ListDto(Guid Id);

    private sealed record CardDto(Guid Id, Guid ListId, string Title);
}
