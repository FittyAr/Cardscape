# Cardscape

> **The self-hostable kanban your AI can drive.**
>
> Drive your boards conversationally from any AI client. Keep
> full ownership of your data.

Cardscape is an open-source, self-hostable project-management tool
with a complete feature surface — kanban boards, calendar,
automation engine, extensions, Inbox, Planner, and AI — and a
Model Context Protocol server that lets AI assistants read,
create, and move cards on your behalf. It runs on **.NET 10**,
persists to **SQLite**, **PostgreSQL**, or **MariaDB**, and ships
under the **Reciprocal Public License 1.5**.

It is the only self-hostable kanban with a first-class MCP server.

---

## Why Cardscape

- **Self-hostable, you own the data.** One `docker compose up` and
  the whole thing is on your hardware. No vendor can read your
  boards, change the rules, or sunset the product.
- **AI integration that is not bolted on.** Cardscape ships an
  MCP server as a peer to the REST API. The same domain model,
  the same authorization, the same idempotency. An AI client
  drives the boards through the same `Application` layer a human
  does through the web UI.
- **Multi-database without lock-in.** SQLite for solo and dev,
  PostgreSQL or MariaDB for production. The provider is
  configuration, not code.
- **A complete feature surface.** Workspaces, boards, lists,
  cards, members, comments, checklists, attachments, calendar,
  automation rules, scheduled commands, Inbox, Planner,
  extensions, API tokens, audit logs. Designed for the long run,
  not a demo.
- **Modern .NET, end to end.** ASP.NET Core 10, Blazor WebAssembly,
  Entity Framework Core 10 LTS, Radzen.Blazor. Type-safe,
  fast, long-term support.
- **Open development.** Public roadmap, public ADRs, public
  issues. Every architectural decision is a Markdown file
  under `docs/adr/`.

---

## The MCP server (the differentiator)

The Model Context Protocol (MCP) is the open standard for
"AI agent ↔ external tool" in 2025-2026. Cardscape ships a
**first-party MCP server** in `src/Cardscape.Mcp/`. It is a
thin transport layer on top of the same `Application` layer the
REST API uses. Every MCP tool maps to an existing command or
query. The same Wolverine pipeline, the same FluentValidation
rules, the same `Result<T>`.

What that means in practice — an AI assistant with your
Cardscape MCP server configured can:

- Show every card assigned to you that is due this week.
- Create a card titled "Investigate the flaky integration test"
  on the Q3 Roadmap board, in the Doing list, assigned to you.
- Move every card with the `urgent` label to the Done list.
- Triage the Inbox on Monday morning and produce a standup
  summary.
- Plan the next sprint from the Backlog list of the active
  board.

All without a new HTTP round-trip, without a parallel REST
API, and without a copy-pasted auth flow.

See:

- [ADR 0002 — Model Context Protocol (MCP) server](docs/adr/0002-mcp-server.md)
- [Architecture — MCP server](docs/architecture/03-mcp-server.md)
- [Roadmap — feature inventory](docs/roadmap/00-feature-inventory.md)
  (the surface the MCP server will eventually expose)

---

## Status

Cardscape is at **`v1.0.0`** — first production release with
full Trello parity. The codebase runs end to end: a user can
register, create a workspace, drop in boards/lists/cards,
comment, label, star, attach files, vote with heart reactions,
add checklists with progress, set up custom fields, schedule
recurring cards, mint and revoke API tokens with per-token
rate limits, send and accept workspace invitations, manage
the Inbox, browse the calendar, plan in the swimlane view,
set up board automation rules, toggle per-board extensions,
push out per-board webhooks, and full-text search every
card. The full HTTP pipeline is covered by integration tests
against the real Program + DI + Wolverine + EF Core stack.
The MCP server exposes the same surface to AI clients and
pushes its writes back to the Web UI in real time.

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution scaffold, multi-DB plumbing, RPL-1.5, AGENTS contract, MCP server project skeleton, full documentation set | **DONE** |
| 1 | MVP: single user, sign-up, workspace, board, list, card, sign in, Web UI | **DONE** (`v0.1.0-mvp`) |
| 2 | Real-time (SignalR) + **MCP server end-to-end** (the differentiator ships here) | **DONE** (`v0.2.0-core-mcp`) |
| 3 | Access control on cards/lists + **first-class API tokens** for the MCP server + Web UI token management | **DONE** (`v0.3.0-api-tokens`) |
| 4 | Workspace invitations, Inbox, Calendar, Planner, automation engine, board extensions | **DONE** (`v0.4.0-realtime-mcp` through `v0.6.4-extensions`) |
| 5 | Background jobs, custom fields, voting, checklists, recurring cards, webhooks, attachments, full-text search, activity, rate limit | **DONE** (`v0.7.0-jobs` through `v0.7.10-polish`) |
| 6 | v1.0.0 release: full Trello parity, production config, CI with coverage, polished docs | **DONE** (`v1.0.0`) |

