# Connecting a desktop AI client to Cardscape MCP

Cardscape exposes MCP over authenticated **stateful Streamable HTTP** at
`https://<mcp-host>/mcp`. It does not expose stdio or legacy SSE.

## 1. Mint a token

Open **Settings → API tokens**, create a token with the minimum required
`read` and/or `write` scopes, and copy the cleartext secret shown once.

## 2. Configure the client

Use the client's remote/Streamable HTTP MCP configuration:

- URL: `https://<mcp-host>/mcp`
- Header: `Authorization: Bearer <secret>`

The exact settings shape depends on the desktop client and version. Do not put
the token in command arguments, a repository file, or a process-wide Cardscape
environment variable. Prefer the client's credential store when available.

For local development, start the API and MCP hosts independently:

```bash
dotnet run --project src/Cardscape.Api
dotnet run --project src/Cardscape.Mcp
```

Then point the client at the MCP host's `/mcp` URL. The MCP process uses its
own database configuration and `Cardscape:ApiBaseUrl` only for cross-process
realtime notifications.

## 3. Smoke test

Restart clients that only reload MCP settings at launch, then ask:

> List my Cardscape workspaces and open the first board.

| Symptom | Likely cause |
| --- | --- |
| HTTP `401` | Missing, malformed, revoked, or unknown API token. |
| `auth.scope_required` | The token lacks the exact `read` or `write` scope required by the operation. |
| Connection failure | Wrong MCP host/port, missing `/mcp`, TLS trust, proxy, or firewall issue. |
| Notifications do not arrive | The client disconnected its stateful session or did not subscribe to the board resource. |

Revoke a compromised token from **Settings → API tokens**. The next request is
rejected without restarting the MCP host.
