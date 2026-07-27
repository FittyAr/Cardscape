# Cardscape

> **The self-hostable kanban your AI can drive.**
>
> Drive your boards conversationally from any AI client. Keep
> full ownership of your data.

Cardscape is an open-source, self-hostable project-management tool
with a complete feature surface — kanban boards, calendar,
automation engine, extensions, Inbox, Planner, and AI — and a
Model Context Protocol server that lets AI assistants read,
create, and move cards on your behalf. It runs on **.NET 11**,
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
- **Modern .NET, end to end.** ASP.NET Core 11, Blazor WebAssembly,
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
query. The same MediatR pipeline, the same FluentValidation
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

Cardscape is in **pre-alpha, design and Phase 0 scaffold
complete**.

| Phase | Scope | Status |
|---|---|---|
| 0 | Solution scaffold, multi-DB plumbing, RPL-1.5, AGENTS contract, MCP server project skeleton, full documentation set | **DONE** |
| 1 | MVP: single user, sign-up, workspace, board, list, card, drag and drop, sign in tomorrow | not started |
| 2 | Collaboration + real-time + **MCP server end-to-end** (the differentiator ships here) | not started |
| 3 | Extensions + automation engine | not started |
| 4 | Enterprise + AI features | not started |
| 5 | Polish + scale | ongoing |

`dotnet build` is green today. The product has no domain
entities yet — that is by design. See
[the implementation plan](docs/roadmap/01-implementation-plan.md)
for the full phased delivery schedule and
[the working contract](docs/AGENTS.md) for how work is done on
this codebase.

---

## Quickstart (today)

The application is a scaffold; there is no runnable product
yet. The honest quickstart is the developer one:

```bash
git clone https://github.com/<owner>/Cardscape.git
cd Cardscape
dotnet build                       # 11/11 projects, 0 errors, 0 warnings
dotnet test                        # 0/0 tests (no domain yet) — green by default
```

The first runnable self-hostable build (with a demo workspace,
seeded data, and `docker compose up` to go) ships with the
`v0.1.0-mvp` tag at the end of **Phase 1**.

If you want to follow along: watch the
[GitHub releases](https://github.com/<owner>/Cardscape/releases)
and the [roadmap](docs/roadmap/01-implementation-plan.md).

---

## Architecture, in one diagram

```
                 ┌────────────────────────┐
                 │     Cardscape.Web      │   Blazor WASM client
                 │   no server deps       │
                 └────────────┬───────────┘
                              │  HTTP (JSON)
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
   │   (MediatR + FV)   │          │    Storage, Email     │
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
| Runtime | .NET 11 | SDK `11.0.100-preview.6` |
| Web framework | ASP.NET Core minimal APIs | 11.0 preview 6 |
| Client | Blazor WebAssembly | 11.0 preview 6 |
| UI components | Radzen.Blazor | 11.1.8 |
| ORM | Entity Framework Core | 10.0.10 LTS (third-party providers trail .NET) |
| DB providers | Sqlite, Npgsql, MySql.EntityFrameworkCore | runtime, all switchable via config |
| Validation | FluentValidation | 11.11.0 |
| CQRS / Mediator | MediatR | 12.4.1 |
| AI integration | Model Context Protocol | .NET SDK `1.4.1`, stdio today |
| Tests | xUnit + FluentAssertions + Moq + NetArchTest | 2.9.2 / 6.12.2 / 4.20.72 / 1.3.2 |
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