`dotnet build` is green. **313 unit tests + 85 integration tests**
are green. The Blazor WebAssembly client talks to the API over
JWT or per-user API tokens. The Docker image boots, applies
migrations, and serves the SPA on port 8080. Production
configuration has zero hard-coded secrets — all required
values come from environment variables. See
[the implementation plan](docs/roadmap/01-implementation-plan.md)
for the full phased delivery schedule and
[the working contract](docs/AGENTS.md) for how work is done on
this codebase.

### What ships in `v1.0.0`

Every Trello power-user feature is in:

- **Workspaces, boards, lists, cards** with positional
  re-ordering (decimal `Position` value object, not floats).
- **Members, comments, labels, due dates, cover colors,
  archiving, starring.**
- **Attachments** — drag-drop a file (up to 25 MB) on a card,
  stored in local disk under `Storage:LocalRoot` and streamed
  back via a download endpoint.
- **Voting** — heart-react on a card, per-user toggle, count
  surfaced in the card detail header.
- **Checklists** — per-card checklists with toggleable items
  and a progress bar.
- **Custom fields** — per-board typed fields (text / number /
  date / checkbox), settable on each card, surfaced in the
  card detail.
- **Recurring cards** — set an interval (1-365 days) and the
  background job dispatcher clones the card automatically
  when the next occurrence is due.
- **Webhooks** — register an HTTP endpoint per board, pick
  which events (`card.created`, `card.moved`,
  `card.completed`, `comment.added`) to forward, deliveries
  are HMAC-SHA256 signed and retried with exponential
  backoff (5 s base, capped at 5 min, max 5 attempts).
- **Full-text search** — case-insensitive substring search
  over cards, comments, checklist items, labels, and
  activity, optionally scoped to a single board, paginated.
- **Activity feed** — per-board and per-card timelines with
  cursor pagination.
- **Calendar & planner** — month grid and swimlane views
  over due-date cards.
- **Inbox** — in-app notifications, auto-created on card
  assignment, with a 60s-polling bell.
- **Workspace invitations** — owner mints by email, invitee
  redeems a one-shot token.
- **Automation engine** — per-board rules that react to
  card events (move / complete / reopen / created in list)
  and run server-side actions (move / assign / set due date /
  mark complete). Best-effort: a failed rule never blocks
  the user request.
- **Board extensions** — per-board toggles for the power-up
  features (Custom Fields, Voting, Card Repeater) with
  opaque JSON config.
- **Real-time (SignalR)** — board and card updates pushed
  to all connected clients within the same ASP.NET Core
  process.
- **Per-API-token rate limit** — each API token has its own
  `RateLimitPerHour` and `BurstSize`; the middleware uses
  the `ApiToken` auth scheme to identify the caller.
- **MCP server** — every domain command and query is
  exposed as a tool, including the new
  `cards_create_checklist`, `cards_vote`,
  `cards_set_recurrence`, `boards_list_webhooks`,
  `search`, and so on.
- **CI** — GitHub Actions build, format, unit, integration,
  and a separate coverage job that uploads an lcov
  artifact.
