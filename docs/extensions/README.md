# Extensions & integrations

> How to extend Cardscape. Whether you want to call the
> REST API from your own client, drive a Cardscape board
> from an AI assistant via the MCP server, or build a
> third-party OAuth app, this is the place to start.

## Contents

- [`01-build-your-own-mcp-client.md`](01-build-your-own-mcp-client.md) —
  A 30-line C# client that connects to the Cardscape MCP
  server over stdio, lists the tools, and calls
  `workspaces_list`.
- (more to come) — Personal access tokens, OAuth 2.0
  third-party flow, webhook signature verification, iCalendar
  feed consumption, Slack/Google Drive/GitHub/Email
  integrations.

## Mental model

Cardscape exposes its surface through **three coordinated
APIs**:

1. **REST API** (`/api/...`) — the same endpoints the Blazor
   WASM client consumes. Documented in
   `docs/api/00-conventions.md` and the live OpenAPI document
   at `/swagger/v1/swagger.json` (Development environment).
2. **Model Context Protocol server** (stdio or HTTP+SSE) —
   the AI integration pillar. Documented in
   `docs/architecture/03-mcp-server.md` and
   `docs/ai/01-mcp-deep-dive.md`.
3. **Webhooks** (per-board, HMAC-SHA256 signed) — push
   notifications on board events. Documented in
   `docs/roadmap/00-feature-inventory.md` §10.4 and the
   webhook configuration endpoint at
   `/api/boards/{id}/webhooks`.

All three APIs share the same Application layer; the
authorization model is identical; the same Wolverine command
bus handles every write. Pick the surface that fits your
use case:

- **Browser / mobile app?** → REST API + API tokens.
- **AI assistant / agentic workflow?** → MCP server.
- **CI / SaaS / monitoring?** → Webhooks.
- **Production-grade integration with multiple workspaces?**
  → OAuth 2.0 third-party flow (coming in v1.1.0).
