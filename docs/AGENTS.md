# Cardscape — Working contract

> This is the human-readable version of `.agents/AGENTS.md`. The
> `.agents/` copy is the operational contract an AI coding agent
> reads at runtime; this copy is the one maintainers reference
> and review.

## 1. What Cardscape is

**Cardscape is a public, open-source, self-hostable kanban and
project-management tool** with a complete feature surface
(workspaces / boards / lists / cards / calendar /
automation / extensions / Inbox / Planner / AI) and a
**Model Context Protocol (MCP) server** that lets any
AI-compatible client (Claude Desktop, Cursor, Windsurf, custom
agents, etc.) drive the boards conversationally.

The maintainer works on this project solo. The bar is **"think
big and professional"**: ADR-grade decisions, polished
documentation, an architecture that scales, a UX that competes
with hosted kanban tools, and an AI integration that no other
self-hostable kanban has. We do not cut corners. We do not
ship a "demo MVP" and call it done.

The persistence layer supports **SQLite**, **PostgreSQL**, and
**MariaDB**; the test matrix currently runs **only on SQLite**.
See [ADR 0001](adr/0001-multi-provider-strategy.md) for the
rationale and the test-trait convention we use to grow the
matrix later. See [ADR 0002](adr/0002-mcp-server.md) for the
MCP server decision.

## 2. Stack

| Layer | Choice | Version (Jul 2026) |
|---|---|---|
| Runtime | .NET | 10.0 (LTS, SDK 10.0.302) |
| Web framework | ASP.NET Core minimal APIs | 10.0.10 |
| Client | Blazor WebAssembly | 10.0.10 |
| UI components | Radzen.Blazor | 11.2.1 |
| ORM | Entity Framework Core | 10.0.10 LTS |
| DB providers (runtime) | Sqlite, Npgsql, MySql.EntityFrameworkCore | 10.0.10 / 10.0.3 / 10.0.9 |
| Validation | FluentValidation | 12.1.1 |
| CQRS / Mediator | Wolverine | 6.24.5 |
| API docs | Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore | 10.0.10 / 2.12.50 |
| AI integration | ModelContextProtocol (MCP) | latest stable (>= 0.3) |
| Tests | xUnit + FluentAssertions + Moq + NetArchTest | 2.9.2 / 6.12.2 / 4.20.72 / 1.3.2 |
| License | Reciprocal Public License 1.5 | RPL-1.5 |

## 3. Architecture

Clean Architecture, **seven source projects**, one public SDK and
**seven test suites plus TestCommon**. The dependency graph is
strict and one-directional:

```
                ┌────────────────────────┐
                │     Cardscape.Web      │   (Blazor WASM client)
                │   no server deps       │
                └────────────┬───────────┘
                             │  HTTP (JSON)
                             ▼
   ┌──────────────────────────────────────────────────────┐
   │                      Cardscape.Api                    │  ← presentation
   │   minimal API endpoints, JWT bearer, Scalar (OpenAPI), │
   │   DI composition root, provider selection             │
   └──────┬───────────────────────────────────┬────────────┘
          │                                   │
          ▼                                   ▼
   ┌────────────────────┐          ┌────────────────────────┐
   │   Application      │  ←────   │    Infrastructure     │  ← technical
   │   use cases        │          │    EF Core, Identity,  │
   │   (Wolverine + FV) │          │    Storage, Search    │
   └────────┬───────────┘          └────────────────────────┘
            ▲                                   ▲
            │                                   │
            │         ┌─────────────────────────┐
            │         │     Cardscape.Mcp       │   ← AI integration
            └─────────┤  Model Context Protocol │     (stateful Streamable HTTP)
                      │  talks to Application   │
                      └─────────────────────────┘
```

Key rules:

- **Domain** depends on nothing.
- **Application** depends only on Domain. It defines the
  abstractions (`IRepository<T>`, `IUnitOfWork`, `IStorageService`, …).
- **Infrastructure** depends on Application and Domain. It
  provides the concrete implementations.
- **Api** depends on Application and Infrastructure. Its
  build-time references to Web and Seeder exist only to host
  Blazor static assets and compose the optional seeder.
- **Web** depends on nothing server-side. It is a Blazor WASM
  client that calls the API over HTTP.
- **Mcp** depends on Application and Infrastructure. It composes
  the same persistence/application registrations as the API plus
  an `ICurrentUser` resolver
  from the API token. The MCP server is a separate process
  (stateful Streamable HTTP at `/mcp`) and shares the entire Application layer
  with the REST API.

Type dependencies and project references are enforced by the
`Cardscape.ArchitectureTests` project.

## 4. The AI integration pillar

Cardscape ships a first-class Model Context Protocol server in
`src/Cardscape.Mcp/`. It's the project's differentiator: **no
other self-hostable kanban has MCP**. With it, an AI client can:

- Read boards, lists, cards, comments, members.
- Create, update, move, archive cards.
- Add comments, assign members, add labels, set due dates.
- Search across all boards.
- Subscribe to board and card resources for live updates.

