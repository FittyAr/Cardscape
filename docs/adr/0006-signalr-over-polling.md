# ADR 0006: SignalR for real-time board sync (over polling, SSE, and WebSockets-direct)

- **Status**: Accepted
- **Date**: 2026-07-29
- **Deciders**: Cardscape maintainers

## Context

A kanban board is a multi-user surface. When one user
moves a card, every collaborator on the same board
should see the move without refreshing. Cardscape
needed a real-time channel from day one (Phase 2).

The realistic options in mid-2026:

| Option | Pros | Cons |
|---|---|---|
| **5-second polling** (`setInterval` + `GET /api/boards/{id}`) | Trivial. Works behind any proxy. | 5-second-old UI is "live" by a generous reading. Server pays a full board read every interval per client. |
| **Server-Sent Events (SSE)** | One-way, simple, native browser support. Reconnect built-in. | One-way (the client cannot easily reply). Browser limits concurrent SSE connections (6 per origin in HTTP/1.1). |
| **WebSockets, hand-rolled** | Full-duplex, lowest protocol overhead. | Every reconnect, every group, every protocol detail is your problem. Easy to leak resources. |
| **SignalR** (Microsoft) | Auto-negotiates transport (WebSocket → SSE → long-poll). Built-in groups (board:{id}), presence, reconnect with backoff. Typed hub clients. | Tied to ASP.NET Core; the MCP process needs a cross-process bridge (we use `/api/internal/broadcast`). |

The maintainer's design intent:

> *"A real-time UI is the difference between 'a tool'
> and 'a tool you trust'. Polling is a non-starter; the
> latency budget is half a second, and the server cost
> of polling is wasted CPU."*

## Decision

The REST API hosts a SignalR **hub** at `/hubs/board`
(defined in `src/Cardscape.Api/Hubs/BoardHub.cs`). Every
command that changes board state (card move, comment
add, label attach, etc.) raises a domain event. The
`DomainEventBroadcaster` listens for those events and
dispatches the matching `IBoardClient` method on the
SignalR group `board:{boardId:N}`.

The Blazor WASM client connects to the hub on board
load, joins the `board:{id}` group, and reloads the
board (or surgically updates a single card) on every
push. A "Live" indicator in the top-right shows the
SignalR connection state; reconnect with backoff is
built in.

The MCP server is a **separate process** that talks to
the API over HTTP (`POST /api/internal/broadcast`).
The endpoint authenticates the MCP request with a
shared `Internal:Secret` header, resolves the board,
and dispatches the matching `IBoardClient` method on
the SignalR group. This keeps the SignalR hub bound
to a single ASP.NET Core process (no need to
distributed-SignalR), while the MCP server can be
deployed independently.

## Consequences

Positive:

- **Half-second latency budget met.** A move made by
  any client (Web, MCP, future native) is broadcast
  to every other client on the same board within
  ~500ms in the same data centre, ~1-2s across
  continents.
- **Auto-negotiated transport.** SignalR picks
  WebSocket, falls back to SSE, falls back to long-
  poll. The Blazor client always connects.
- **Groups are first-class.** `BoardHub.Groups
  .AddToGroupAsync(connectionId, $"board:{boardId}")`
  is the routing primitive. A card move on board 42
  only fans out to clients connected to board 42.
- **Typed hub contract.** `IBoardClient` is a C#
  interface; the Web client and the MCP push client
  share the same `BoardEventPayloads` records.
- **Microsoft-supported.** SignalR is a first-class
  ASP.NET Core citizen. The transport, the JS
  client, the Blazor client, and the
  `Microsoft.AspNetCore.SignalR.Client` package are
  all on the same release train as the runtime.

Negative / accepted:

- **Tied to ASP.NET Core.** A future non-.NET client
  (e.g. a Go-based aggregator) cannot use SignalR
  directly; it would consume the same WebSocket
  protocol or the `/api/internal/broadcast` HTTP
  endpoint. We accept this — the alternative is a
  hand-rolled protocol that nobody else will speak.
- **Cross-process push requires a shared secret.** The
  `/api/internal/broadcast` endpoint authenticates
  the MCP request with an `Internal:Secret` header.
  This is a 32-byte random value, generated at
  install time, and stored in the deployment's
  environment. The endpoint refuses to serve when
  the secret is empty (returns 503). This is a
  sandboxed-dev safety, not a security boundary
  against an attacker inside the data centre.
- **Reconnect storms.** A partition that reconnects
  N clients at once can stampede the server. SignalR
  has built-in backoff, and the client is expected
  to honour it. We have not yet hit this in
  production; the maintainer will add a rate-
  limited reconnect path if and when it shows up.

## When to revisit

This ADR should be revisited when **any** of the
following is true:

1. The deployment surface requires real-time across
   multiple ASP.NET Core instances behind a load
   balancer (today, Cardscape is single-process; the
   scale-out story uses the same `/api/internal/broadcast`
   hub-pattern with a Redis backplane).
2. A non-.NET client becomes important (e.g. a Go
   service that needs to consume the same events).
3. SignalR ships a major-version break (unlikely in
   the .NET 10 timeframe; SignalR has been API-stable
   since .NET 5).

## References

- `src/Cardscape.Api/Hubs/BoardHub.cs` — the hub
- `src/Cardscape.Api/Hubs/IBoardClient.cs` — the
  typed client contract
- `src/Cardscape.Api/Realtime/DomainEventBroadcaster.cs` —
  the broadcaster
- `src/Cardscape.Api/Endpoints/Internal/BoardBroadcastEndpoints.cs` —
  the cross-process push
- `src/Cardscape.Mcp/Realtime/HttpBoardPushClient.cs` —
  the MCP-side push client
- `docs/architecture/03-mcp-server.md` — the MCP
  cross-process push design
- `docs/roadmap/01-implementation-plan.md` §3.5 —
  the Phase 2 real-time section
