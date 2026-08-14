using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Cardscape.Application.Authentication.DTOs;
using Cardscape.Application.Integrations.GoogleCalendar;
using Cardscape.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            (await connected.Content.ReadFromJsonAsync<SlackWorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

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
            (await channels.Content.ReadFromJsonAsync<SlackChannelDto[]>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
            (await connected.Content.ReadFromJsonAsync<SlackWorkspaceDto>(TestJson.Options, TestContext.Current.CancellationToken))!;

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
            (await linked.Content.ReadFromJsonAsync<SlackChannelDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
            (await channels.Content.ReadFromJsonAsync<SlackChannelDto[]>(TestJson.Options, TestContext.Current.CancellationToken))!;
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

    [Fact]
    public async Task Slack_Connect_ByWorkspaceMember_ReturnsForbiddenWithoutCreatingConnection()
    {
        (HttpClient owner, AuthResponse _) = await CreateAuthenticatedSessionAsync();
        Guid workspaceId = await CreateWorkspaceAsync(owner, "slack-owner-only");
        (HttpClient member, AuthResponse memberAuth) = await CreateAuthenticatedSessionAsync();
        HttpResponseMessage addMember = await owner.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/members",
            new { userId = memberAuth.User.Id, role = "member" },
            TestContext.Current.CancellationToken);
        addMember.EnsureSuccessStatusCode();

        HttpResponseMessage attempt = await member.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new { teamId = "T-MEMBER", teamName = "Member Team", botToken = "xoxb-member" },
            TestContext.Current.CancellationToken);

        attempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage unchanged = await owner.GetAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/",
            TestContext.Current.CancellationToken);
        unchanged.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Slack_Reconnect_ByOwner_RotatesTeamAndTokenOnExistingConnection()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(owner, "slack-reconnect");
        const string firstToken = "xoxb-first-token";
        const string secondToken = "xoxb-second-token";

        SlackWorkspaceDto first = await ConnectSlackAsync(
            owner, workspaceId, "T-FIRST", "First Team", firstToken);
        SlackWorkspaceDto second = await ConnectSlackAsync(
            owner, workspaceId, "T-SECOND", "Second Team", secondToken);

        second.Id.Should().Be(first.Id);
        second.TeamId.Should().Be("T-SECOND");
        second.TeamName.Should().Be("Second Team");
        second.BotTokenPrefix.Should().Be(HashPrefix(secondToken));
        second.BotTokenPrefix.Should().NotBe(first.BotTokenPrefix);
        second.Active.Should().BeTrue();
    }

    [Fact]
    public async Task Slack_Reconnect_WithInvalidTeam_LeavesExistingConnectionUnchanged()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(owner, "slack-invalid-reconnect");
        SlackWorkspaceDto original = await ConnectSlackAsync(
            owner, workspaceId, "T-ORIGINAL", "Original Team", "xoxb-original-token");

        HttpResponseMessage invalid = await owner.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new { teamId = "T-REJECTED", teamName = "", botToken = "xoxb-rejected-token" },
            TestContext.Current.CancellationToken);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        SlackWorkspaceDto unchanged = (await owner.GetFromJsonAsync<SlackWorkspaceDto>(
            $"api/workspaces/{workspaceId}/integrations/slack/",
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
        unchanged.Id.Should().Be(original.Id);
        unchanged.TeamId.Should().Be(original.TeamId);
        unchanged.TeamName.Should().Be(original.TeamName);
        unchanged.BotTokenPrefix.Should().Be(original.BotTokenPrefix);
        unchanged.Active.Should().BeTrue();
    }

    [Fact]
    public async Task Slack_ChannelRoutes_WithDifferentWorkspace_ReturnForbiddenWithoutMutation()
    {
        HttpClient owner = await CreateAuthenticatedClientAsync();
        Guid sourceWorkspaceId = await CreateWorkspaceAsync(owner, "slack-source");
        Guid otherWorkspaceId = await CreateWorkspaceAsync(owner, "slack-other");
        Guid boardId = await CreateBoardAsync(owner, sourceWorkspaceId, "slack-source-board");
        SlackWorkspaceDto slack = await ConnectSlackAsync(
            owner, sourceWorkspaceId, "T-SOURCE", "Source Team", "xoxb-source-token");

        HttpResponseMessage linked = await owner.PostAsJsonAsync(
            $"api/workspaces/{sourceWorkspaceId}/integrations/slack/channels",
            new
            {
                slackWorkspaceId = slack.Id,
                boardId,
                channelId = "C-SOURCE",
                channelName = "source",
                events = new[] { "card.created" }
            },
            TestContext.Current.CancellationToken);
        linked.EnsureSuccessStatusCode();
        SlackChannelDto channel = (await linked.Content.ReadFromJsonAsync<SlackChannelDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;

        HttpResponseMessage crossList = await owner.GetAsync(
            $"api/workspaces/{otherWorkspaceId}/integrations/slack/channels?boardId={boardId}",
            TestContext.Current.CancellationToken);
        crossList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage crossLink = await owner.PostAsJsonAsync(
            $"api/workspaces/{otherWorkspaceId}/integrations/slack/channels",
            new
            {
                slackWorkspaceId = slack.Id,
                boardId,
                channelId = "C-CROSS",
                channelName = "cross",
                events = new[] { "card.created" }
            },
            TestContext.Current.CancellationToken);
        crossLink.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        HttpResponseMessage crossUnlink = await owner.DeleteAsync(
            $"api/workspaces/{otherWorkspaceId}/integrations/slack/channels/{channel.Id}",
            TestContext.Current.CancellationToken);
        crossUnlink.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        SlackChannelDto[] sourceChannels = (await owner.GetFromJsonAsync<SlackChannelDto[]>(
            $"api/workspaces/{sourceWorkspaceId}/integrations/slack/channels?boardId={boardId}",
            TestJson.Options,
            TestContext.Current.CancellationToken))!;
        sourceChannels.Should().ContainSingle(c => c.Id == channel.Id && c.Active);
        sourceChannels.Should().NotContain(c => c.ChannelId == "C-CROSS");
    }

    // ── Google Calendar ───────────────────────────────────

    [Fact]
    public async Task GitHub_Operations_ForRepositoryNotLinkedToBoard_ReturnForbidden()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        Guid workspaceId = await CreateWorkspaceAsync(client, "github-boundary");
        Guid boardId = await CreateBoardAsync(client, workspaceId, "github-board");
        Guid listId = await CreateListAsync(client, boardId, "github-list");
        HttpResponseMessage cardResponse = await client.PostAsJsonAsync(
            "api/cards/", new { listId, title = "GitHub card", description = (string?)null },
            TestContext.Current.CancellationToken);
        cardResponse.EnsureSuccessStatusCode();
        Guid cardId = (await cardResponse.Content.ReadFromJsonAsync<CardDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!.Id;

        HttpResponseMessage pulls = await client.GetAsync(
            $"api/integrations/github/pulls?boardId={boardId}&repoFullName=other/repo&state=open",
            TestContext.Current.CancellationToken);
        HttpResponseMessage linkPull = await client.PostAsJsonAsync(
            "api/integrations/github/pulls/link",
            new { cardId, repoFullName = "other/repo", pullRequestNumber = 42 },
            TestContext.Current.CancellationToken);
        HttpResponseMessage issue = await client.PostAsJsonAsync(
            "api/integrations/github/issues",
            new { cardId, repoFullName = "other/repo", title = "No", body = "No" },
            TestContext.Current.CancellationToken);

        pulls.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        linkPull.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        issue.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GoogleCalendar_OAuthRoundTrip_PreservesIdentityAndCreatesConnection()
    {
        WebApplicationFactory<Program> factory = CreateGoogleOAuthFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await AuthenticateAsync(client);
        Guid workspaceId = await CreateWorkspaceAsync(client, "gcal-test");

        HttpResponseMessage started = await client.GetAsync(
            $"api/integrations/google-calendar/start?workspaceId={workspaceId}&returnUrl={Uri.EscapeDataString("https://evil.example/steal")}",
            TestContext.Current.CancellationToken);
        started.StatusCode.Should().Be(HttpStatusCode.Redirect);
        Uri redirect = started.Headers.Location!;
        string state = ParseQueryValue(redirect.Query, "state");
        state.Should().NotBeNullOrWhiteSpace();

        AuthenticationHeaderValue authorization = client.DefaultRequestHeaders.Authorization!;
        client.DefaultRequestHeaders.Authorization = null;
        HttpResponseMessage callback = await client.GetAsync(
            $"api/integrations/google-calendar/callback?code=test-code&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);
        callback.StatusCode.Should().Be(HttpStatusCode.Redirect);
        callback.Headers.Location.Should().Be("/settings/integrations/google-calendar?connected=1");

        client.DefaultRequestHeaders.Authorization = authorization;

        HttpResponseMessage got = await client.GetAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        GoogleCalendarConnectionDto dto = (await got.Content.ReadFromJsonAsync<GoogleCalendarConnectionDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        dto.GoogleEmail.Should().Be("oauth-user@gmail.com");
        dto.WorkspaceId.Should().Be(workspaceId);
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GoogleCalendar_RemovedCredentialAndWebhookRoutes_ReturnNotFound()
    {
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage connect = await client.PostAsJsonAsync(
            "api/integrations/google-calendar/connect", new { }, TestContext.Current.CancellationToken);
        HttpResponseMessage watch = await client.PostAsync(
            "api/integrations/google-calendar/watch", null, TestContext.Current.CancellationToken);
        HttpResponseMessage webhook = await client.PostAsync(
            "api/integrations/google-calendar/webhook", null, TestContext.Current.CancellationToken);

        connect.StatusCode.Should().Be(HttpStatusCode.NotFound);
        watch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        webhook.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GoogleCalendar_Start_RequiresAuthentication_AndCallbackRejectsTamperedState()
    {
        WebApplicationFactory<Program> factory = CreateGoogleOAuthFactory();
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        HttpResponseMessage anonymous = await client.GetAsync(
            $"api/integrations/google-calendar/start?workspaceId={Guid.NewGuid()}",
            TestContext.Current.CancellationToken);
        HttpResponseMessage tampered = await client.GetAsync(
            "api/integrations/google-calendar/callback?code=test-code&state=tampered",
            TestContext.Current.CancellationToken);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tampered.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tampered.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Contain("google_calendar.state_invalid");
    }

    [Fact]
    public async Task GoogleCalendar_Get_For_Fresh_User_Returns_Default_Not_Connected_Dto()
    {
        // BETA-2-UI-#6 — see src/Cardscape.Api/Endpoints/Integrations/GoogleCalendarEndpoints.cs.
        // The handler used to return Results.Ok(null) which
        // produced a 200 with a 0-byte body, breaking the Blazor
        // WASM client's ReadFromJsonAsync<> (threw
        // JsonException: ExpectedJsonTokens and the page got
        // stuck on "Loading…"). The fix is to always return a
        // non-null default DTO with IsActive=false so the client
        // can deserialise a "not connected" state and render the
        // connect form.
        HttpClient client = await CreateAuthenticatedClientAsync();
        HttpResponseMessage got = await client.GetAsync(
            "api/integrations/google-calendar/", TestContext.Current.CancellationToken);
        got.IsSuccessStatusCode.Should().BeTrue();
        GoogleCalendarConnectionDto? dto = await got.Content.ReadFromJsonAsync<GoogleCalendarConnectionDto>(TestJson.Options, TestContext.Current.CancellationToken);
        dto.Should().NotBeNull();
        dto!.IsActive.Should().BeFalse(
            "a user with no Google Calendar connection gets a default DTO with IsActive=false");
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
            (await registered.Content.ReadFromJsonAsync<InboundEmailAddressDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
        created.EmailAddress.Should().Be("inbox-abc@cardscape.example");
        created.TargetListId.Should().Be(listId);

        HttpResponseMessage listed = await client.GetAsync(
            $"api/integrations/email/addresses?workspaceId={workspaceId}",
            TestContext.Current.CancellationToken);
        listed.IsSuccessStatusCode.Should().BeTrue();
        InboundEmailAddressDto[] list =
            (await listed.Content.ReadFromJsonAsync<InboundEmailAddressDto[]>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
            (await registered.Content.ReadFromJsonAsync<InboundEmailAddressDto>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
            (await listed.Content.ReadFromJsonAsync<InboundEmailAddressDto[]>(TestJson.Options, TestContext.Current.CancellationToken))!;
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
        (HttpClient client, _) = await CreateAuthenticatedSessionAsync();
        return client;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedSessionAsync()
    {
        HttpClient client = _factory.CreateApiClient();
        string email = $"int-{Guid.NewGuid():N}@cardscape.local";
        RegisterRequest register = new(email, "Tester", "Password123!");
        HttpResponseMessage r = await client.PostAsJsonAsync("api/auth/register", register);
        r.IsSuccessStatusCode.Should().BeTrue();
        AuthResponse auth = (await r.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        string email = $"int-{Guid.NewGuid():N}@cardscape.local";
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "api/auth/register", new RegisterRequest(email, "Tester", "Password123!"),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        AuthResponse auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    private WebApplicationFactory<Program> CreateGoogleOAuthFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Integrations:GoogleCalendar:ClientId"] = "test-client",
                    ["Integrations:GoogleCalendar:ClientSecret"] = "test-secret"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("google-oauth")
                    .ConfigurePrimaryHttpMessageHandler(() => new GoogleOAuthHandler());
            });
        });

    private static string ParseQueryValue(string query, string name)
    {
        foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 2 && string.Equals(pair[0], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[1]);
            }
        }
        return string.Empty;
    }

    private sealed class GoogleOAuthHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string json = request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
                ? "{\"refresh_token\":\"refresh-token\",\"access_token\":\"access-token\"}"
                : "{\"email\":\"oauth-user@gmail.com\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<SlackWorkspaceDto> ConnectSlackAsync(
        HttpClient client,
        Guid workspaceId,
        string teamId,
        string teamName,
        string botToken)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"api/workspaces/{workspaceId}/integrations/slack/connect",
            new { teamId, teamName, botToken },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SlackWorkspaceDto>(
            TestJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static string HashPrefix(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
            .ToLowerInvariant()[..8];

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/workspaces/", new { name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        WorkspaceDto ws = (await resp.Content.ReadFromJsonAsync<WorkspaceDto>(TestJson.Options))!;
        return ws.Id;
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/",
            new { workspaceId, name, description = (string?)null, visibility = "private" });
        resp.IsSuccessStatusCode.Should().BeTrue();
        BoardDto board = (await resp.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options))!;
        return board.Id;
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId, string name)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/", new { boardId, name });
        resp.IsSuccessStatusCode.Should().BeTrue();
        ListDto list = (await resp.Content.ReadFromJsonAsync<ListDto>(TestJson.Options))!;
        return list.Id;
    }

    private sealed record WorkspaceDto(Guid Id);
    private sealed record BoardDto(Guid Id, Guid WorkspaceId);
    private sealed record ListDto(Guid Id);
    private sealed record CardDto(Guid Id);
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
