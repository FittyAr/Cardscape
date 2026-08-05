using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cardscape.Application.Abstractions;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Infrastructure.BackgroundJobs;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cardscape.IntegrationTests.BackgroundJobs;

/// <summary>
/// Coverage for the archived-source / archived-list short-circuits
/// the v1.2.0 audit (pass 6) added to
/// <see cref="CloneCardHandler"/>. The previous incarnation
/// cloned regardless of the source card's or the parent list's
/// <c>IsArchived</c> flag, which produced two bugs:
/// <list type="number">
///   <item>archiving a card (which keeps the recurrence rule
///         on the aggregate) and then waiting for the rule to
///         fire produced a clone of an archived card — visible
///         to the user as "phantom" cards appearing in the
///         list until the source was restored;</item>
///   <item>archiving the parent list (a separate "archive the
///         list" workflow in the Web UI) had the same
///         effect, with the bonus that the clone was hidden
///         from the user until the list was restored —
///         effectively untraceable.</item>
/// </list>
/// Both paths now log a deferral and skip the clone (and do
/// NOT advance <c>NextOccurrenceAt</c>, so the rule still
/// fires once the user restores the source).
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class CloneCardHandlerArchiveTests
{
    private readonly CardscapeWebApplicationFactory _factory;

    public CloneCardHandlerArchiveTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Skips_Clone_When_Source_Card_Is_Archived()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "archived-source");

        // Archive the source card via the public endpoint.
        HttpResponseMessage archive = await client.PostAsync(
            $"api/cards/{seed.CardId}/archive", content: null, TestContext.Current.CancellationToken);
        archive.IsSuccessStatusCode.Should().BeTrue();

        int beforeCount = await CountCardsAsync(seed.ListId);

        await InvokeHandlerAsync(seed.CardId);

        int afterCount = await CountCardsAsync(seed.ListId);

        afterCount.Should().Be(beforeCount,
            "the handler must not clone an archived source card; a previous incarnation did, " +
            "producing invisible-to-the-user cards until the source was restored");
    }

    [Fact]
    public async Task Skips_Clone_When_Parent_List_Is_Archived()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Seed seed = await CreateSeedAsync(client, "archived-list");

        // Archive the parent list via the public endpoint.
        HttpResponseMessage archive = await client.PostAsync(
            $"api/lists/{seed.ListId}/archive", content: null, TestContext.Current.CancellationToken);
        archive.IsSuccessStatusCode.Should().BeTrue();

        int beforeCount = await CountCardsAsync(seed.ListId);

        await InvokeHandlerAsync(seed.CardId);

        int afterCount = await CountCardsAsync(seed.ListId);

        afterCount.Should().Be(beforeCount,
            "the handler must not clone into an archived list; cloning into an archived list " +
            "is invisible to the user until the list is restored — effectively untraceable");
    }

    private async Task<int> CountCardsAsync(Guid listId)
    {
        // The /api/lists/{id}/cards endpoint is the public
        // projection; the list endpoint honours the include
        // archived / not filter. We want the absolute count
        // of rows in the list, archived or not, so we hit
        // the card list with includeArchived=true via the
        // dedicated query path.
        using IServiceScope scope = _factory.Services.CreateScope();
        var cards = scope.ServiceProvider.GetRequiredService<Cardscape.Application.Abstractions.Persistence.ICardRepository>();
        var list = (await cards.ListForListAsync(
            new Cardscape.Domain.Lists.BoardListId(listId),
            includeArchived: true,
            TestContext.Current.CancellationToken));
        return list.Count;
    }

    private async Task InvokeHandlerAsync(Guid cardId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        // The handler is registered as IBackgroundJobHandler
        // (singleton, since the dispatcher resolves it once
        // at startup). We resolve the interface here so the
        // test stays decoupled from the concrete class.
        IBackgroundJobHandler handler = scope.ServiceProvider
            .GetServices<IBackgroundJobHandler>()
            .Single(h => h.Type == RecurringCardJobTypes.CloneCard);
        JsonElement payload = JsonSerializer.SerializeToElement(new { cardId });
        await handler.HandleAsync(Guid.NewGuid(), payload, TestContext.Current.CancellationToken);
    }

    // ── seed helpers ─────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"rec-handler-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Recurrence Handler Tester", "Password123!");
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

        return new Seed(card.Id, list.Id);
    }

    private sealed record Seed(Guid CardId, Guid ListId);
    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id, Guid ListId);
}
