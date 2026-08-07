using Cardscape.Infrastructure.Logging;
using Cardscape.Mcp.Endpoints.Internal;
using Cardscape.Mcp.Extensions;
using Cardscape.Mcp.Observability;
using Cardscape.Mcp.Tools;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ────────────────────────────────────────────────
// Serilog wires the same shared infrastructure the API uses:
// structured console, rolling daily file under
// logs/mcp/{yyyyMMdd}/mcp-app.log, a Warning+ error stream,
// and (when enabled) the OTel / DB sinks. Runs before the
// OTel / MCP service registration so every component that
// resolves ILogger<T> gets the same logger.
builder.UseCardscapeSerilog(ServiceType.Mcp);

// ── Composition root ──────────────────────────────────────────
//
// The MCP server composes the same Application + Infrastructure
// DI registrations as the REST API. The two services are
// independent deployables that share the application layer.

builder.Services.AddCardscapeMcp(builder.Configuration);
builder.Services.AddMcpTracing(builder.Configuration);

// The MCP's internal endpoints return snapshots with
// enum-typed fields (e.g. SubscriptionEventKind). The
// default System.Text.Json serialises enums as their
// numeric value, which forces every consumer to know
// the underlying integer. The API's McpSubscriptionsClient
// (and the Web UI) expects the human-readable form, so
// we register JsonStringEnumConverter here. The
// converter applies to all minimal-API responses in the
// MCP process.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

// ── Ambient bus for MCP tools ──────────────────────────────
// The MCP tools (decorated with [McpServerTool]) need a
// Wolverine IMessageBus to dispatch commands. The bus is
// a singleton on the root provider (Wolverine registers it
// there as part of the AddCardscapeMcp composition), so
// resolving it from the root is the right lifetime - no
// short-lived scope, no risk of the captured reference
// racing against a half-disposed host (BETA-8-MCP-#8).
McpToolContext.Bus = app.Services.GetRequiredService<IMessageBus>();

// ── Pipeline ─────────────────────────────────────────────
app.UseCardscapeMcp();
app.MapCardscapeHealthChecks();
// The API HTTP-calls /api/internal/board-event after every
// board-changing domain event so the MCP can fan the change
// out to subscribed AI clients (see
// McpResourceBroadcaster.BroadcastAsync).
app.MapMcpBoardEventEndpoints();

app.Run();

namespace Cardscape.Mcp
{
    // Public marker for WebApplicationFactory<Cardscape.Mcp.Program>
    // in the E2E test project (the implicit Program class that the
    // .NET minimal-API SDK generates is internal; the test project
    // needs the type to be reachable).
    public partial class Program;
}
