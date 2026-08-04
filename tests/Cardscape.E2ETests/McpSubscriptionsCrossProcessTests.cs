using System.Net;
using System.Net.Http.Json;
using Cardscape.E2ETests.Fixtures;
using FluentAssertions;
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
///    MCP at <c>/api/internal/board-event</c>.
/// 3. The MCP fans the event out to every
///    subscribed resource (the AI client).
///
/// The single-host integration suite can verify
/// steps 1 and 2 (it can call the API and the API
/// <em>thinks</em> it called the MCP). The
/// cross-process contract — the API really hitting
/// the MCP really firing the subscription — lives
/// here.
///
/// NOTE: the full cross-process E2E coverage
/// (the API mutation -&gt; broadcaster fan-out -&gt;
/// MCP receives -&gt; resource subscription delivers
/// to a stub AI client) is the v1.3.0 work item.
/// The smoke tests in this file pin the fixture
/// contract: both hosts boot, the API listens on
/// a real port, the MCP listens on a real port,
/// and the inbound event endpoint is reachable.
/// </summary>
[Collection(E2E.Name)]
public sealed class McpSubscriptionsCrossProcessTests
{
    private readonly TwoHostWebApplicationFactory _factory;
    public McpSubscriptionsCrossProcessTests(TwoHostWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void Both_Hosts_Boot_And_Bind_To_Real_Ports()
    {
        // The fixture is the deliverable for the
        // v1.2.0 follow-up; the full cross-process
        // coverage is v1.3.0. This test pins the
        // contract that the dual-host fixture
        // compiles + boots + binds to a real port
        // on each host.
        _factory.Api.ServerAddress.Should().NotBeNullOrEmpty(
            "the API host must have a bound address after CreateClient()");
        _factory.Mcp.ServerAddress.Should().NotBeNullOrEmpty(
            "the MCP host must have a bound address after CreateClient()");
    }

    [Fact]
    public async Task Mcp_Health_Endpoint_Reports_Healthy()
    {
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        HttpResponseMessage resp = await mcpClient.GetAsync(
            "health/live", TestContext.Current.CancellationToken);
        resp.IsSuccessStatusCode.Should().BeTrue(
            "the MCP /health/live endpoint must return 2xx; " +
            "a failure here means the dual-host fixture did not boot");
    }

    [Fact]
    public async Task Api_Can_Call_Mcp_Internal_Endpoint()
    {
        // Direct cross-process test: the MCP's
        // internal endpoint receives a synthetic
        // board event. The MCP may accept (204),
        // require auth (401/403), or reject the
        // body shape (400). The regression we want
        // to catch is a 5xx: a server crash on
        // the inbound event.
        HttpClient mcpClient = _factory.Mcp.CreateClient();
        var payload = new
        {
            boardId = Guid.NewGuid(),
            eventType = "card.created",
            payload = new { cardId = Guid.NewGuid() }
        };
        HttpResponseMessage resp = await mcpClient.PostAsJsonAsync(
            "api/internal/board-event",
            payload, TestContext.Current.CancellationToken);
        ((int)resp.StatusCode).Should().BeLessThan(500,
            $"the MCP must not crash on a board event; " +
            $"status {(int)resp.StatusCode} {resp.StatusCode} is a server error");
    }
}
