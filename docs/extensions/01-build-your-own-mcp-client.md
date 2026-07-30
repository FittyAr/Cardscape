# Build your own MCP client

> A 30-line C# walkthrough of a client that talks to the
> Cardscape Model Context Protocol server. The MCP protocol
> is the open standard for "AI agent ↔ external tool" in
> 2025-2026; any client that speaks it can drive a Cardscape
> board on the user's behalf.

## 1. Pick a transport

The Cardscape MCP server supports two transports:

- **stdio** — the client spawns the server as a subprocess
  and exchanges JSON-RPC messages over stdin/stdout. Used
  by Claude Desktop, Cursor, Continue, and most local AI
  clients.
- **HTTP + Server-Sent Events** — the server runs as a
  long-lived HTTP endpoint. Used by hosted AI clients that
  can't spawn subprocesses. Available in
  `Cardscape.Mcp` when the deployment configures
  `Cardscape__Mcp__Transport=http`.

For the stdio path, the client launches the server with the
right `Cardscape__ApiBaseUrl` and the user's `Cardscape__ApiToken`:

```json
{
  "mcpServers": {
    "cardscape": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/Cardscape.Mcp",
        "--",
        "--mcp-transport=stdio"
      ],
      "env": {
        "Cardscape__ApiBaseUrl": "https://cardscape.example.com",
        "Cardscape__ApiToken": "<the user's API token>"
      }
    }
  }
}
```

## 2. Speak JSON-RPC over the chosen transport

The Model Context Protocol SDK (`ModelContextProtocol` on
NuGet) handles the wire format. The minimum client looks
like this:

```csharp
using ModelContextProtocol.Client;

await using var client = await McpClient.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "Cardscape",
        Command = "dotnet",
        Arguments = new[] { "run", "--project", "src/Cardscape.Mcp" },
        EnvironmentVariables = new Dictionary<string, string?>
        {
            ["Cardscape__ApiBaseUrl"] = "https://cardscape.example.com",
            ["Cardscape__ApiToken"]   = Environment.GetEnvironmentVariable("CARDS_CAPE_TOKEN")
        }
    }));

// 1. List the available tools
foreach (var tool in await client.ListToolsAsync())
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// 2. Call a tool
var result = await client.CallToolAsync(
    "workspaces_list",
    new Dictionary<string, object?>());

// 3. Inspect the structured content
foreach (var block in result.Content)
{
    Console.WriteLine(block.Text);
}
```

That's it. The MCP SDK handles the JSON-RPC framing, the
session lifecycle, the tool discovery, and the call/result
shapes. Cardscape returns a JSON-serialised DTO per tool;
the SDK exposes it as `result.Content` (a list of
`TextContentBlock` or `EmbeddedResourceBlock`).

## 3. Surface the right tools

The Cardscape MCP server ships **47 tools** (as of
`v1.1.0-roadmap-execution`). The most useful starting
set:

| Tool | What it does |
|---|---|
| `workspaces_list` | List the workspaces the authenticated user can see. |
| `boards_list` | List the boards in a workspace. |
| `boards_get` | Get a single board (lists, members, labels). |
| `cards_list` | List the cards on a board. |
| `cards_get` | Get a single card (comments, checklists, votes). |
| `cards_create` | Create a card on a list. |
| `cards_move` | Move a card to a different list or position. |
| `cards_complete` / `cards_reopen` | Toggle a card's completion state. |
| `cards_assign` | Assign a member to a card. |
| `cards_attach_label` | Attach a label to a card. |
| `comments_add` | Add a comment to a card. |
| `search` | Full-text search across cards, comments, checklist items, labels, activity. |
| `ai_generate_card_description` | Have the configured AI provider draft a card description. |
| `ai_summarize_thread` | Have the configured AI provider summarise a comment thread. |
| `ai_suggest_owners` | Have the configured AI provider suggest assignees for a card. |

The server also exposes **5 resources** and **5 prompts**;
the SDK exposes them through `client.ListResourcesAsync()`
and `client.ListPromptsAsync()`.

## 4. Subscribe to live updates (optional)

Cardscape's MCP server supports resource subscriptions. When
the board's `board://{boardId}` resource changes, the server
fires a `ResourceUpdated` notification to every subscribed
client. The notification is the standard MCP envelope
`notifications/resources/updated` with a `{ "uri": "..." }`
payload.

To subscribe:

```csharp
// Returns when the server has registered the
// subscription; from here on, the server pushes every
// change to the matching board to this client.
await client.SubscribeToResourceAsync("board://<board-id>");

// ... when the server pushes, the SDK raises an event:
client.ResourceUpdated += (sender, e) =>
{
    // e.Uri is the resource URI ("board://<board-id>").
    // Re-read the resource to get the new state:
    var fresh = await client.ReadResourceAsync(e.Uri);
    Console.WriteLine($"Board {e.Uri} changed: {fresh.Contents?.FirstOrDefault()?.Text}");
};
```

How it works end-to-end:

- The Web client uses SignalR, but the MCP server is a
  separate process — it does not own the SignalR hub. The
  API's `DomainEventBroadcaster` (the static Wolverine
  handlers under `Cardscape.Api.Realtime`) fans every
  board-changing domain event out to the matching
  `board:{boardId}` SignalR group **and** to the MCP through
  the new `IMcpResourceNotifier` (a
  `HttpMcpResourceNotifier` that POSTs to the MCP's
  `/api/internal/board-event` with the same `X-Internal-Secret`
  shared secret the MCP uses to call the API in the reverse
  direction).
- On the MCP side, the request handler is routed to
  `McpResourceBroadcaster.Subscribe(uri, McpServer)` (a
  dictionary keyed by resource URI), and the broadcaster's
  `BroadcastAsync(boardId)` walks the per-URI subscriber
  list and emits the standard `notifications/resources/updated`
  notification on each subscribed session's transport.

For idempotency, both `Subscribe` and `Unsubscribe` are safe
to call multiple times for the same `(uri, session)` pair.
The broadcaster drops a subscriber whose transport throws on
send (e.g. a closed session) so a bad client cannot take
down the fan-out.

## 5. Idempotency

Every Cardscape MCP write tool accepts an optional
`idempotencyKey` parameter. Pass the same UUID for retries
of the same logical operation; the server short-circuits
the handler and returns the stored response from the first
call. This is the right pattern for AI agents that may
retry on transient network failures.

## 6. Reference

- [`docs/architecture/03-mcp-server.md`](../architecture/03-mcp-server.md) — the
  server-side design and the `ApiToken` auth scheme.
- [`docs/ai/01-mcp-deep-dive.md`](../ai/01-mcp-deep-dive.md) — operational
  guide (adding a tool, adding a resource, adding a prompt).
- [`docs/ai/02-prompt-library.md`](../ai/02-prompt-library.md) — the
  prompt templates the server ships out of the box.
- [Model Context Protocol spec](https://modelcontextprotocol.io/) — the
  open standard.
- [`ModelContextProtocol` NuGet](https://www.nuget.org/packages/ModelContextProtocol) —
  the .NET SDK used in the example.