- **Production config** — `appsettings.Production.json` has
  zero secrets; every required value comes from an
  environment variable (`ConnectionStrings__Default`,
  `Jwt__SigningKey`, `Internal__Secret`,
  `Storage__LocalRoot`).

---

## Quickstart

### 1. Self-host with Docker (recommended)

The fastest way to a running Cardscape instance:

```bash
# Clone the repo
git clone https://github.com/cardscape/cardscape.git
cd cardscape

# Generate a real JWT signing key for production
export CARDS_CAPE_JWT_KEY=$(openssl rand -base64 48)

# (optional) Override the default Postgres password
export CARDS_CAPE_DB_PASSWORD=cardscape-dev-password

# Bring the stack up
docker compose up -d

# Open the UI
open http://localhost:8080
```

You can also use the SQLite-only dev compose if you do not want
to run Postgres:

```bash
docker compose -f docker-compose.dev.yml up -d
```

### 2. Developer quickstart (dotnet CLI)

For iterating on the source:

```bash
git clone https://github.com/cardscape/cardscape.git
cd cardscape
dotnet build                       # 11 projects, 0 errors, 0 warnings
dotnet test                        # 313 unit + 85 integration, all green
dotnet run --project src/Cardscape.Api          # API on http://localhost:5291
dotnet run --project src/Cardscape.Web          # Web on http://localhost:5206
```

The Blazor client reads its API base URL from
`src/Cardscape.Web/wwwroot/appsettings.json` and defaults to
`http://localhost:5291/`. Override with
`-- "ApiBaseUrl=http://your-api:8080/"` or by editing that
file before running.

### 3. Picking a database

Cardscape supports three providers, all switchable at runtime
through configuration:

| Provider | Connection string example | When to use |
|---|---|---|
| SQLite | `Data Source=Data/cardscape.db` | Solo, dev, single-node self-host |
| PostgreSQL | `Host=db;Port=5432;Database=cardscape;Username=…;Password=…` | Production, multi-user |
| MariaDB | `Server=db;Port=3306;Database=cardscape;Uid=…;Pwd=…` | Production, MySQL shops |

Set `Database__Provider` to `Sqlite`, `PostgreSQL`, or `MariaDB`
to match the connection string. Migrations live in a single set
under `src/Cardscape.Infrastructure/Persistence/Migrations/`
that runs on all three engines.

---

## What's in `v0.5.0-invitations`

This release closes the first slice of Phase 4: **workspace
member invitations**. A workspace owner can mint an invitation
by email, the invitee gets an email link, follows it, and is
added to the workspace with the granted role. The whole
lifecycle (issue, accept, list pending, revoke) is exposed
through the REST API, the MCP server, and the Blazor Web UI.

- **`WorkspaceInvitation` aggregate** (`src/Cardscape.Domain/
  Workspaces/`): stores the SHA-256 hash of a 32-byte random
  cleartext token + a 10-char display prefix. 14-day default
  expiry (max 60). `Issue`, `Accept`, `Revoke` domain methods
  with idempotent failure modes. Domain events
  `WorkspaceInvitationIssued`, `Accepted`, `Revoked`.
- **`IInvitationService`** owns the secret-generation +
  validation logic: `RandomNumberGenerator.GetBytes(32)` →
  base64url → SHA-256. The cleartext is returned exactly once
  at issuance; the server only persists the hash + prefix.
  `ValidateAsync` distinguishes "expired" / "revoked" /
  "already accepted" with specific error codes so the UI can
  show a useful message.
- **`IInvitationEmailService`** with a default `Console…`
  implementation that logs the invite URL. The public base
  URL is read from `App:PublicBaseUrl` (default
  `http://localhost:5206`, the Blazor WASM port); in
  production this is swapped for an SMTP / SES / SendGrid
  adapter behind the same interface.
- **REST surface** under `/api/workspaces/{id}/invitations` and
  `/api/invitations` (anonymous, owner-gated by
  `Authorization`): `POST` issues, `GET` lists, `DELETE`
  revokes, `POST /api/invitations/accept` redeems, `GET
  /api/invitations/pending` is the invitee-facing inbox.
