using System.Net.Http.Headers;
using System.Text;
using Cardscape.Application.Abstractions.Settings;
using Cardscape.Application.Security.Commands;
using Cardscape.Contracts.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static Cardscape.IntegrationTests.Endpoints.WorkspaceMemberAdministrationTests;

namespace Cardscape.IntegrationTests.Endpoints;

/// <summary>
/// Personal AI agents end to end (ADR 0015): the administrator's switch for
/// the built-in MCP server and its write ceiling, an agent key's permissions
/// on REST and MCP, and the attribution of everything an agent changes to
/// "the owner's AI" in board activity.
/// </summary>
[Collection(CardscapeApi.Name)]
public sealed class AiAgentTests(CardscapeWebApplicationFactory factory)
{
    private const string AgentName = "Claude test agent";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mcp_AnswersNotFound_WhileTheAdministratorHasItOff()
    {
        await SetAgentSettingsAsync(mcpEnabled: false, allowWrites: true);
        Account owner = await RegisterAsync(factory, "agent-off");
        string key = await IssueKeyAsync(owner.Client, "read");

        using HttpRequestMessage request = InitializeRequest(key);
        HttpResponseMessage response = await factory.CreateClient().SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Ct)).Should().Contain("mcp.disabled");
    }

    [Fact]
    public async Task Mcp_RejectsBrowserSessions_EvenWhenOn()
    {
        try
        {
            await SetAgentSettingsAsync(mcpEnabled: true, allowWrites: true);
            Account owner = await RegisterAsync(factory, "agent-jwt");
            using HttpRequestMessage request = InitializeRequest(bearer: null);
            request.Headers.Authorization = owner.Client.DefaultRequestHeaders.Authorization;

            HttpResponseMessage response = await factory.CreateClient().SendAsync(request, Ct);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
        finally
        {
            await SetAgentSettingsAsync(mcpEnabled: false, allowWrites: true);
        }
    }

    [Fact]
    public async Task AgentChanges_AreAttributedToTheOwnersAi_AndTheOwnersOwnAreNot()
    {
        Account owner = await RegisterAsync(factory, "agent-owner");
        (Guid boardId, Guid listId) = await CreateBoardWithListAsync(owner.Client);
        string key = await IssueKeyAsync(owner.Client, "read", "write");
        HttpClient agent = AgentClient(key);

        HttpResponseMessage byAgent = await agent.PostAsJsonAsync(
            "api/cards/", new { listId, title = "Drafted by the agent", description = (string?)null }, Ct);
        byAgent.StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage byOwner = await owner.Client.PostAsJsonAsync(
            "api/cards/", new { listId, title = "Typed by hand", description = (string?)null }, Ct);
        byOwner.StatusCode.Should().Be(HttpStatusCode.Created);

        IReadOnlyList<JsonElement> activity = await BoardActivityAsync(owner.Client, boardId);
        JsonElement agentEntry = activity.Single(a => PayloadMentions(a, "Drafted by the agent"));
        agentEntry.GetProperty("actorId").GetGuid().Should().Be(owner.Id, "the agent acts as its owner");
        agentEntry.GetProperty("agentName").GetString().Should().Be(AgentName);
        agentEntry.GetProperty("agentId").GetGuid().Should().NotBeEmpty();

        JsonElement ownerEntry = activity.Single(a => PayloadMentions(a, "Typed by hand"));
        ownerEntry.GetProperty("actorId").GetGuid().Should().Be(owner.Id);
        ownerEntry.TryGetProperty("agentName", out JsonElement none).Should().BeTrue();
        none.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AgentKey_WithOnlyFineGrainedCapabilities_IsReadOnlyOnRest()
    {
        Account owner = await RegisterAsync(factory, "agent-narrow");
        (Guid boardId, Guid listId) = await CreateBoardWithListAsync(owner.Client);
        HttpClient agent = AgentClient(await IssueKeyAsync(owner.Client, "read", "cards:write"));

        (await agent.GetAsync($"api/boards/{boardId}", Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage write = await agent.PostAsJsonAsync(
            "api/cards/", new { listId, title = "Not allowed here", description = (string?)null }, Ct);

        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await write.Content.ReadAsStringAsync(Ct)).Should().Contain("auth.agent_scope_forbidden");
    }

    [Fact]
    public async Task AgentKey_CannotMintKeysOrTouchAccountSecurity()
    {
        Account owner = await RegisterAsync(factory, "agent-escalate");
        HttpClient agent = AgentClient(await IssueKeyAsync(owner.Client, "read", "write"));

        HttpResponseMessage mint = await agent.PostAsJsonAsync(
            "api/security/api-tokens/", new { name = "broader", scopes = new[] { "read", "write" } }, Ct);
        HttpResponseMessage list = await agent.GetAsync("api/security/api-tokens/", Ct);

        mint.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        list.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdministratorCeiling_MakesEveryAgentReadOnly()
    {
        Account owner = await RegisterAsync(factory, "agent-ceiling");
        (_, Guid listId) = await CreateBoardWithListAsync(owner.Client);
        HttpClient agent = AgentClient(await IssueKeyAsync(owner.Client, "read", "write"));
        try
        {
            await SetAgentSettingsAsync(mcpEnabled: false, allowWrites: false);

            HttpResponseMessage write = await agent.PostAsJsonAsync(
                "api/cards/", new { listId, title = "Blocked by the admin", description = (string?)null }, Ct);

            write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            await SetAgentSettingsAsync(mcpEnabled: false, allowWrites: true);
        }
    }

    [Fact]
    public async Task McpTools_RunAsTheOwnersAi_WithinTheKeysCapabilities()
    {
        Account owner = await RegisterAsync(factory, "agent-mcp");
        (Guid boardId, Guid listId) = await CreateBoardWithListAsync(owner.Client);
        string key = await IssueKeyAsync(owner.Client, "read", "cards:write");
        try
        {
            await SetAgentSettingsAsync(mcpEnabled: true, allowWrites: true);
            await using McpClient client = await ConnectAsync(key);

            CallToolResult created = await client.CallToolAsync(
                "cards_create",
                new Dictionary<string, object?> { ["listId"] = listId, ["title"] = "Filed over MCP", ["description"] = null },
                cancellationToken: Ct);
            created.IsError.Should().NotBeTrue(Text(created));

            // The key lacks comments:write: the call is refused before the tool runs.
            string denial;
            try
            {
                CallToolResult refused = await client.CallToolAsync(
                    "comments_add",
                    new Dictionary<string, object?> { ["cardId"] = Guid.NewGuid(), ["body"] = "Hello" },
                    cancellationToken: Ct);
                refused.IsError.Should().BeTrue();
                denial = Text(refused);
            }
            catch (McpException ex)
            {
                denial = ex.Message;
            }

            denial.Should().Contain("comments:write");
        }
        finally
        {
            await SetAgentSettingsAsync(mcpEnabled: false, allowWrites: true);
        }

        JsonElement entry = (await BoardActivityAsync(owner.Client, boardId))
            .Single(a => PayloadMentions(a, "Filed over MCP"));
        entry.GetProperty("actorId").GetGuid().Should().Be(owner.Id);
        entry.GetProperty("agentName").GetString().Should().Be(AgentName);
    }

    // ── helpers ─────────────────────────────────────────────

    private async Task SetAgentSettingsAsync(bool mcpEnabled, bool allowWrites)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ISystemSettingsService settings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
        SystemSettings current = await settings.GetAsync(Ct);
        current.Agents.McpEnabled = mcpEnabled;
        current.Agents.AllowWrites = allowWrites;
        (await settings.UpdateAsync(current, "tests", Ct)).IsSuccess.Should().BeTrue();
    }

    private static async Task<string> IssueKeyAsync(HttpClient owner, params string[] scopes)
    {
        HttpResponseMessage issue = await owner.PostAsJsonAsync(
            "api/security/api-tokens/", new { name = AgentName, scopes }, Ct);
        issue.IsSuccessStatusCode.Should().BeTrue();
        return (await issue.Content.ReadFromJsonAsync<ApiTokenIssuanceDto>(Ct))!.CleartextSecret;
    }

    private HttpClient AgentClient(string key)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private async Task<McpClient> ConnectAsync(string key)
    {
        HttpClient http = factory.CreateClient();
        HttpClientTransport transport = new(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
            },
            http,
            NullLoggerFactory.Instance,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport, cancellationToken: Ct);
    }

    private static HttpRequestMessage InitializeRequest(string? bearer)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"tests","version":"1.0"}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return request;
    }

    private static async Task<(Guid BoardId, Guid ListId)> CreateBoardWithListAsync(HttpClient owner)
    {
        WorkspaceDto workspace = await CreateWorkspaceAsync(owner, "Agents");
        HttpResponseMessage board = await owner.PostAsJsonAsync(
            "api/boards/", new { workspaceId = workspace.Id, name = "Agent board", description = (string?)null, visibility = "Private" }, Ct);
        board.IsSuccessStatusCode.Should().BeTrue();
        Guid boardId = (await board.Content.ReadFromJsonAsync<BoardDto>(TestJson.Options, Ct))!.Id;
        HttpResponseMessage list = await owner.PostAsJsonAsync("api/lists/", new { boardId, name = "Todo" }, Ct);
        list.IsSuccessStatusCode.Should().BeTrue();
        return (boardId, (await list.Content.ReadFromJsonAsync<BoardListDto>(TestJson.Options, Ct))!.Id);
    }

    private static async Task<IReadOnlyList<JsonElement>> BoardActivityAsync(HttpClient client, Guid boardId)
    {
        using JsonDocument page = JsonDocument.Parse(await client.GetStringAsync($"api/boards/{boardId}/activities/", Ct));
        return [.. page.RootElement.GetProperty("items").EnumerateArray().Select(item => item.Clone())];
    }

    private static bool PayloadMentions(JsonElement activity, string text) =>
        activity.GetProperty("payloadJson").GetString()?.Contains(text, StringComparison.Ordinal) == true;

    private static string Text(CallToolResult result) =>
        string.Join(" | ", result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
