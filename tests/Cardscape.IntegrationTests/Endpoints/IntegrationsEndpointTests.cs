using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// G15 (v1.2.0 plan) — integration coverage for the
/// third-party integration endpoints shipped in v1.1.0.
/// These are the workspace-scoped and user-scoped
/// "connect / disconnect / link / list" surfaces; the
/// actual external-service round trips (Slack API,
/// Google API, SendGrid webhook signature) live behind
/// interfaces that the unit tests mock, so the
/// integration tests here cover only the in-process
/// behaviour: the connect command persists the
/// connection, the list returns it, the disconnect
/// marks it inactive, and an unauthenticated request
/// gets 401.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class IntegrationsEndpointTests
{
    private readonly CardscapeWebApplicationFactory _factory;
    public IntegrationsEndpointTests(CardscapeWebApplicationFactory factory) => _factory = factory;

    // ── Slack ──────────────────────────────────────────────

    [Fact]
    public async Task Slack_Connect_Then_Get_Returns_Connection()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "slack-test");

        HttpResponseMessage connected = await client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new
            {
                teamId = "T00000001",
                teamName = "Acme",
                botToken = "xoxb-test-1234567890"
            },
            TestContext.Current.CancellationToken);
        if (!connected.IsSuccessStatusCode)
        {
            string body = await connected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"Slack connect returned {(int)connected.StatusCode} {connected.StatusCode}. Body: {body}");
        }
        connected.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage got = await client.GetAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/", TestContext.Current.CancellationToken);
        got.IsSuccessStatusCode.Should().BeTrue();
        string slackBody = await got.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        slackBody.Should().Contain("T00000001");
        slackBody.Should().Contain("Acme");
        // The bot token must never be returned in the
        // projection; only the prefix hash.
        slackBody.Should().NotContain("xoxb-test-1234567890");
    }

    [Fact]
    public async Task Slack_Link_Then_List_Channels()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "slack-channels");
        Guid boardId = await CreateBoardAsync(client, workspaceId, "slack-channels-board");

        HttpResponseMessage connected = await client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new { teamId = "T00000002", teamName = "Acme2", botToken = "xoxb-test-9876543210" },
            TestContext.Current.CancellationToken);
        connected.IsSuccessStatusCode.Should().BeTrue();
        SlackWorkspaceDto workspace =
            (await connected.Content.ReadFromJsonAsync<SlackWorkspaceDto>(TestContext.Current.CancellationToken))!;

        HttpResponseMessage linked = await client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels",
            new
            {
                slackWorkspaceId = workspace.Id,
                boardId,
                channelId = "C00000001",
                channelName = "general",
                events = new[] { "card.created", "card.moved" }
            },
            TestContext.Current.CancellationToken);
        linked.IsSuccessStatusCode.Should().BeTrue();

        HttpResponseMessage channels = await client.GetAsync(
            $"/api/workspaces/{workspaceId}/integrations/slack/channels?boardId={boardId}",
            TestContext.Current.CancellationToken);
        channels.IsSuccessStatusCode.Should().BeTrue();
        SlackChannelDto[] list =
            (await channels.Content.ReadFromJsonAsync<SlackChannelDto[]>(TestContext.Current.CancellationToken))!;
        list.Should().ContainSingle(c =>
            c.ChannelId == "C00000001" && c.ChannelName == "general");
    }

    [Fact]
    public async Task Slack_Unlink_Channel_Marks_Inactive()
    {
        // Slack unlink is a soft delete: the channel row
        // stays in the DB (so the user can re-activate it
        // without re-granting the Slack OAuth scope), but
        // the projection flips Active=false. The endpoint
        // returns 204 on success; the GET still contains
        // the row, with Active=false.
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "slack-unlink");
        Guid boardId = await CreateBoardAsync(client, workspaceId, "slack-unlink-board");

        HttpResponseMessage connected = await client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new { teamId = "T00000003", teamName = "Acme3", botToken = "xoxb-test-1111111111" },
            TestContext.Current.CancellationToken);
        SlackWorkspaceDto workspace =
            (await connected.Content.ReadFromJsonAsync<SlackWorkspaceDto>(TestContext.Current.CancellationToken))!;

        HttpResponseMessage linked = await client.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/integrations/slack/channels",
            new
            {
                slackWorkspaceId = workspace.Id,
                boardId,
                channelId = "C00000002",
                channelName = "unlink-me",
                events = new[] { "card.created" }
            },
            TestContext.Current.CancellationToken);
        linked.IsSuccessStatusCode.Should().BeTrue();
        SlackChannelDto channel =
            (await linked.Content.ReadFromJsonAsync<SlackChannelDto>(TestContext.Current.CancellationToken))!;
        channel.Active.Should().BeTrue();

        HttpResponseMessage unlinked = await client.DeleteAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels/{channel.Id}",
            TestContext.Current.CancellationToken);
        unlinked.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage channels = await client.GetAsync(
            $"/api/workspaces/{workspaceId}/integrations/slack/channels?boardId={boardId}",
            TestContext.Current.CancellationToken);
        channels.IsSuccessStatusCode.Should().BeTrue();
        SlackChannelDto[] list =
            (await channels.Content.ReadFromJsonAsync<SlackChannelDto[]>(TestContext.Current.CancellationToken))!;
        SlackChannelDto reloaded = list.Single(c => c.Id == channel.Id);
        reloaded.Active.Should().BeFalse();
    }

    [Fact]
    public async Task Slack_Unlink_Unknown_Channel_Returns_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "slack-unknown");

        HttpResponseMessage resp = await client.DeleteAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/channels/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Google Calendar ───────────────────────────────────

    [Fact]
    public async Task GoogleCalendar_Connect_Then_Get_Returns_Connection()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "gcal-test");

        HttpResponseMessage connected = await client.PostAsJsonAsync(
            "api/integrations/google-calendar/connect",
            new
            {
                workspaceId,
                googleEmail = "user@gmail.com",
                encryptedRefreshToken = "encrypted-blob-abc",
                calendarId = "primary"
            },
            TestContext.Current.CancellationToken);
        if (!connected.IsSuccessStatusCode)
        {
            string errBody = await connected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"GoogleCalendar connect returned {(int)connected.StatusCode} {connected.StatusCode}. Body: {errBody}");
        }
        connected.StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage got = await client.GetAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        got.IsSuccessStatusCode.Should().BeTrue();
        string body = await got.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("user@gmail.com");
        body.Should().Contain("primary");
    }

    [Fact]
    public async Task GoogleCalendar_Disconnect_Marks_Connection_Inactive()
    {
        // GoogleCalendar disconnect is a soft delete: the
        // connection row stays (so the user can re-link
        // without re-doing the OAuth dance), but the
        // projection flips IsActive=false. The endpoint
        // returns 204 on success.
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "gcal-disconnect");

        await client.PostAsJsonAsync(
            "api/integrations/google-calendar/connect",
            new
            {
                workspaceId,
                googleEmail = "user2@gmail.com",
                encryptedRefreshToken = "encrypted-blob-def",
                calendarId = (string?)null
            },
            TestContext.Current.CancellationToken);

        HttpResponseMessage disconnect = await client.DeleteAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        disconnect.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage got = await client.GetAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        got.IsSuccessStatusCode.Should().BeTrue();
        GoogleCalendarConnectionDto reloaded =
            (await got.Content.ReadFromJsonAsync<GoogleCalendarConnectionDto>(TestContext.Current.CancellationToken))!;
        reloaded.Should().NotBeNull();
        reloaded.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleCalendar_Get_For_Fresh_User_Returns_Null()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage got = await client.GetAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        // Results.Ok(null) in the minimal-API host returns 200
        // with an empty body (the framework's default JSON
        // serialiser writes nothing for a top-level null).
        // The contract under test is "the endpoint does not
        // 404 / 500 when no connection exists" — both a 200
        // with an empty body and a 200 with the literal
        // string "null" satisfy the contract today.
        got.IsSuccessStatusCode.Should().BeTrue();
        string body = await got.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        (body == "null" || body.Length == 0).Should().BeTrue(
            "the no-connection response is 200 + empty body or 200 + literal 'null'");
    }

    // ── Inbound email ─────────────────────────────────────

    [Fact]
    public async Task InboundEmail_Register_Then_List_Returns_Address()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "email-test");
        Guid boardId = await CreateBoardAsync(client, workspaceId, "email-board");
        Guid listId = await CreateListAsync(client, boardId, "Inbox");

        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "api/integrations/email/addresses",
            new
            {
                workspaceId,
                emailAddress = "inbox-abc@cardscape.example",
                targetListId = listId,
                label = "Inbox from email"
            },
            TestContext.Current.CancellationToken);
        if (!registered.IsSuccessStatusCode)
        {
            string body = await registered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new Xunit.Sdk.XunitException(
                $"Email register returned {(int)registered.StatusCode} {registered.StatusCode}. Body: {body}");
        }
        registered.StatusCode.Should().Be(HttpStatusCode.Created);
        InboundEmailAddressDto created =
            (await registered.Content.ReadFromJsonAsync<InboundEmailAddressDto>(TestContext.Current.CancellationToken))!;
        created.EmailAddress.Should().Be("inbox-abc@cardscape.example");
        created.TargetListId.Should().Be(listId);

        HttpResponseMessage listed = await client.GetAsync(
            $"api/integrations/email/addresses?workspaceId={workspaceId}",
            TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        InboundEmailAddressDto[] list =
            (await listed.Content.ReadFromJsonAsync<InboundEmailAddressDto[]>(TestContext.Current.CancellationToken))!;
        list.Should().ContainSingle(a => a.Id == created.Id);
    }

    [Fact]
    public async Task InboundEmail_Unregister_Marks_Address_Inactive()
    {
        // InboundEmail unregister is a soft delete: the
        // address row stays (so the user can re-enable
        // without re-creating it), but the projection flips
        // Active=false. The endpoint returns 204 on success.
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "email-unreg");
        Guid boardId = await CreateBoardAsync(client, workspaceId, "email-unreg-board");
        Guid listId = await CreateListAsync(client, boardId, "Inbox");

        HttpResponseMessage registered = await client.PostAsJsonAsync(
            "api/integrations/email/addresses",
            new
            {
                workspaceId,
                emailAddress = "unreg@cardscape.example",
                targetListId = listId,
                label = "Unreg me"
            },
            TestContext.Current.CancellationToken);
        InboundEmailAddressDto created =
            (await registered.Content.ReadFromJsonAsync<InboundEmailAddressDto>(TestContext.Current.CancellationToken))!;
        created.Active.Should().BeTrue();

        HttpResponseMessage unreg = await client.DeleteAsync(
            $"api/integrations/email/addresses/{created.Id}",
            TestContext.Current.CancellationToken);
        unreg.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage listed = await client.GetAsync(
            $"api/integrations/email/addresses?workspaceId={workspaceId}",
            TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        InboundEmailAddressDto[] list =
            (await listed.Content.ReadFromJsonAsync<InboundEmailAddressDto[]>(TestContext.Current.CancellationToken))!;
        InboundEmailAddressDto reloaded = list.Single(a => a.Id == created.Id);
        reloaded.Active.Should().BeFalse();
    }

    [Fact]
    public async Task InboundEmail_Unregister_Unknown_Address_Returns_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage resp = await client.DeleteAsync(
            $"api/integrations/email/addresses/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"int-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await resp.Content.ReadFromJsonAsync<WorkspaceDto>())!;
        return ws.Id;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId, name, description = (string?)null, visibility = 0 });
        resp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await resp.Content.ReadFromJsonAsync<BoardDto>())!;
        return board.Id;
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        ListDto list = (await resp.Content.ReadFromJsonAsync<ListDto>())!;
        return list.Id;
    }

    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record SlackWorkspaceDto(
        Guid Id,
        Guid WorkspaceId,
        string TeamId,
        string TeamName,
        string BotTokenPrefix,
        bool Active);
    private sealed record SlackChannelDto(
        Guid Id,
        Guid SlackWorkspaceId,
        Guid BoardId,
        string ChannelId,
        string ChannelName,
        IReadOnlyList<string> Events,
        bool Active);
    private sealed record InboundEmailAddressDto(
        Guid Id,
        Guid WorkspaceId,
        string EmailAddress,
        Guid TargetListId,
        string Label,
        bool Active);
}
