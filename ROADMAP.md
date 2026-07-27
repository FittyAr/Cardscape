# Cardscape roadmap

> The public, community-readable version of the project's
> delivery plan. The detailed, internal version (with
> per-phase task lists, the maintainer's working notes, and
> the rationale for each decision) is in
> [`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md).
> This file is the short version for newcomers.

---

## Where we are

Cardscape is in **pre-alpha**. The architecture is in place,
the documentation is in place, the project has a clean
vendor-neutral positioning, and the public website is live.
The product itself is not yet runnable.

The first runnable build — a single user, a workspace, a
board, lists, cards, drag-and-drop, sign in tomorrow — is
**Phase 1**, the MVP. The target is the end of **August
2026**.

---

## The phases

| Phase | Theme | Target | Status |
|---|---|---|---|
| 0 | Solution scaffold | — | ✅ done |
| 1 | MVP (workspaces / boards / cards / auth) | end of August 2026 | 🔄 in progress |
| 2 | Core + MCP server (the differentiator) | end of October 2026 | ⏳ not started |
| 3 | Extensions & automation | end of December 2026 | ⏳ not started |
| 4 | Enterprise & AI | end of Q1 2027 | ⏳ not started |
| 5 | Polish & scale | ongoing | ⏳ ongoing |

Dates are **aspirational**. The project is solo-maintained.
Each phase ends with a tagged release and a self-hostable
demo deployable from a single `docker compose up`.

---

## Phase 0 — Solution scaffold ✅

- 6 source projects in Clean Architecture (Domain, Application,
  Infrastructure, Api, Web, Mcp).
- 5 test projects (unit, integration, architecture, functional,
  shared test common).
- Multi-provider persistence: SQLite, PostgreSQL, MariaDB.
  Test matrix today is SQLite-only; the convention to grow
  it is documented in [ADR 0001](docs/adr/0001-multi-provider-strategy.md).
- RPL-1.5 LICENSE.
- `Cardscape.Mcp` project skeleton with stdio transport and
  the auth handler scaffold. See
  [ADR 0002](docs/adr/0002-mcp-server.md).
- Full `docs/` set: working contract, ADRs, architecture,
  development conventions, API conventions, feature inventory,
  implementation plan, product positioning.
- Public website on the `site` branch.

---

## Phase 1 — MVP

A single user can sign up, create a workspace, create a
board, add lists and cards, drag cards between lists, and
sign in tomorrow to see the same state. No collaboration,
no integrations, no AI, no MCP. This is the smallest
end-to-end vertical that proves the architecture.

**Deliverables**

- Domain entities: Workspace, Board, List, Card, User,
  Member, Label, Attachment, Checklist, Comment, Activity.
- EF Core persistence with migrations for all three
  providers (SQLite, PostgreSQL, MariaDB).
- ASP.NET Identity integration.
- REST endpoints for the entities above.
- Blazor WASM client with the board view, drag-and-drop,
  and the card detail.
- Radzen components throughout.
- `v0.1.0-mvp` tag.
- `docker-compose.yml` for self-hosting.
- A 5-minute quickstart in the README.

---

## Phase 2 — Core + MCP server

Collaboration features (member invitations, roles,
@mentions, comments with reactions, watch / un-watch,
email notifications), real-time (SignalR hub, presence,
typing indicators, live drag), and **the headline** — the
MCP server that ships the differentiator end-to-end.

**Deliverables**

- The `Cardscape.Mcp` server, with the initial set of tools,
  resources, and prompts.
- Bearer-token authentication via the new `ApiToken` entity
  in the `Members` context.
- Idempotency keys on every MCP write tool.
- OpenTelemetry traces end-to-end: MCP call → handler →
  repository → DB.
- Real-time SignalR hub.
- Bulk actions on cards (multi-select, multi-move).
- Inbox quick capture (the start of the Inbox feature; full
  Inbox lands in Phase 4).
- Board templates, saved filters, command palette.
- PWA manifest, installable, offline shell.
- `v0.2.0-core-mcp` tag.

This is the phase that makes Cardscape worth choosing over
every other self-hostable kanban. The MCP server is the
differentiator; the rest of the phase is the surface the AI
client can drive.

---

## Phase 3 — Extensions & automation

The extension framework, the most-requested first-party
extensions (Calendar, Table, Timeline, Dashboard, Custom
Fields, Card Aging / Snooze / Repeater, Voting, List Limits,
Dashcards), and a full automation engine (rules, custom
buttons, scheduled commands).

**Deliverables**

- The `IExtension` framework and DI registration.
- The first-party extensions listed above, with the
  Calendar extension as the maintainer's priority.
- The automation engine: rule editor, custom card and board
  buttons, scheduled commands (cron-like), per-user quota.
- The first-party integrations: Webhooks, iCalendar feed,
  Slack, Google Drive, GitHub, email-to-board.
- OpenAPI spec published to a public URL.
- Personal access tokens for the REST API.
- OAuth flow for third-party apps.
- `v0.3.0-extensions` tag.

---

## Phase 4 — Enterprise & AI

The features a 50-person company needs before they trust
Cardscape with their work: OAuth / OIDC / SAML SSO,
two-factor authentication, audit logs, SCIM provisioning,
data residency, and the Inbox + Planner (the personal
productivity surface). Plus the first wave of **Cardscape
AI** features: card description generation, comment
summary, auto-checklists, smart suggestions.

**Deliverables**

- OAuth / OIDC login (Google, Microsoft, Apple).
- SAML SSO.
- Two-factor authentication (TOTP).
- API tokens with scopes.
- Audit logs.
- SCIM provisioning.
- Data residency (per-workspace region).
- Inbox + Planner pages.
- Google Calendar sync.
- Cardscape AI provider abstraction.
- Card description generation, comment summary,
  auto-checklists, smart boards.
- MCP tools for AI queries.
- `v0.4.0-enterprise` tag.

---

## Phase 5 — Polish & scale (ongoing)

The long tail. i18n, theming, performance, background jobs,
MCP subscriptions, import from other kanban tools, the
C# API client SDK, public status page, security audit,
pen test, SOC 2 / GDPR compliance docs. No fixed target
date — this phase is "as long as the project is alive".

---

## What we are explicitly NOT building

- **No AI image generation** beyond the AI features listed
  in Phase 4. Cardscape is not Stable Diffusion for boards.
- **No marketplace** for third-party extensions. Extensions
  are first-party only. The `IExtension` interface is
  public so self-hosters can build their own; we do not
  curate or distribute them.
- **No billing / subscription**. Cardscape is open-source
  under RPL-1.5; there is no paid tier. "Enterprise"
  features in Phase 4 are gated by configuration, not by a
  license server.
- **No public cloud version**. We are self-host-first. Cloud
  hosting is up to the user.

---

## How to influence the roadmap

The roadmap is **not** a contract. The maintainer reviews it
at the end of every phase and adjusts the next phase based
on what the community is asking for.

The most effective ways to influence it:

1. **File a feature request** as a GitHub issue using the
   **Feature request** template. A well-written request with
   a clear problem statement, a user story, and a proposed
   solution moves the conversation forward.
2. **Open an Idea discussion** for open-ended "what if we
   built X?" conversations. Ideas are not commitments; they
   are conversations.
3. **Open a PR** for a small, scoped change. A working PR
   with a test is the most concrete form of feedback.
4. **Adopt and use Cardscape.** Real users with real boards
   find the gaps that no design doc anticipates.

The roadmap does **not** change in response to a single
strong opinion; it changes in response to a pattern of need
across the community.

---

## Where to read more

| If you want to know… | Read |
|---|---|
| the full per-phase task list | [`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md) |
| the full target feature surface | [`docs/roadmap/00-feature-inventory.md`](docs/roadmap/00-feature-inventory.md) |
| why a specific decision was made | [`docs/adr/`](docs/adr/) |
| how Cardscape presents itself | [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md) |
| the architecture and bounded contexts | [`docs/architecture/00-overview.md`](docs/architecture/00-overview.md) |
| how the MCP server works | [`docs/architecture/03-mcp-server.md`](docs/architecture/03-mcp-server.md) and [ADR 0002](docs/adr/0002-mcp-server.md) |
| how to contribute | [`CONTRIBUTING.md`](CONTRIBUTING.md) |
| the release process | [`docs/development/04-release-process.md`](docs/development/04-release-process.md) |
| the brand and visual identity | [`docs/brand/00-brand-kit.md`](docs/brand/00-brand-kit.md) |