The MCP server uses authenticated **stateful Streamable HTTP** and is
**idempotent** (every
write tool accepts an `idempotencyKey` to make AI retries
safe).

See [`architecture/03-mcp-server.md`](architecture/03-mcp-server.md)
for the operational guide and [ADR 0002](adr/0002-mcp-server.md)
for the decision.

## 5. Design philosophy: design for three, test on one

> *"todo el desarrollo debe ser pensado, diseñador y programado
> pensando en los 3."*

The application is designed, implemented, and packaged to run on
three database engines — SQLite, PostgreSQL, and MariaDB. But
the automated test matrix today runs **only on SQLite**.

**Why**: the third-party EF Core providers for PostgreSQL and
MariaDB target EF Core 10 on the 10.0.x line, and the whole
solution lives on the same LTS feature band for support
uniformity. See [ADR 0001](adr/0001-multi-provider-strategy.md)
for the full rationale.

**What this means in code**:

- The runtime projects (`Cardscape.Api`, `Cardscape.Mcp`)
  reference all three provider packages and select the engine
  at boot time via `Database:Provider` configuration.
- The integration-test project (`Cardscape.IntegrationTests`)
  references only `Microsoft.EntityFrameworkCore.Sqlite`.
- Every LINQ expression, every column attribute, every
  migration body is written against the relational abstractions.
  We avoid `EF.Functions.*` provider-specific helpers, raw SQL
  strings, and per-engine JSON conventions.

**Test trait convention**:

```csharp
[Trait("Database", "Sqlite")]      // today
[Trait("Database", "PostgreSQL")]  // when the provider ships EF Core 11
[Trait("Database", "MariaDB")]     // when the provider ships EF Core 11
```

The CI command is `dotnet test --filter "Database=Sqlite"`. When
the deferred providers catch up, removing the filter and adding
the trait to the new tests is the entire migration.

## 6. Working rules for any agent

1. **Never edit `global.json` without explicit human approval.**
2. **Never bump EF Core provider versions** without verifying all
   three engines (SQLite, PostgreSQL, MariaDB) are still working.
3. **Never delete ADR files.** Mark as `Superseded by ADR NNNN`
   instead.
4. **When adding a NuGet package, declare its version in
   `Directory.Packages.props` only.**
5. **Migrations**: each EF Core provider has its own output
   directory under `src/Cardscape.Infrastructure/Persistence/Migrations/{Provider}`.
6. **Don't touch the `.gitignore` for `obj/`, `bin/`, `.vs/`, etc.**
7. **No provider-specific code paths without a comment**
   explaining why the abstraction failed and pointing at the ADR.
8. **No new dependencies without a justification line** in the
   commit message and a row in the `Directory.Packages.props`
   changelog.
9. **MCP tool changes are a contract.** Renaming a tool, changing
   its parameters, or changing its return type is a breaking
   change. Bump the `Cardscape.Mcp` major version.
10. **Document as you go.** Every architectural decision gets an
    ADR. Every new feature gets a docs entry. Every contract
    (REST endpoint, MCP tool, domain event) is documented
    inline with XML doc comments.

## 7. Migrations incantation

```bash
# SQLite
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/Sqlite

# PostgreSQL
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/PostgreSQL

# MariaDB
dotnet ef migrations add <Name> \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/MariaDB
```

Always run all three. The first migration is hand-diffed before
merge to catch the cases where the abstraction is too thin.

## 8. Available agent skills (project-local)

The skills in `.agents/skills/` are loaded by the agent on demand:

| Skill | Use when |
|---|---|
| `authoring-github-workflows` | editing anything under `.github/workflows/` |
| `create-custom-agent` | creating a new VS Code `.agent.md` file |
| `create-skill` | adding a new skill to `.agents/skills/` |
| `create-skill-test` | running skill eval tests |
| `radzen-blazor` | implementing UI in `src/Cardscape.Web/` |

See `.agents/skills/radzen-blazor/SKILL.md` before touching any
UI. For MCP work, follow the recipe in
[`architecture/03-mcp-server.md`](architecture/03-mcp-server.md).

## 9. Onboarding checklist for a new agent

1. Read this file.
2. Read [`roadmap/00-feature-inventory.md`](roadmap/00-feature-inventory.md)
   — what we are building.
3. Read [`roadmap/01-implementation-plan.md`](roadmap/01-implementation-plan.md)
   — what comes next.
4. Read [`architecture/00-overview.md`](architecture/00-overview.md)
   — the shape of the code.
5. Read [`architecture/03-mcp-server.md`](architecture/03-mcp-server.md)
   — the AI integration surface.
6. Read every entry in `docs/adr/`.
7. Run `dotnet build` and confirm the baseline is green before
   changing anything.
8. Run `dotnet test` (currently tests pass with the SQLite-only
   matrix).
9. Check `.agents/skills/radzen-blazor/SKILL.md` before any UI
   work.