- **MCP server** gets five new tools: `workspaces_invite`,
  `workspaces_list_invitations`, `workspaces_revoke_invitation`,
  `invitations_list_pending`, `invitations_accept`. The
  `workspaces_invite` tool returns the cleartext token in the
  same shape as the REST API.
- **Blazor UI**: `/invitations` is the invitee's read-only
  inbox (workspace name + role + expiry). `/invitations/accept`
  is the email-link landing page. `/workspaces/{id}/members`
  is the owner's members + invitations page, with an invite
  form (email + role) that shows the cleartext token in a
  one-shot panel — copy it into the email you actually send.
- **Bugfix**: `WorkspaceRepository.GetWithMembersAsync` was
  using `EF.Property<Guid>(w, "Id")` to compare the strongly-
  typed `WorkspaceId` against the parameter, which collided
  with the `HasConversion` pipeline and threw
  `Object must implement IConvertible` at materialization.
  The query is now `w.Id == id` and EF Core 10 handles the
  converter end to end. No production had hit this yet (no
  handler was using the path), but the integration test now
  pins the contract.
- **Tests**: 231 in total — 200 unit + 31 integration.
  `WorkspaceInvitationTests` (13 unit tests) covers email
  normalization, default + custom lifetime, hash/prefix
  validation, accept/revoke idempotency, expiry transitions.
  `WorkspaceInvitationTests` (6 integration tests) covers the
  full issue → accept → membership flow, wrong-email
  redemption, inbox filtering, non-owner rejection, and
  revoke-before-accept.

What is **not** in v0.5.0-invitations (still in Phase 4):

- Extensions, automation engine, calendar, Inbox, Planner —
  these ship across `v0.6.0-inbox` through `v0.6.4-extensions`.
- Search relevance + PostgreSQL FTS / Lucene.NET.
- Attachments and storage providers other than the local
  filesystem.

---

## What's in `v0.6.x` (Inbox, Calendar, Planner, Automation, Extensions)

The remaining five slices of Phase 4 ship as a series of small
releases so each one is reviewable in isolation. The shared
pattern: per-board or per-user feature, full REST + MCP + Web
UI coverage, integration tests against the real pipeline.

- **`v0.6.0-inbox`** — `Notification` inbox (`/inbox`),
  unread-count bell in the global nav (60s poll), auto-create
  on `AssignCardCommand` (skipped for self-assign), MCP tools
  `inbox_list` / `inbox_unread_count` / `inbox_mark_read` /
  `inbox_mark_all_read`.
- **`v0.6.1-calendar`** — month grid view of due-date cards
  (`/calendar`), `ListCardsDueInRangeQuery`, REST
  `GET /api/cards/calendar?from&to&boardId?`, MCP
  `cards_calendar`.
- **`v0.6.2-planner`** — swimlane roadmap view of due-date
  cards (`/planner`), one row per list, weeks of the month as
  columns. Pure read-only Web UI on top of the calendar query.
- **`v0.6.3-automation`** — `BoardAutomationRule` aggregate
  with 4 triggers (CardMoved / Completed / Reopened /
  CreatedInList) and 4 actions (Move / Assign / SetDueDate /
  MarkComplete). Server-side dispatcher wired to the card
  domain events; failed rules never block the user request.
  REST `/api/boards/{id}/automation`, MCP
  `automation_list_rules` / `create` / `enable` / `disable` /
  `delete`.
- **`v0.6.4-extensions`** — per-board extension toggles
  (`/boards/{id}/extensions`) for Custom Fields, Voting, and
  Card Repeater. Opaque `ConfigJson` per row; the
  feature-specific UI reads the JSON at runtime. Trello
  power-ups style, without committing to a full plugin loader
  yet. REST + 4 MCP tools (`boards_list_extensions` / `enable` /
  `disable` / `update_extension_config`).

Two recurring EF Core 10 traps bit each new repository and
forced fixes:

- **Strongly-typed-id value-object filter** — `e.BoardId.Value
  == x` cannot be translated to SQL. The fix is the same
  client-side filter via `AsAsyncEnumerable` we already use in
  `ApiTokenRepository` and `WorkspaceInvitationRepository`.
