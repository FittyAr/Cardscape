using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class KanbanImportFidelityTests
{
    private const string Archive = """
        [
          {
            "name": "Imported Fidelity Board",
            "description": "Preview and apply must agree",
            "labels": [
              { "id": "label-1", "name": "Important", "color": "red" }
            ],
            "lists": [
              { "id": "list-1", "name": "Todo" }
            ],
            "cards": [
              {
                "id": "card-1",
                "name": "Keep label relation",
                "description": "Imported card",
                "listId": "list-1",
                "labelIds": ["label-1"]
              }
            ],
            "members": [
              { "id": "member-1", "fullName": "Imported Member" }
            ]
          }
        ]
        """;

    private readonly CardscapeWebApplicationFactory _factory;

    public KanbanImportFidelityTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PreviewAndApply_ForSameArchive_ReturnMatchingCounts_AndPersistCardLabels()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client);

        HttpResponseMessage previewResponse = await PostArchiveAsync(
            client, "preview", workspaceId);
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportResultDto preview = (await previewResponse.Content.ReadFromJsonAsync<ImportResultDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;

        preview.ImportedBoardIds.Should().BeEmpty();
        preview.ImportedListIds.Should().BeEmpty();
        preview.ImportedCardIds.Should().BeEmpty();
        preview.ImportedLabelIds.Should().BeEmpty();
        preview.Preview.Should().BeEquivalentTo(new ImportPreviewDto(
            1, 1, 1, 1, 1,
            ["Imported Fidelity Board"], ["Todo"], ["Keep label relation"], false));

        string boardsAfterPreview = await client.GetStringAsync(
            $"api/boards/?workspaceId={workspaceId}", TestContext.Current.CancellationToken);
        boardsAfterPreview.Should().NotContain("Imported Fidelity Board");

        HttpResponseMessage applyResponse = await PostArchiveAsync(
            client, "apply", workspaceId);
        applyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ImportResultDto applied = (await applyResponse.Content.ReadFromJsonAsync<ImportResultDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;

        applied.Preview.Should().BeEquivalentTo(
            preview.Preview,
            options => options.Excluding(summary => summary.WasApplied));
        applied.Preview.WasApplied.Should().BeTrue();
        applied.ImportedBoardIds.Should().ContainSingle();
        applied.ImportedListIds.Should().ContainSingle();
        applied.ImportedCardIds.Should().ContainSingle();
        applied.ImportedLabelIds.Should().ContainSingle();

        CardDto card = (await client.GetFromJsonAsync<CardDto>(
            $"api/cards/{applied.ImportedCardIds.Single()}",
            TestJson.Options, TestContext.Current.CancellationToken))!;
        card.LabelCount.Should().Be(1);
    }

    [Fact]
    public async Task AmbiguousKanbanImportRoute_IsRemoved()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client);

        using MultipartFormDataContent content = CreateArchiveContent(workspaceId);
        HttpResponseMessage response = await client.PostAsync(
            "api/imports/kanban", content, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        var registration = new RegisterRequest(
            $"import-{Guid.NewGuid():N}@cardscape.local", "Import Fidelity", "Password123!");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", registration, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"Import-{Guid.NewGuid():N}" },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        WorkspaceDto workspace = (await response.Content.ReadFromJsonAsync<WorkspaceDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        return workspace.Id;
    }

    private static async Task<HttpResponseMessage> PostArchiveAsync(
        HttpClient client,
        string action,
        Guid workspaceId)
    {
        using MultipartFormDataContent content = CreateArchiveContent(workspaceId);
        return await client.PostAsync(
            $"api/imports/kanban/{action}", content, TestContext.Current.CancellationToken);
    }

    private static MultipartFormDataContent CreateArchiveContent(Guid workspaceId)
    {
        var content = new MultipartFormDataContent();
        var archive = new ByteArrayContent(Encoding.UTF8.GetBytes(Archive));
        archive.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(archive, "file", "boards.json");
        content.Add(new StringContent(workspaceId.ToString()), "targetWorkspaceId");
        return content;
    }

    private sealed record WorkspaceDto(Guid Id);

    private sealed record CardDto(Guid Id, int LabelCount);

    private sealed record ImportResultDto(
        IReadOnlyList<Guid> ImportedBoardIds,
        IReadOnlyList<Guid> ImportedListIds,
        IReadOnlyList<Guid> ImportedCardIds,
        IReadOnlyList<Guid> ImportedLabelIds,
        ImportPreviewDto Preview);

    private sealed record ImportPreviewDto(
        int BoardCount,
        int ListCount,
        int CardCount,
        int LabelCount,
        int MemberCount,
        IReadOnlyList<string> SampleBoardNames,
        IReadOnlyList<string> SampleListNames,
        IReadOnlyList<string> SampleCardNames,
        bool WasApplied);
}
