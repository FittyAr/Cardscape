using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions.Authentication;
using Cardscape.Application.Abstractions.Persistence;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Domain.Webhooks;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cardscape.IntegrationTests.Endpoints;

[Collection(CardscapeApi.Name)]
public sealed class WebhookDeliverySecurityTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public WebhookDeliverySecurityTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task WebhookCreation_StoresProtectedSecret_NotSigningKeyMaterial()
    {
        const string cleartext = "database-leak-must-not-forge";
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid boardId = await CreateBoardAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"api/boards/{boardId}/webhooks/",
            new { url = "https://example.com/cardscape-hook", secret = cleartext, events = new[] { "card.created" } },
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid endpointId = await ReadEndpointIdAsync(response);

        using IServiceScope scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        WebhookEndpoint endpoint = (await repository.GetByIdAsync(
            new WebhookEndpointId(endpointId), TestContext.Current.CancellationToken))!;
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        endpoint.ProtectedSecret.Should().NotBe(cleartext);
        endpoint.ProtectedSecret.Should().NotBe(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cleartext))).ToLowerInvariant());
        protector.Unprotect(endpoint.ProtectedSecret).Should().Be(cleartext);
    }

    [Fact]
    public void WebhookDeliveryHttpClient_DisablesAutomaticRedirects()
    {
        var factory = _factory.Services.GetRequiredService<IHttpMessageHandlerFactory>();
        using HttpMessageHandler rootHandler = factory.CreateHandler(WebhookDeliveryHandler.WebhookHttpClientName);
        HttpMessageHandler handler = rootHandler;
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        handler.Should().BeOfType<HttpClientHandler>()
            .Which.AllowAutoRedirect.Should().BeFalse();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        var registration = new RegisterRequest(
            $"webhook-security-{Guid.NewGuid():N}@cardscape.local", "Webhook Security", "Password123!");
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", registration, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client)
    {
        HttpResponseMessage workspaceResponse = await client.PostAsJsonAsync(
            "api/workspaces/", new { name = $"Webhook-{Guid.NewGuid():N}" },
            TestContext.Current.CancellationToken);
        workspaceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument workspace = await JsonDocument.ParseAsync(
            await workspaceResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Guid workspaceId = workspace.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage boardResponse = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId, name = "Webhook Security", description = (string?)null, visibility = "private" },
            TestContext.Current.CancellationToken);
        boardResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using JsonDocument board = await JsonDocument.ParseAsync(
            await boardResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return board.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ReadEndpointIdAsync(HttpResponseMessage response)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty("endpoint").GetProperty("id").GetGuid();
    }
}
