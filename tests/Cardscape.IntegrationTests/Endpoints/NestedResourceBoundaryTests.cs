using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class NestedResourceBoundaryTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public NestedResourceBoundaryTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AttachmentItemOperations_WithMismatchedRouteCard_ReturnNotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        ResourceSeed first = await CreateSeedAsync(client, "attachment-owner");
        ResourceSeed second = await CreateSeedAsync(client, "attachment-wrong-parent");

        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("bounded attachment"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "boundary.txt");

        HttpResponseMessage uploaded = await client.PostAsync(
            $"api/cards/{first.CardId}/attachments/", form, TestContext.Current.CancellationToken);
        uploaded.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid attachmentId = await ReadNestedIdAsync(uploaded, "id");

        HttpResponseMessage wrongDownload = await client.GetAsync(
            $"api/cards/{second.CardId}/attachments/{attachmentId}/download",
            TestContext.Current.CancellationToken);
        HttpResponseMessage wrongDelete = await client.DeleteAsync(
            $"api/cards/{second.CardId}/attachments/{attachmentId}/",
            TestContext.Current.CancellationToken);

        wrongDownload.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage canonicalDownload = await client.GetAsync(
            $"api/cards/{first.CardId}/attachments/{attachmentId}/download",
            TestContext.Current.CancellationToken);
        canonicalDownload.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WebhookItemOperations_WithMismatchedRouteBoard_ReturnNotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        ResourceSeed first = await CreateSeedAsync(client, "webhook-owner");
        ResourceSeed second = await CreateSeedAsync(client, "webhook-wrong-parent");

        HttpResponseMessage created = await client.PostAsJsonAsync(
            $"api/boards/{first.BoardId}/webhooks/",
            new { url = "https://example.com/cardscape-hook", secret = "boundary-secret", events = new[] { "card.created" } },
            TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid endpointId = await ReadNestedIdAsync(created, "endpoint", "id");

        HttpResponseMessage wrongUpdate = await client.PatchAsJsonAsync(
            $"api/boards/{second.BoardId}/webhooks/{endpointId}",
            new { active = false }, TestContext.Current.CancellationToken);
        HttpResponseMessage wrongDeliveries = await client.GetAsync(
            $"api/boards/{second.BoardId}/webhooks/{endpointId}/deliveries",
            TestContext.Current.CancellationToken);
        HttpResponseMessage wrongDelete = await client.DeleteAsync(
            $"api/boards/{second.BoardId}/webhooks/{endpointId}",
            TestContext.Current.CancellationToken);

        wrongUpdate.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongDeliveries.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);

        HttpResponseMessage canonicalDeliveries = await client.GetAsync(
            $"api/boards/{first.BoardId}/webhooks/{endpointId}/deliveries",
            TestContext.Current.CancellationToken);
        canonicalDeliveries.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        var registration = new RegisterRequest(
            $"nested-{Guid.NewGuid():N}@cardscape.local", "Nested Boundary", "Password123!");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", registration, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ResourceSeed> CreateSeedAsync(HttpClient client, string name)
    {
        HttpResponseMessage workspaceResponse = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"WS-{name}-{Guid.NewGuid():N}" },
            TestContext.Current.CancellationToken);
        workspaceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid workspaceId = await ReadNestedIdAsync(workspaceResponse, "id");

        HttpResponseMessage boardResponse = await client.PostAsJsonAsync(
            "api/boards/", new { workspaceId, name, description = (string?)null, visibility = "private" },
            TestContext.Current.CancellationToken);
        boardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid boardId = await ReadNestedIdAsync(boardResponse, "id");

        HttpResponseMessage listResponse = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name = "Todo" }, TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid listId = await ReadNestedIdAsync(listResponse, "id");

        HttpResponseMessage cardResponse = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title = "Card", description = (string?)null },
            TestContext.Current.CancellationToken);
        cardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid cardId = await ReadNestedIdAsync(cardResponse, "id");

        return new ResourceSeed(boardId, cardId);
    }

    private static async Task<Guid> ReadNestedIdAsync(HttpResponseMessage response, params string[] path)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        JsonElement current = document.RootElement;
        foreach (string segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current.GetGuid();
    }

    private sealed record ResourceSeed(Guid BoardId, Guid CardId);
}
