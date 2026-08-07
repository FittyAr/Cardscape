# Connecting a desktop AI client to Cardscape's MCP server

The Cardscape MCP server speaks the [Model Context Protocol][mcp]
over **stdio** (not HTTP). A desktop AI client such as Claude
Desktop, Cursor, or Windsurf therefore launches the server as a
child process and exchanges JSON-RPC messages on its stdin /
stdout. The client only needs to know:

1. how to **start** the process;
2. how to **authenticate** it (the same API tokens the Web UI
   mints from *Settings → API tokens*);
3. how to reach the **Cardscape API** (the MCP server delegates
   every tool to a Wolverine command, so it needs to know the
   API's base URL too).

[mcp]: https://modelcontextprotocol.io

## 1. Get an API token

In the running Cardscape app:

1. Open **Settings → API tokens**.
2. Click **New token**, give it a name (e.g. `claude-desktop`),
   pick the scopes you want the AI to have, and create.
3. Copy the cleartext secret. Cardscape will only show it once.

> Keep the secret the way you'd keep any long-lived credential.
> You can revoke it from the same screen at any time, and the
> next call from the AI client will return 401 — no need to
> rebuild anything.

## 2. Configure your AI client

The exact key in the client config file depends on the client.
For Claude Desktop the file is `claude_desktop_config.json`,
typically at:

- macOS — `~/Library/Application Support/Claude/claude_desktop_config.json`
- Windows — `%APPDATA%\Claude\claude_desktop_config.json`
- Linux — `~/.config/Claude/claude_desktop_config.json`

For Cursor the same shape lives under
**Settings → Model Context Protocol → Edit config**.

The minimum entry is:

```jsonc
{
  "mcpServers": {
    "cardscape": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/Cardscape/src/Cardscape.Mcp"
      ],
      "env": {
        "Cardscape__ApiBaseUrl": "https://cardscape.example.com",
        "Cardscape__ApiToken":   "sk_live_…paste the cleartext secret…"
      }
    }
  }
}
```

Replace:

- `/absolute/path/to/Cardscape/src/Cardscape.Mcp` with the
  real path on your machine.
- `https://cardscape.example.com` with the URL of your
  Cardscape instance (use `http://localhost:8080` for a local
  dev container).
- `sk_live_…` with the cleartext secret from step 1.

> The `Cardscape__ApiToken` env var is the supported way to
> authenticate the stdio transport — the alternative
> `Authorization: Bearer <secret>` HTTP header has no place to
> ride on a stdio message. The MCP server's auth handler
> resolves the env var in the same code path as the header.
> You can also use the shorthand `CARDS_API_TOKEN` if you
> prefer.

## 3. Build once (or skip — the `dotnet run` form does it for you)

The example above uses `dotnet run`, which compiles the MCP
project on first invocation. If you ship a release build,
publish it once and replace the `args` with the published
binary's path:

```bash
dotnet publish src/Cardscape.Mcp/Cardscape.Mcp.csproj \
    -c Release -o /opt/cardscape/mcp
```

Then the config becomes:

```jsonc
{
  "mcpServers": {
    "cardscape": {
      "command": "/opt/cardscape/mcp/Cardscape.Mcp",
      "env": {
        "Cardscape__ApiBaseUrl": "https://cardscape.example.com",
        "Cardscape__ApiToken":   "sk_live_…"
      }
    }
  }
}
```

## 4. Restart the AI client

Claude Desktop / Cursor only reads the config at launch. Quit
and reopen, then open the **MCP** panel — you should see
*cardscape* in the list with `n` tools enabled (the count
grows as new tools land). Click any tool to see its description
and parameter schema.

## 5. Smoke test from the chat

Ask the assistant a question that has to use a tool:

> List my Cardscape workspaces and pick the first board in the
> first one.

If the response comes back with a real workspace name, the
client is wired correctly. If you get a "tool call failed" with
`auth.required` in the stderr, the env var did not reach the
process — double-check the env key spelling and restart the
client.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| `auth.required` in the tool error | `Cardscape__ApiToken` env var did not reach the process. Claude Desktop only re-reads the config on launch — quit and reopen. |
| `api_not_reachable` | `Cardscape__ApiBaseUrl` is wrong, the API is not running, or the host's firewall blocks the loopback. The MCP server hits the API for every command. |
| `token_revoked` | The token was deleted in the Web UI. Mint a new one and update the config. |
| The AI client doesn't list `cardscape` at all | The config file is in the wrong path, the JSON is malformed (trailing comma is the classic), or `dotnet` is not on `PATH` for the launcher. |

## What the AI client can do once it's connected

The MCP surface currently ships 90+ tools covering workspaces,
boards, lists, cards, comments, checklists, voting, custom
fields, automation, webhooks, activity, search, AI helpers,
recurring cards, and board extensions. See
[`docs/architecture/03-mcp-server.md`](../architecture/03-mcp-server.md)
for the canonical list. The token's scopes gate which tools the
client may call; mint a token with the **read-only** scope set
if you only want the AI to read state.