- **`EF.Property<T>(entity, "Id")` + `HasConversion`** — the
  shadow-property form collides with the converter pipeline and
  throws `InvalidCastException: Object must implement
  IConvertible` at materialization. The safe form is
  `entity.Id == id` (typed id comparison). `BoardRepository.
  GetWithMembersAsync` regressed to the broken form during
  Phase 4.6; restored.

Tests: **216 unit + 46 integration** = 262 in total, all green.

---

## What's in `v0.4.0-realtime-mcp`

This release turns the MCP server into a **peer** of the REST
API for real-time: any card/list/comment the AI creates or
moves on your behalf is broadcast through the same SignalR
hub the Web UI listens to, so the human user sees the change
without a page reload.

- **`/api/internal/broadcast` endpoint** in the API
  (X-Internal-Secret auth, configurable via `Internal:Secret`).
  The MCP process POSTs a `{boardId?, listId?, cardId?,
  method, payload}` envelope; the API resolves the
  `board:{boardId:N}` group and dispatches the matching
  `IBoardClient` method. 503 if the secret is not configured
  (sandboxed dev), 401 on a wrong secret, 400 on an unknown
  method.
- **`IBoardPushClient` + `HttpBoardPushClient`** in
  `src/Cardscape.Mcp/Realtime/`: typed methods for every
  pushed event. The MCP calls the push client after a
  successful command; failures log a warning and never block
  the tool result.
- **Shared payload records** moved from `Cardscape.Api/Hubs/
  IBoardClient.cs` to `Cardscape.Application/Realtime/
  BoardEventPayloads.cs`, so the API hub and the MCP push
  client serialize the same shape without duplication.
- **MCP tool coverage widened**: `lists_create`, `cards_create`,
  `cards_move`, `cards_complete`, `cards_reopen`, `comments_add`
  all push their result through the broadcast endpoint.
  `cards_move` looks up the source list id before invoking
  so the `FromListId` field in `CardMovedPayload` is correct.
- **`BoardBroadcastEndpointTests`** (5 integration tests):
  unconfigured (503), wrong secret (401), unknown method
  (400), happy path with `boardId` (202), `listId` resolver
  path.
- **Tests**: 212 in total — 187 unit + 25 integration.

What is **not** in v0.4.0-realtime-mcp:

- Workspace member invitations (the next slice, v0.5).
- Extensions, automation engine, calendar, Inbox, Planner.
- Search relevance + PostgreSQL FTS / Lucene.NET.
- Attachments and storage providers other than the local
  filesystem.

---

## What's in `v0.2.0-core-mcp`

This release adds **real-time board sync** (SignalR) and the
**MCP server end-to-end** — the differentiator. The full vertical
slice for a single user still works as in `v0.1.0-mvp`, plus:

- **Real-time board sync** (`/hubs/board`): every command
  (create, move, complete, comment, label, etc.) fires a
  domain event that the `DomainEventBroadcaster` ships to a
  `BoardHub` SignalR group. The Blazor client connects on
  board load, joins `board:{boardId}`, and reloads the board
  on any push — the "Live" indicator in the top-right shows
  connection state. Reconnect with backoff is built in.
- **MCP server end-to-end** (`src/Cardscape.Mcp/`): the same
  `Application` layer the REST API uses is now exposed to AI
  clients through 19 MCP tools — `workspaces_list`,
  `boards_list/get/create/star/unstar`, `lists_list/create`,
  `cards_list/get/create/move/complete/reopen/assign/attach_label`,
  `comments_add/list`, `labels_list/create`. Every tool goes
  through the same Wolverine bus, so authorization, validation,
  and side effects are exactly the same as the REST surface.
  Auth is JWT bearer (the same tokens the API issues); the
  planned `ApiToken` first-class entity lands in v0.3.
- **Newtonsoft.Json pin** to 13.0.3 — SignalR.Client 1.2.0
  pulled a transitive 11.0.2 that has a high-severity advisory
  (GHSA-5crp-9r3c-p9vr).
