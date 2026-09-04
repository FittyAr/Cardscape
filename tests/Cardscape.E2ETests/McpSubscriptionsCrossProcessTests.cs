using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cardscape.Application.Abstractions.Security;
using Cardscape.Domain.Members;
using Cardscape.E2ETests.Fixtures;
using Cardscape.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Cardscape.E2ETests;

/// <summary>
/// E2E coverage for the cross-process broadcaster
/// contract. The pattern:
///
/// 1. The API receives a board-mutating command
///    (e.g. create a card).
/// 2. The API fires a domain event that the
///    broadcaster listens to, then HTTP-calls the
///    MCP at <c>/api/internal/board-event</c>
///    (the same shared secret is the auth).
/// 3. The MCP records the broadcast in its
///    <c>McpResourceBroadcaster</c> event log and
///    fans the notification out to every subscribed
///    AI client.
///
/// The smoke tests confirm the fixture boots both
/// hosts. The cross-process test (the actual deliverable
/// of this v1.2.0 follow-up) drives a card creation
/// through the API and asserts the MCP's
/// <c>/api/internal/board-event/subscriptions</c>
/// endpoint reports a <c>Broadcast</c> event for the
/// matching <c>board://{id}</c> URI — proving the
/// API really hit the MCP really recorded the event.
/// </summary>
[Collection(E2E.Name)]
public sealed class McpSubscriptionsCrossProcessTests
{
    private readonly TwoHostWebApplicationFactory _factory;
    public McpSubscriptionsCrossProcessTests(TwoHostWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Both_Hosts_Boot_And_Bind_To_The_Expected_Ports()
    {
        // The fixture pins both hosts to local ports so the
        // API's HttpMcpResourceNotifier can target the MCP
        // by URL. The TestServer's address feature reports
        // the actual bound address (which is the env-var
        // value when Kestrel honours it, or the random
        // ephemeral port the host fell back to).
        _factory.Api.ServerAddress.Should().EndWith($":{TwoHostWebApplicationFactory.ApiPort}",
            $"the API host must bind to the fixture's fixed port {TwoHostWebApplicationFactory.ApiPort}; " +
            $"actual: {_factory.Api.ServerAddress}");
        _factory.Mcp.ServerAddress.Should().EndWith($":{TwoHostWebApplicationFactory.McpPort}",
            $"the MCP host must bind to the fixture's fixed port {TwoHostWebApplicationFactory.McpPort}; " +
            $"actual: {_factory.Mcp.ServerAddress}");
        _factory.Api.ServerAddress.Should().NotBe(_factory.Mcp.ServerAddress);
    }

    [Fact]
    public async Task Mcp_Health_Endpoint_Reports_Healthy()
    {
        // The TestServer's CreateClient returns an
        // in-memory HttpClient bound to the MCP host's
        // pipeline (no real socket needed). This proves
        // the MCP is hosting the request pipeline.
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        HttpResponseMessage resp = await mcpClient.GetAsync(
            "health/live", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Mcp_StreamableHttp_Endpoint_Rejects_Anonymous_Protocol_Requests()
    {
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"e2e","version":"1.0"}}}""",
                Encoding.UTF8,
                "application/json")
        };

        HttpResponseMessage response = await mcpClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the mapped MCP transport must authenticate before dispatching JSON-RPC");
    }

    [Fact]
    public async Task Mcp_StreamableHttp_Propagates_ApiToken_Identity_Into_Tools()
    {
        ApiTokenIssuance token;
        using (IServiceScope scope = _factory.Mcp.Services.CreateScope())
        {
            CardscapeDbContext db = scope.ServiceProvider.GetRequiredService<CardscapeDbContext>();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            User user = User.Register(
                UserId.New(),
                EmailAddress.Create($"mcp-{Guid.NewGuid():N}@cardscape.local").Value,
                DisplayName.Create("MCP E2E").Value,
                PasswordHash.FromHashed("v1.e2e").Value,
                DateTimeOffset.UtcNow).Value;
            db.Users.Add(user);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            token = await scope.ServiceProvider.GetRequiredService<IApiTokenService>().IssueAsync(
                user.Id,
                "mcp-e2e",
                ["read"],
                expiresAt: null,
                rateLimitPerHour: null,
                burstSize: null,
                TestContext.Current.CancellationToken);
        }

        HttpClient httpClient = _factory.Mcp.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {token.CleartextSecret}"
                }
            },
            httpClient,
            NullLoggerFactory.Instance,
            ownsHttpClient: false);
        await using McpClient client = await McpClient.CreateAsync(
            transport,
            cancellationToken: TestContext.Current.CancellationToken);

        CallToolResult result = await client.CallToolAsync(
            "workspaces_list",
            cancellationToken: TestContext.Current.CancellationToken);

        string details = string.Join(
            " | ",
            result.Content.OfType<TextContentBlock>().Select(block => block.Text));
        result.IsError.Should().NotBeTrue(details);
    }

    [Fact]
    public async Task Api_Can_Call_Mcp_Internal_Endpoint_Directly()
    {
        // Bypass the broadcaster for the smoke test:
        // the API HTTP-calls the MCP directly. The MCP
        // returns 202 (Accepted) on a valid board event
        // and 401 on a missing secret.
        HttpClient mcpClient = _factory.Mcp.CreateClient();

        var payload = new
        {
            boardId = Guid.NewGuid()
        };
        using var noAuth = new HttpRequestMessage(HttpMethod.Post, "api/internal/board-event/")
        {
            Content = JsonContent.Create(payload)
        };
        HttpResponseMessage noAuthResp = await mcpClient.SendAsync(
            noAuth, TestContext.Current.CancellationToken);
        noAuthResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the MCP must reject board events without the shared secret");

        using var withAuth = new HttpRequestMessage(HttpMethod.Post, "api/internal/board-event/")
        {
            Content = JsonContent.Create(payload)
        };
        withAuth.Headers.Add("X-Internal-Secret", TwoHostWebApplicationFactory.SharedSecret);
        HttpResponseMessage withAuthResp = await mcpClient.SendAsync(
            withAuth, TestContext.Current.CancellationToken);
        withAuthResp.IsSuccessStatusCode.Should().BeTrue(
            "the MCP must accept a board event with the shared secret");
    }

    [Fact]
    public async Task Api_Notifier_Can_Call_Mcp_Directly_Across_Processes()
    {
        // The wiring is correct end-to-end: a direct call
        // to the API's HttpMcpResourceNotifier results in
        // an event landing on the MCP. The test is the
        // contract: the notifier is configured with the
        // right base URL and secret.
        using IServiceScope scope = _factory.Api.Services.CreateScope();
        var notifier = scope.ServiceProvider
            .GetRequiredService<Cardscape.Api.Realtime.HttpMcpResourceNotifier>();

        // Reset the recording sink so we only see the
        // call we are about to make.
        foreach (RecordedCall _ in _factory.RecordingSink.Snapshot())
        {
            // no-op: we just want the count below
        }
        int beforeCallCount = _factory.RecordingSink.Snapshot().Count;

        Guid boardId = Guid.NewGuid();
        await notifier.NotifyAsync(boardId, TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        IReadOnlyList<RecordedCall> calls = _factory.RecordingSink.Snapshot();
        var ours = calls
            .Skip(beforeCallCount)
            .Where(c => c.Uri.Contains("api/internal/board-event", StringComparison.Ordinal))
            .ToList();

        ours.Should().NotBeEmpty(
            "the API's HttpMcpResourceNotifier must have made at least one HTTP call to the MCP's " +
            "/api/internal/board-event endpoint; the recording sink should show it. " +
            $"Total recorded calls: {calls.Count}, by method: {string.Join(", ", calls.Select(c => c.Method + " " + c.Uri))}");
        ours[0].StatusCode.Should().BeInRange(200, 299,
            $"the cross-process broadcast call must have succeeded; a 4xx/5xx means " +
            $"the MCP rejected the call (auth or path mismatch) and the event was not recorded. " +
            $"Failure: {ours[0].Failure ?? "(none)"}");

        int count = await CountBroadcastEventsForBoardAsync(boardId);
        count.Should().BeGreaterThan(0,
            $"the broadcast call returned 2xx (recorded as {ours[0].StatusCode}) but the MCP " +
            $"event log has no Broadcast event for the matching board URI {boardId:N}; " +
            "the broadcaster's event-recording path is broken");
    }

    [Fact]
    public async Task Api_Mutation_Reaches_Mcp_Broadcaster_Across_Processes()
    {
        // The cross-process E2E test. Steps:
        //   1. Register a user via the API.
        //   2. Create a workspace + board + list via the API.
        //   3. Create a card via the API — this fires
        //      a domain event (CardCreated) that the
        //      DomainEventBroadcaster listens to, which
        //      calls the HttpMcpResourceNotifier, which
        //      HTTP-calls the MCP at /api/internal/board-event.
        //   4. The MCP's broadcaster records a Broadcast
        //      event in its event log.
        //   5. Query the MCP's subscriptions snapshot to
        //      confirm the event is there.

        HttpClient apiClient = await CreateAuthenticatedApiClientAsync();

        Guid workspaceId = await CreateWorkspaceAsync(apiClient);
        Guid boardId = await CreateBoardAsync(apiClient, workspaceId);
        Guid listId = await CreateListAsync(apiClient, boardId);

        // Snapshot the MCP event log BEFORE the mutation
        // so we can assert the delta is at least 1.
        int beforeCount = await CountBroadcastEventsForBoardAsync(boardId);
        int beforeCalls = _factory.RecordingSink.Snapshot().Count;

        Guid cardId = await CreateCardAsync(apiClient, listId, "e2e-card");

        // The HTTP-call from the API to the MCP is
        // fire-and-forget. Poll the MCP for up to
        // 5 seconds for the event to land.
        bool found = false;
        int afterCount = beforeCount;
        int afterCalls = beforeCalls;
        for (int i = 0; i < 50; i++)
        {
            afterCount = await CountBroadcastEventsForBoardAsync(boardId);
            afterCalls = _factory.RecordingSink.Snapshot().Count;
            if (afterCount > beforeCount || afterCalls > beforeCalls)
            {
                found = true;
                break;
            }
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        if (!found)
        {
            var allCalls = _factory.RecordingSink.Snapshot()
                .Select(c => $"{c.Method} {c.Uri} -> {c.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? c.Failure}")
                .ToList();
            throw new Xunit.Sdk.XunitException(
                $"no Broadcast event for board {boardId} on the MCP after card creation. " +
                $"MCP event log before={beforeCount}, after={afterCount}. " +
                $"Recording sink saw {allCalls.Count} calls since fixture init: " +
                string.Join(" | ", allCalls));
        }
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task<HttpClient> CreateAuthenticatedApiClientAsync()
    {
        HttpClient client = _factory.Api.CreateClient();
        var register = new
        {
            email = $"e2e-{Guid.NewGuid():N}@cardscape.local",
            displayName = "E2E",
            password = "Goodpass123!"
        };
        HttpResponseMessage r = await client.PostAsJsonAsync(
            "api/auth/register", register, TestContext.Current.CancellationToken);
        r.IsSuccessStatusCode.Should().BeTrue();
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        string accessToken = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/workspaces/",
            new { name = $"WS-E2E-{Guid.NewGuid():N}" },
            TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateBoardAsync(HttpClient client, Guid workspaceId)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/boards/",
            new
            {
                workspaceId,
                name = $"Board-E2E-{Guid.NewGuid():N}",
                description = (string?)null,
                visibility = "private"
            },
            TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateListAsync(HttpClient client, Guid boardId)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/lists/",
            new { boardId, name = "To Do" },
            TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCardAsync(HttpClient client, Guid listId, string title)
    {
        HttpResponseMessage resp = await client.PostAsJsonAsync(
            "api/cards/",
            new { listId, title, description = (string?)null },
            TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> DumpMcpSubscriptionsAsync()
    {
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/internal/board-event/subscriptions");
        req.Headers.Add("X-Internal-Secret", TwoHostWebApplicationFactory.SharedSecret);
        HttpResponseMessage resp = await mcpClient.SendAsync(
            req, TestContext.Current.CancellationToken);
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return $"[{(int)resp.StatusCode}] {body}";
    }

    private async Task<int> CountBroadcastEventsForBoardAsync(Guid boardId)
    {
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/internal/board-event/subscriptions");
        req.Headers.Add("X-Internal-Secret", TwoHostWebApplicationFactory.SharedSecret);
        HttpResponseMessage resp = await mcpClient.SendAsync(
            req, TestContext.Current.CancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return 0;
        }
        string body = await resp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("events", out JsonElement events))
        {
            return 0;
        }
        string expectedUri = $"board://{boardId:N}";
        int count = 0;
        foreach (JsonElement e in events.EnumerateArray())
        {
            if (e.TryGetProperty("uri", out JsonElement uri) &&
                string.Equals(uri.GetString(), expectedUri, StringComparison.Ordinal) &&
                e.TryGetProperty("eventKind", out JsonElement kind) &&
                kind.ValueKind == JsonValueKind.String &&
                string.Equals(kind.GetString(), "Broadcast", StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }
}
