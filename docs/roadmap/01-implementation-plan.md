# Implementation plan

> The phased delivery plan for Cardscape, derived from
> [`00-feature-inventory.md`](00-feature-inventory.md).
> Phases are ordered; the bar is **full  parity** plus
> Cardscape's MCP differentiator.
>
> **Maintainer's directive**: "pensar en grande y profesional.
>  tiene más que el sistema de columnas y tarjetas.
> También debemos incluir el calendario y todas las features
> que tiene ese proyecto, al menos todo lo que podamos
> replicar."

## 0. Status

| Phase | Status | Notes |
|---|---|---|
| 0 — Solution scaffold | **DONE** | Commits `cd2170b`, `dc8e68b`, `31c21d6`. |
| 1 — MVP (workspaces / boards / cards / auth) | not started | Target: end of August 2026 (4–6 weeks). |
| 2 — Core + MCP server (differentiation pillar) | not started | Target: end of October 2026. |
| 3 — Power-ups & Automation | not started | Target: end of December 2026. |
| 4 — Enterprise & AI | not started | Target: end of Q1 2027. |
| 5 — Polish & scale | not started | ongoing, no fixed target. |

The dates are **aspirational** and depend on contributor
bandwidth. Each phase ends with a tagged release
(`v0.1.0-mvp`, `v0.2.0-core`, etc.) and a self-hosted demo
deployable from a single `docker-compose up`.

The ambition is **"all of , plus the MCP pillar"**. The
MVP (Phase 1) is the smallest shippable cut. From Phase 2
onward, every  feature surface listed in
[`00-feature-inventory.md`](00-feature-inventory.md)
is in scope; we explicitly call out the ones we are **not**
shipping (e.g. paid-only Atlassian Intelligence features) in the
relevant phase section.

---

## 1. Phase 0 — Solution scaffold (DONE)

**Commits:** `cd2170b` + `dc8e68b` + `31c21d6`

Deliverables:

- 10-project Clean Architecture skeleton in `src/` and
  `tests/`.
- Central Package Management on `Directory.Packages.props`.
- RPL-1.5 LICENSE.
- Project-local `.agents/` folder with the working contract
  and 5 skills (4 from `dotnet/skills`, 1 from
  `devixlabs/DotNetWebApp`).