- **Tests**: 192 in total — 179 unit + 13 integration
  (added three SignalR hub tests: 401 without token, reachable
  with token, and the create-card → list-cards flow that
  exercises the broadcaster pipeline end-to-end).

What is **not** in v0.2.0-core-mcp (per the [implementation plan](docs/roadmap/01-implementation-plan.md)):

- Multi-user collaboration (workspaces are single-owner; the
  `board_members` / `workspace_members` tables are scaffolded
  but not yet enforced on read).
- API-token entity (the long-lived, scoped, revocable token
  the MCP server should ideally use instead of JWTs).
- Extensions, automation engine, calendar, Inbox, Planner.
- Search relevance + PostgreSQL FTS / Lucene.NET.
- Attachments and storage providers other than the local
  filesystem.

These land in Phases 3 through 5.

---

## What's in `v0.1.0-mvp`

This is the first self-hostable build. It ships the full
end-to-end vertical slice for a single user:

- **Auth**: register, login, JWT bearer (HS256), refresh token,
  password hashing (Pbkdf2), 60-minute access / 30-day refresh.
- **Workspaces, boards, lists, cards** with create / rename /
  archive / move. Boards can be **starred**; cards can be
  **assigned**, **labeled**, **dated**, **completed**, and
  **commented on**.
- **Multi-DB**: SQLite (default), PostgreSQL, MariaDB. The
  same EF Core migration runs on all three; the provider is
  picked at boot from configuration.
- **Blazor WebAssembly client** (`src/Cardscape.Web/`) that
  covers the full surface: sign up, sign in, workspaces, boards,
  board detail (kanban columns), card detail (metadata +
  comments), star/unstar, archive/restore.
- **REST API** with 60+ endpoints under `/api/...`, all using
  the same `Result<T>` pattern, RFC 7807 ProblemDetails on
  failure, JWT-gated.
- **Wolverine** for command/query routing (no MediatR, no
  source-generated dispatcher boilerplate).
- **Mapperly** for compile-time DTO mapping.
- **Docker** multi-stage build + `docker compose.yml` (API +
  Postgres) and `docker-compose.dev.yml` (SQLite only).
- **Tests**: 179 unit tests (domain + application, all fakes
  in-memory) and 10 integration tests (full HTTP stack via
  `WebApplicationFactory<Program>` with SQLite shared-memory).
  Both suites are green in under 10 seconds combined.

---

## Architecture, in one diagram

```
                 ┌────────────────────────┐
                 │     Cardscape.Web      │   Blazor WASM client
                 │   no server deps       │   (AuthService + typed API clients)
                 └────────────┬───────────┘
                              │  HTTP (JSON, JWT bearer)
                              ▼
   ┌─────────────────────────────────────────────────────┐
   │                       Cardscape.Api                   │  ← presentation
   │   minimal API endpoints, JWT bearer, Swagger,        │
   │   DI composition root, provider selection             │
   └──────┬───────────────────────────────────┬───────────┘
          │                                   │
          ▼                                   ▼
   ┌────────────────────┐          ┌────────────────────────┐
   │   Application      │  ←────   │    Infrastructure     │  ← technical
   │   use cases        │          │    EF Core, Identity,  │
   │ (Wolverine + FV +  │          │    Storage, Email     │
   │  Mapperly)         │          │                        │
   └────────┬───────────┘          └────────────────────────┘
            ▲                                   ▲
            │                                   │
            │         ┌─────────────────────────┐
            │         │     Cardscape.Mcp       │   ← AI integration
            └─────────┤  Model Context Protocol │     (stdio or HTTP+SSE)
                      │  talks to Application   │
                      └─────────────────────────┘
```

- Clean Architecture, six source projects (Domain,
  Application, Infrastructure, Api, Web, Mcp) plus five
  test projects.
- The dependency graph is strict and one-directional, enforced
  by `tests/Cardscape.ArchitectureTests` (NetArchTest).
- The same `Application` layer is consumed by both the REST API
  and the MCP server.

Full layout and dependency rules:
[`docs/architecture/00-overview.md`](docs/architecture/00-overview.md).

---

