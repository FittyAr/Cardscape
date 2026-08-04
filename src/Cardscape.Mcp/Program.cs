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

var app = builder.Build();

// ── Ambient bus for MCP tools ──────────────────────────────
// The MCP tools (decorated with [McpServerTool]) need a
// Wolverine IMessageBus to dispatch commands. Set the ambient
// reference here so the tool methods can read it through
// McpToolContext.Bus at dispatch time.
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
