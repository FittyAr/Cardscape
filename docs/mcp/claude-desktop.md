# Connecting a desktop AI client to Cardscape MCP

Cardscape exposes MCP over authenticated **stateful Streamable HTTP** at
`https://<your-cardscape>/mcp`, served by the same process as the web app
(ADR 0015). It does not expose stdio or legacy SSE.

## 0. An administrator turns it on

The MCP server is **off by default**. An administrator enables it in
**System settings > AI > AI agents (MCP server)**. The same card has
**Agents may change data**: when off, every agent is read-only. While the
server is off, `/mcp` answers `404` with `code: mcp.disabled`.

## 1. Create your agent key

Open **AI agents** from your profile menu (`/account/ai-agents`), click
**New agent**, name it after the client (for example "Claude Desktop on my
laptop") and choose what it may do:

| Permission | Scope | Grants |
| --- | --- | --- |
| Read | `read` | Every read-only tool, resource and prompt. |
| Work on cards | `cards:write` | Cards, checklists, votes, snoozes, recurrence, card labels and field values, inbox. |
| Comment | `comments:write` | Add, edit and delete comments. |
| Manage boards | `boards:write` | Boards, lists, labels, custom fields, automation, dashcards, extensions, imports. |
| Manage workspaces | `workspace:write` | Invitations, OAuth apps, integrations. |

The agent always acts **as you**: these permissions only narrow your own
workspace and board roles, never widen them. Copy the key shown once.

Everything the agent changes is recorded as **"Your name · AI"** (with the
agent's name) in board activity, the audit log and the server logs, so
everyone can tell your changes from your agent's.

## 2. Configure the client

Use the client's remote/Streamable HTTP MCP configuration (the AI agents page
shows a ready-made example with your instance's URL):

```json
{
  "mcpServers": {
    "cardscape": {
      "type": "http",
      "url": "https://<your-cardscape>/mcp",
      "headers": { "Authorization": "Bearer <your-agent-key>" }
    }
  }
}
```

The exact settings shape depends on the desktop client and version. Do not put
the key in command arguments, a repository file, or a process-wide environment
variable. Prefer the client's credential store when available.

For local development, the API serves `/mcp` itself:

```bash
dotnet run --project src/Cardscape.Api
```

The standalone `Cardscape.Mcp.Host` process is optional (to scale MCP apart
from the web app); it reads the administrator's switch from its own data
directory, so point `Cardscape:DataRoot` at the same directory as the API.

## 3. Smoke test

Restart clients that only reload MCP settings at launch, then ask:

> List my Cardscape workspaces and open the first board.

| Symptom | Likely cause |
| --- | --- |
| HTTP `404` with `mcp.disabled` | An administrator has not turned the MCP server on. |
| HTTP `401` | Missing, malformed, revoked, or unknown agent key (a browser session or JWT is not accepted). |
| `mcp.scope.forbidden` | The key lacks the permission the tool needs, or the administrator made agents read-only. |
| Connection failure | Wrong host, missing `/mcp`, TLS trust, proxy, or firewall issue. |
| Notifications do not arrive | The client disconnected its stateful session or did not subscribe to the board resource. |

Disconnect a compromised agent from **AI agents**. The next request is
rejected without restarting anything.