## Stack

| Layer | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | SDK `10.0.302` (latest 10.0.x feature band) |
| Web framework | ASP.NET Core minimal APIs | 10.0.10 |
| Client | Blazor WebAssembly | 11.0 preview 6, Radzen components |
| UI components | Radzen.Blazor | 11.1.8 |
| ORM | Entity Framework Core | 10.0.10 LTS (third-party providers trail .NET) |
| DB providers | Sqlite, Npgsql, MySql.EntityFrameworkCore | runtime, all switchable via config |
| Validation | FluentValidation | 11.11.0 |
| CQRS / Mediator | Wolverine | 6.23.1 (JasperFx), source-generator |
| Mapping | Mapperly | 4.3.1 (Riok), source-generator |
| Auth | JWT bearer + Pbkdf2 password hasher | built-in + `System.IdentityModel.Tokens.Jwt` 8.2.1 |
| AI integration | Model Context Protocol | .NET SDK `1.4.1`, stdio today |
| Tests | xUnit + FluentAssertions + `WebApplicationFactory` | 2.9.2 / 6.12.2 / 11.0 preview 6 |
| Containers | Docker + docker compose | multi-stage Dockerfile, Postgres + API |
| License | Reciprocal Public License 1.5 | RPL-1.5 |

Stack rationale and pinned versions are in
[`docs/AGENTS.md`](docs/AGENTS.md).

---

## Project layout

```
Cardscape/
├── .agents/                      # contract for AI agents (mirrored in docs/)
│   ├── AGENTS.md
│   └── skills/                   # project-local skills
├── docs/                         # the design + architecture corpus
│   ├── README.md                 # documentation index
│   ├── AGENTS.md                 # the working contract (human view)
│   ├── adr/                      # append-only architecture decision records
│   ├── architecture/             # how the solution is shaped
│   ├── development/              # how to set up and work on the solution
│   ├── api/                      # public API conventions
│   ├── roadmap/                  # where the project is going
│   └── community/                # community-facing reference docs (changelog, roadmap, governance, maintainers, launch)
├── src/                          # 6 source projects (Domain, Application, Infrastructure, Api, Web, Mcp)
├── tests/                        # 5 test projects (xUnit)
├── tools/                        # developer tooling
├── samples/                      # sample clients
├── Directory.Build.props         # shared MSBuild properties
├── Directory.Packages.props      # central package management
├── global.json                   # pinned .NET SDK
├── Cardscape.slnx                # solution file
├── LICENSE                       # RPL-1.5
└── README.md                     # you are here
```

---

## Documentation map

