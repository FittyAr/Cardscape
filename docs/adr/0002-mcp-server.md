# ADR 0002: Model Context Protocol (MCP) server

- **Status**: Accepted
- **Date**: 2026-07-27
- **Deciders**: Cardscape maintainers
- **Supersedes**: —
- **Superseded by**: —

## Context

Cardscape is a public, open-source -like kanban. The
maintainer wants to **connect Cardscape to AI assistants** (Claude
Desktop in the first place, but also any MCP-compatible client:
Cursor, Windsurf, Continue, JetBrains AI, custom agents, etc.) so
that users can drive their boards conversationally:

- "Show me all the cards in the Inbox list that are due this week."
- "Create a new card on the Q3 Roadmap board, in the Doing list,
  titled 'Investigate the flaky integration test', and assign it
  to me."
- "Move every card with the label 'urgent' to the Done list."

The de-facto standard for "AI agent ↔ external tool" in 2025-2026
is the **Model Context Protocol** (MCP), an open standard published
by Anthropic and adopted across the industry. There is a
first-class .NET SDK (`ModelContextProtocol` on NuGet) that runs
on `net8.0+` and therefore works on `net11.0`.

The decision is **where the MCP server lives** in the solution and
**how it integrates** with the rest of the architecture.

### Options considered

1. **MCP as a sub-app inside `Cardscape.Api`**.
   Pro: no new project, easy to share auth. Con: makes `Cardscape.Api`
   bigger; the MCP surface is a different deployment shape (stdio for
   local clients, SSE/HTTP for hosted clients); sharing the
   HTTP pipeline means we couple an AI tool surface with the public
   REST API.

2. **MCP as a separate process, talks to the API over HTTP**.
   Pro: clean separation. Con: we now have two HTTP surfaces
   (`/api/...` and the API client used by MCP), and the latency of
   HTTP-in-HTTP means every MCP tool call is at least one extra
   round-trip.