- Multi-provider persistence scaffolding (SQLite / PostgreSQL /
  MariaDB), with the test matrix decided as **SQLite-only** for
  now and the convention for growing it documented in
  [`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md).
- Radzen.Blazor wired into the Blazor WASM client
  (`AddRadzenComponents()`, providers in `App.razor`, `@using`
  in `_Imports.razor`).
- `docs/` set with the full onboarding, conventions,
  architecture, API, and roadmap documents (13 markdown files,
  116 KB).
- `dotnet build` green (10/10 projects, 0 errors, 0 warnings).

What's intentionally **not** in this phase:

- No domain entity. No command, no handler, no endpoint, no
  component, no MCP tool. The solution is featureless by design.

## 2. Phase 1 — MVP (4–6 weeks)

**Goal:** a single user can sign up, create a workspace,
create a board, add lists and cards, drag cards between
lists, and sign in again tomorrow to see the same state. No
collaboration, no integrations, no AI, no MCP. This is the
smallest end-to-end vertical that proves the architecture.

### 2.1 Domain & Application

- [ ] `Workspace` aggregate: name, owner, members, board
  collection.
- [ ] `Board` aggregate: name, description, visibility
  (private / public), list collection, member collection,
  label collection.
- [ ] `List` aggregate: name, position, card collection,
  archived state.
- [ ] `Card` aggregate: title, description, position, due date
  (optional), members, labels, checklists, attachments,
  comments, activity events, archived state.
- [ ] `Label` entity: name, color.
- [ ] `Member` entity: user reference + role (admin / member /
  observer).
- [ ] `User` entity: email, display name, avatar, password
  hash, created date, last login.
- [ ] `Attachment` entity: name, mime type, size, blob storage
  reference, uploader.
- [ ] `Checklist` and `ChecklistItem` entities.
- [ ] `Comment` entity.
- [ ] `Activity` entity (append-only event log).
- [ ] Common value objects: `UserId`, `WorkspaceId`, `BoardId`,
  `ListId`, `CardId`, `LabelId`, `Color`, `EmailAddress`,
  `PasswordHash`, `BoardVisibility`, etc.
- [ ] Common errors: `NotFound`, `Forbidden`, `Validation`,
  `Conflict`, etc.

### 2.2 Infrastructure

- [ ] `CardscapeDbContext` with all the entity configurations.
- [ ] Three sets of migrations (one per provider), starting
  with `InitialSchema` and covering the entities above.
- [ ] ASP.NET Identity integration.
- [ ] `LocalFileStorageService` for attachments.
- [ ] `AspNetEmailService` that writes to the console in
  Development and to a queue in Production.
- [ ] `DomainEventsInterceptor` that dispatches domain events
  after `SaveChangesAsync`.
- [ ] `AuditableEntityInterceptor` that stamps `CreatedAt`,
  `CreatedBy`, `UpdatedAt`, `UpdatedBy`.

### 2.3 API

- [ ] `AuthEndpoints`: register, login, refresh, logout.
- [ ] `WorkspaceEndpoints`: CRUD + invite.
- [ ] `BoardEndpoints`: CRUD + star/unstar + archive.
- [ ] `ListEndpoints`: CRUD + reorder + move-all-cards.
- [ ] `CardEndpoints`: CRUD + move + assign + label + comment +
  checklist.
- [ ] `LabelEndpoints`: CRUD.
- [ ] `MemberEndpoints`: list, change role, remove.
- [ ] `SearchEndpoints`: full-text.
- [ ] `ActivityEndpoints`: per-board and per-card.
- [ ] `NotificationEndpoints`: list unread, mark as read.
- [ ] Global exception handling middleware.
- [ ] JWT bearer authentication.
- [ ] CORS for the Blazor dev server.
- [ ] Health checks.
- [ ] Swagger UI in Development.

### 2.4 Web

- [ ] Layout: header, left nav, main content.
- [ ] Routes: `/`, `/login`, `/register`, `/workspaces`, etc.
- [ ] Auth flow.
- [ ] Board view: lists as columns, cards stacked, drag and
  drop.
- [ ] Card detail: description, comments, checklists,
  attachments, activity tabs.
- [ ] Radzen components throughout.
- [ ] Material theme + dark mode toggle.
- [ ] Responsive layout down to tablet.

### 2.5 Tests

- [ ] Unit tests for every command/query handler.
- [ ] Unit tests for every value object.
- [ ] Unit tests for every domain rule.
- [ ] Integration tests for every endpoint with happy and
  error paths.
- [ ] Architecture tests (already wired, expand with new
  rules).
- [ ] Functional smoke test: register → workspace → board →
  list → card → move → archive.

### 2.6 Release

- `v0.1.0-mvp` tag.
- `docker-compose.yml`: Api + Web + SQLite volume.
- README with a 5-minute quick-start.
- Demo deploy.

## 3. Phase 2 — Core + MCP server (4–6 weeks)

**Goal:** collaboration features, real-time, **and the MCP
server that differentiates Cardscape from every other
 clone**. After this phase, an AI client can drive
Cardscape end-to-end via the MCP protocol.

### 3.1 MCP server (`Cardscape.Mcp`)

This is the **headline feature of Phase 2**. See
[`../architecture/03-mcp-server.md`](../architecture/03-mcp-server.md)
and [ADR 0002](../adr/0002-mcp-server.md).

- [ ] New project `src/Cardscape.Mcp/` (already scaffolded
  in Phase 0).
- [ ] `ApiToken` entity in the `Members` context.
- [ ] `ApiTokenAuthenticationHandler` (Bearer token in
  `Authorization: Bearer <secret>`).
- [ ] `ICurrentUser` resolved from the authenticated principal.
- [ ] MCP server registered via
  `services.AddMcpServer(...).WithHttpTransport().WithStdioServerTransport().WithToolsFromAssembly(...)`.
- [ ] Initial tools: `workspaces_list`, `boards_list`,
  `boards_get`, `cards_list`, `cards_get`, `cards_create`,
  `cards_update`, `cards_move`, `cards_archive`,
  `comments_add`, `members_assign`, `search`, `labels_add`,
  `checklist_add`.
- [ ] Initial resources: `board://{id}`, `card://{id}`,
  `workspace://{id}`, `cards://board/{id}`.
- [ ] Initial prompts: `standup-summary`, `triage-inbox`,
  `sprint-planning`, `weekly-review`, `stale-cards`.
- [ ] Idempotency: every write tool accepts an
  `idempotencyKey` parameter; backed by a new
  `IdempotencyKey` entity.
- [ ] OpenTelemetry tracing: every tool call emits a span
  with the `mcp.tool.*` attributes; trace context
  propagates into the Application layer.
- [ ] Smoke test: launch the server, connect with Claude
  Desktop, run the `triage-inbox` prompt against a seeded
  workspace.
- [ ] Documentation: "Build your own MCP client" guide in
  `docs/extensions/`.

### 3.2 Collaboration

- [ ] Member invitations via email.
- [ ] Board-level and workspace-level roles with real
  authorization rules.
- [ ] @mentions in card descriptions and comments.
- [ ] Reactions on comments.
- [ ] Watch / un-watch.
- [ ] Email notifications.

### 3.3 Card features

- [ ] Card mirror.
- [ ] Card snooze.
- [ ] Card repeater.
- [ ] Card aging.
- [ ] Voting.
- [ ] Bulk actions.

### 3.4 Productivity

- [ ] Board templates (workspace-level).
- [ ] Saved filters per board.
- [ ] Quick capture: Inbox endpoint, voice capture via
  browser MediaRecorder API.
- [ ] Command palette (Cmd-K style).
- [ ] Recent boards in the home page.

### 3.5 Real-time

- [ ] SignalR hub per board: live card moves, live comments.
- [ ] Presence indicators.
- [ ] Typing indicators in comments.

### 3.6 Mobile

- [ ] Responsive layout down to phone width.
- [ ] Touch-friendly drag-and-drop.
- [ ] PWA manifest: installable, offline shell.

### 3.7 Release

- `v0.2.0-core-mcp` tag.
- Public blog post: "Cardscape ships a Model Context Protocol
  server. No other  clone does this."

## 4. Phase 3 — Power-ups & Automation (6–8 weeks)

**Goal:** match 's "extensibility" story. We build the
most-requested first-party extensions and a Automation-equivalent
automation engine.

### 4.1 Power-up framework

- [ ] `IPowerUp` interface and DI registration.
- [ ] UI: a "Power-Ups" tab in board settings.

### 4.2 First-party extensions

- [ ] **Calendar** — board view showing due-date cards on a
  monthly grid. **Specifically called out by the
  maintainer** as a Phase 3 priority.
- [ ] **Table** — board view with cards as rows.
- [ ] **Timeline** (Gantt) — board view with start / end
  dates.
- [ ] **Dashboard** — board view with cards-per-list,
  cards-per-member, overdue counts.
- [ ] **Custom Fields** — schema definition UI, field rendering
  on card back.
- [ ] **Card Aging**.
- [ ] **Card Snooze**.
- [ ] **Card Repeater**.
- [ ] **Voting**.
- [ ] **List Limits** (WIP cap).
- [ ] **Dashcards**.

### 4.3 Automation automation

- [ ] Rule engine: trigger → actions.
- [ ] Triggers: card created, moved, archived, due date,
  member added, label added.
- [ ] Actions: move, copy, archive, assign, label, comment,
  due date, mark complete.
- [ ] Custom card buttons.
- [ ] Custom board buttons.
- [ ] Scheduled commands (cron-like).
- [ ] Per-user quota (configurable; default 250 / month).

### 4.4 Integrations (first-party)

- [ ] **Webhooks** — outgoing POSTs.
- [ ] **iCalendar feed** — per-board `.ics` of due dates.
- [ ] **Slack** — channel notifications.
- [ ] **Google Drive** — file picker.
- [ ] **GitHub** — link PRs and issues.
- [ ] **Email-to-board** — inbound email creates a card.

### 4.5 Developer experience

- [ ] OpenAPI spec published to a public URL.
- [ ] Personal access tokens for the REST API.
- [ ] OAuth flow for third-party apps.
- [ ] Webhook signature verification.

### 4.6 Release

- `v0.3.0-extensions` tag.

## 5. Phase 4 — Enterprise & AI (6–8 weeks)

**Goal:** the features a 50-person company needs before they
trust Cardscape with their work.

### 5.1 Security

- [ ] OAuth 2.0 / OIDC login (Google, Microsoft, Apple).
- [ ] SAML SSO (enterprise tier).
- [ ] Two-factor authentication (TOTP).
- [ ] API tokens with scopes (already in place from Phase 2's
  MCP work).
- [ ] Session management: list / revoke active sessions.
- [ ] Audit logs (every administrative action).

### 5.2 Enterprise admin

- [ ] Org-wide permissions.
- [ ] SCIM provisioning.
- [ ] Data residency (per-workspace region).
- [ ] Guard-style policy enforcement.

### 5.3 Inbox & Planner

- [ ] Inbox page (cards captured outside any board).
- [ ] Planner page (personal calendar of due dates).
- [ ] Google Calendar sync (OAuth).
- [ ] Smart scheduling.

### 5.4 AI (Cardscape AI-style)

- [ ] **Provider abstraction** in
  `Application/Abstractions/IAiService.cs`.
- [ ] **Multiple providers**: built-in rule-based + an
  OpenAI-compatible provider (BYOK).
- [ ] **Card description generation**.
- [ ] **Comment summary**.
- [ ] **Auto-checklist** from description.
- [ ] **Smart Boards** — AI suggestions.
- [ ] MCP tools for AI queries (e.g. `ai_suggest_owners`,
  `ai_summarize_thread`).

### 5.5 Release

- `v0.4.0-enterprise` tag.

## 6. Phase 5 — Polish & scale (ongoing)

- [ ] Internationalization (i18n): extract all strings; ship
  English + Spanish, then more.
- [ ] Theming: full dark mode, custom themes.
- [ ] Performance: caching (Redis), CDN for attachments.
- [ ] Background jobs: Hangfire for Automation schedules.
- [ ] Rate limiting: per-user and per-IP.
- [ ] Webhooks v2: retry with backoff, dead-letter queue.
- [ ] Mobile native apps (MAUI or PWA-only).
- [ ] Realtime: presence, typing indicators, live drag.
- [ ] MCP subscriptions: live updates over the MCP protocol
  (resource subscriptions).
- [ ] Export: per-board archive download (JSON + attachments).
- [ ] **Import:  JSON import** (parse
  `boards.cardscape.json` files). Critical for adoption
  — let users move from  to Cardscape.
- [ ] API client SDK: ship a C# client, generated from
  OpenAPI via Kiota.
- [ ] Public status page.
- [ ] Security audit (third-party).
- [ ] Pen test.
- [ ] SOC 2 / GDPR compliance docs.
- [ ] Migration tooling from  to Cardscape.

## 7. What we are explicitly NOT building

- **No AI image generation** beyond the AI features listed
  in Phase 4. We're not Stable Diffusion for boards.
- **No marketplace** for third-party extensions. Power-ups are
  first-party only. The `IPowerUp` interface is public so
  self-hosters can build their own; we don't curate or
  distribute them.
- **No billing / subscription**. Cardscape is open-source
  under RPL-1.5; there's no paid tier. "Enterprise" features
  in Phase 4 are gated by configuration, not by a license
  server.
- **No public cloud version**. We are self-host-first. Cloud
  hosting is up to the user.

## 8. Tracking

We track progress with GitHub issues and milestones, one
milestone per phase. Each issue references the phase it
belongs to and (when applicable) the section of
`00-feature-inventory.md` that motivates it.

The roadmap is reviewed at the end of every phase. New
phases can be added, but the existing phase structure
(0–5) doesn't get reordered — what we already promised
stays in the same order.

## 9. References

- [`00-feature-inventory.md`](00-feature-inventory.md)
  — the source feature list.
- [`../architecture/00-overview.md`](../architecture/00-overview.md)
  — how the solution is shaped.
- [`../architecture/03-mcp-server.md`](../architecture/03-mcp-server.md)
  — the MCP server operational guide.
- [`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md)
  — the multi-provider / SQLite-only decision.
- [`../adr/0002-mcp-server.md`](../adr/0002-mcp-server.md) —
  the MCP server decision.
- [`../development/02-vertical-slices.md`](../development/02-vertical-slices.md)
  — the recipe for adding a feature end-to-end.