| If you want to… | Read |
|---|---|
| Understand the design philosophy and the working rules | [`docs/AGENTS.md`](docs/AGENTS.md) |
| See the full target feature surface | [`docs/roadmap/00-feature-inventory.md`](docs/roadmap/00-feature-inventory.md) |
| See the phased delivery plan | [`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md) |
| See why a specific decision was made | [`docs/adr/`](docs/adr/) |
| Understand the architecture and bounded contexts | [`docs/architecture/00-overview.md`](docs/architecture/00-overview.md) |
| Add a new feature end-to-end | [`docs/development/02-vertical-slices.md`](docs/development/02-vertical-slices.md) |
| Set up the solution on your machine | [`docs/development/00-onboarding.md`](docs/development/00-onboarding.md) |
| Learn the C# and EF Core conventions | [`docs/development/01-conventions.md`](docs/development/01-conventions.md) |
| See how the test matrix is organized | [`docs/development/03-testing-strategy.md`](docs/development/03-testing-strategy.md) |
| Drive the API from a third party | [`docs/api/00-conventions.md`](docs/api/00-conventions.md) |

---

## Contributing

Cardscape is a **solo-maintained**, public, open-source project.
The bar is "think big and professional": ADR-grade decisions,
polished documentation, an architecture that scales, a UX that
competes with hosted kanban tools, and an AI integration no
other self-hostable kanban has.

Contributions are welcome. The place to start is
[`docs/AGENTS.md`](docs/AGENTS.md) — it is the contract every
contributor (human or AI agent) reads before touching the
codebase. It covers:

- The stack and pinned versions.
- The Clean Architecture rules.
- The MCP server as the differentiator pillar.
- The "design for three, test on one" persistence strategy.
- The "no corners cut, no demo MVP" rule.
- The 10 working rules for any agent (working tree hygiene,
  ADR append-only, migration incantation, etc.).
- The list of available project-local skills.

If you are an AI agent picking up a task: read
`.agents/AGENTS.md` first (it is the operational contract your
tool reads at runtime), then `docs/AGENTS.md` for the
human-friendly rendering.

A `CONTRIBUTING.md` with the formal contribution flow (issues,
PRs, review process, release process) will land with the first
external contribution. Until then, the working rules in
`docs/AGENTS.md` are the contract.

---

## Community files — current state

| File | Status | Notes |
|---|---|---|
| `README.md` | **this file** | public pitch + status |
| `LICENSE` | **present** | RPL-1.5, full text |
| `CONTRIBUTING.md` | **present** | formal contribution flow |
| `CODE_OF_CONDUCT.md` | **present** | Contributor Covenant v2.1 |
| `SECURITY.md` | **present** | vulnerability reporting process |
| `SUPPORT.md` | **present** | where to ask questions |
| `docs/community/` | **present** | changelog, roadmap, governance, maintainers, contributors, launch runbook |
| `docs/AGENTS.md` | **present** | working contract for any agent |
| `.agents/AGENTS.md` | **present** | operational contract for AI tools |
| `docs/adr/` | **present** | 2 ADRs, append-only |
| `docs/roadmap/` | **present** | inventory + implementation plan + product positioning |
| `docs/brand/` | **present** | brand kit (palette, typography, logo) |
| `.github/ISSUE_TEMPLATE/` | **present** | bug, feature, question templates |
| `.github/PULL_REQUEST_TEMPLATE.md` | **present** | PR template with checklists |
| `.github/DISCUSSION_TEMPLATE/` | **present** | announcements, ideas, Q&A, show-and-tell |
| `site` branch | **present** | public website (orphan branch, single-page HTML+CSS) |

Everything in the table is in the repo. New community files
land as the project needs them.

---

## Contributing

Cardscape is a **solo-maintained**, public, open-source project.
The bar is "think big and professional". Contributions are
welcome.

- Read [`CONTRIBUTING.md`](CONTRIBUTING.md) for the formal
  contribution flow.
- The working contract every contributor (human or AI agent)
  reads first is [`docs/AGENTS.md`](docs/AGENTS.md).
- For "how do I…" questions, use
  [GitHub Discussions → Q&A](https://github.com/cardscape/cardscape/discussions/categories/q-a).
- For bug reports, use the
  [bug report issue template](https://github.com/cardscape/cardscape/issues/new?template=bug_report.md).
- For feature requests, use the
  [feature request issue template](https://github.com/cardscape/cardscape/issues/new?template=feature_request.md).
- For security disclosures, read [`SECURITY.md`](SECURITY.md)
  — do **not** file a public issue.

By participating, you agree to the
[Contributor Covenant v2.1](CODE_OF_CONDUCT.md).

---

## License

Cardscape is licensed under the
**Reciprocal Public License 1.5 (RPL-1.5)**. See
[`LICENSE`](LICENSE) for the full text.

The short version: you can use it, you can read it, you can
fork it, you can deploy it. If you distribute a modified
version, your modifications must also be RPL-1.5. This is
deliberate — Cardscape is built in the open, and improvements
must stay in the open.

RPL-1.5 is OSI-approved and is the right license for a project
that wants to stay open while preventing proprietary
fork-the-code-and-close-it moves.

---

## Acknowledgements

Cardscape stands on the shoulders of:

- The **.NET** team and the **Entity Framework Core** team for
  the runtime and the ORM.
- **Anthropic** and the **Model Context Protocol** working
  group for the open standard that makes first-class AI
  integration possible.
- The **Radzen** team for the Blazor component library.
- Every open-source kanban and project-management tool that
  showed the shape of the feature space.