3. **MCP as a separate process, talks to `Application` directly** ✅.
   Pro: no new HTTP surface, no extra round-trip, reuses all the
   existing handlers and validators; deployment is independent of
   the REST API (you can run the MCP server without exposing the
   public API at all). Con: one more project to maintain; the MCP
   server is a trusted process (it can do anything the bearer
   token's owner can do).

We choose **option 3**. The MCP server is a thin transport layer
on top of the existing `Application` layer. Every MCP tool call
maps to an existing command or query. The same `MediatR` pipeline
runs; the same `FluentValidation` rules apply; the same `Result<T>`
is returned.

## Decision

1. **New project `Cardscape.Mcp`** under `src/`. It is a standalone
   ASP.NET Core minimal API. SDK: `Microsoft.NET.Sdk.Web`.

2. **Dependency direction**:
   ```
   Cardscape.Mcp → Cardscape.Application
   Cardscape.Mcp → Cardscape.Domain (for shared error types)
   ```
   `Cardscape.Mcp` does **not** reference `Cardscape.Api` or
   `Cardscape.Infrastructure`. It composes the application layer
   with the same DI registrations as the API, plus an
   `ICurrentUser` resolver that reads the user from the MCP
   request.

3. **Authentication**: the MCP server authenticates with **API
   tokens** (a new entity in the `Members` context, see
   [`../architecture/03-mcp-server.md`](../architecture/03-mcp-server.md)
   for details). Each token has a name, a hashed secret, an owner,
   a list of allowed scopes (`boards:read`, `boards:write`,
   `cards:read`, `cards:write`, `comments:write`, etc.), an
   optional expiry, and a last-used timestamp. The token is
   supplied by the client as `Authorization: Bearer <token>` for
   the SSE / HTTP transport, or via a configuration file for the
   stdio transport.

4. **Transport**:
   - **stdio** (default for local clients like Claude Desktop):
     the MCP server is started as a child process by the client;
     communication is over stdin/stdout.
   - **HTTP + SSE** (for hosted deployments): the server exposes
     `POST /mcp/messages` and `GET /mcp/sse` for clients that
     prefer HTTP. Standard MCP protocol.

5. **Initial tools** (exposed to the AI client):
   - `list_workspaces` — return the caller's workspaces.
   - `list_boards(workspaceId, query?)` — boards in a workspace.
   - `get_board(boardId)` — board details with lists.
   - `list_cards(boardId, listId?, labelId?, assigneeId?, dueBefore?, dueAfter?, query?)` — filtered cards.
   - `get_card(cardId)` — full card details.
   - `create_card(listId, title, description?, dueDate?, labelIds?, assigneeIds?)` — create a new card.
   - `move_card(cardId, listId, position?)` — move a card.
   - `update_card(cardId, title?, description?, dueDate?, isArchived?)` — partial update.
   - `add_comment(cardId, text)` — add a comment.
   - `assign_card(cardId, userId, unassign?)` — assign / unassign.
   - `search(query, limit?)` — full-text search.

6. **Initial resources** (addressable data the AI can subscribe to):
   - `board://{boardId}` — JSON of the board.
   - `card://{cardId}` — JSON of the card.
   - `workspace://{workspaceId}` — JSON of the workspace.

7. **Initial prompts** (templated instructions the user can run):
   - `standup-summary` — "Generate a standup summary for the cards
     assigned to me in the last 24 hours."
   - `triage-inbox` — "Help me triage the cards in the Inbox list."
   - `sprint-planning` — "Plan the next sprint based on the
     Backlog list of the active board."

8. **Idempotency**: every write tool accepts an optional
   `IdempotencyKey` parameter. When provided, the same key
   produces the same effect (no duplicate side-effects) for a
   configurable retention window (default 24 hours). Implemented
   via a new `Idempotency` entity in the `Application` layer.

9. **Observability**: the MCP server emits structured logs and
   OpenTelemetry traces for every tool call. The trace context is
   propagated to the `Application` layer, so an end-to-end trace
   covers the MCP call → handler → repository → DB.

10. **Versioning**: the MCP server is versioned separately from
    the REST API (MCP uses its own protocol version, currently
    `2025-06-18`). Breaking changes bump the MCP protocol
    version and ship in a new `Cardscape.Mcp` major version.
    The REST API and the MCP server are versioned independently.

## Consequences

Positive:

- **One more reason to use Cardscape over other  clones.**
  The MCP server makes the project the **only** self-hostable
   alternative with first-class AI integration. It's a
  clear differentiator for the open-source release.
- **No HTTP tax.** Tool calls are direct in-process calls into
  the application layer. No JSON serialization on the wire, no
  HTTP latency, no extra round-trip.
- **Reuse of the entire Application layer.** New use cases
  added in the future automatically become available as MCP
  tools with no MCP-specific code (just a one-line registration
  in `Cardscape.Mcp/Tools/...Tool.cs`).
- **Independent deployment.** A user can run the MCP server
  without exposing the public REST API. Useful for
  air-gapped deployments and for personal AI workflows.
- **Reuse of authentication, authorization, validation,
  logging, error handling.** Everything that `Cardscape.Api`
  already does, the MCP server does too.

Negative / accepted:

- **A new project to maintain.** The MCP server is ~500-1000
  lines of code plus the tools. We accept it because the
  value is high.
- **A new authentication model.** API tokens are a new entity
  (`ApiToken` in the `Members` context). We accept it because
  the existing cookie-based JWT auth doesn't make sense for a
  long-lived CLI process.
- **Tool surface is a contract.** Once an MCP tool is documented
  and clients start calling it, renaming or changing its
  signature is a breaking change. We treat the MCP tool
  surface with the same versioning rigor as the REST API.
- **stdio transport requires a child process per client.** The
  HTTP transport is a better fit for hosted deployments; the
  stdio transport is the right fit for local clients
  (Claude Desktop, etc.).

## When to revisit

This ADR should be revisited when **any** of the following is true:

1. The MCP protocol itself changes in a way that breaks our
   deployment shape (e.g. drops stdio in favor of HTTP-only).
2. The .NET SDK for MCP becomes unsupported or unmaintained.
3. We add a fourth or fifth transport (e.g. gRPC) and the
   transport abstraction needs to be re-shaped.
4. A new AI-client ecosystem emerges that has its own
   protocol (e.g. an "Agent Protocol" spec) and we want to
   support both.

## References

- [Model Context Protocol — official spec](https://modelcontextprotocol.io/)
- [ModelContextProtocol — .NET SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [`../architecture/03-mcp-server.md`](../architecture/03-mcp-server.md) —
  the operational guide
- [`../roadmap/01-implementation-plan.md`](../roadmap/01-implementation-plan.md)
  — the MCP work sits in Phase 2
