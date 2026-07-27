# Changelog

All notable changes to Cardscape are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

The current development line. Items here ship with the next
patch or feature release.

---

## [v0.1.0-mvp] — 2026-07-27

Phase 1 complete. The first self-hostable, runnable build of
Cardscape. A user can register, create a workspace, and use the
full kanban surface (boards, lists, cards, comments, labels,
starred boards, due dates) through the Blazor WASM client and
the REST API, on SQLite, PostgreSQL, or MariaDB.

### Added

- **Domain layer** (`src/Cardscape.Domain/`): 9 aggregates
  (Workspace, Board, Card, List, Comment, Label, User, Activity,
  Notification), 12 strongly-typed IDs, 11 value objects with
  `Result<T>`-returning factory methods.
- **Application layer** (`src/Cardscape.Application/`): 32
  command/query handlers, `IClock` / `IUnitOfWork` /
  `ICurrentUser` / `IPasswordHasher` / `ITokenService` /
  `IStorageService` / `IEmailService` abstractions, generic +
  typed repositories.
- **Infrastructure layer** (`src/Cardscape.Infrastructure/`):
  EF Core 10 with the three providers, single migration set,
  Pbkdf2 password hasher, JWT issuance (HS256), local storage
  service, console email service, in-memory search index.
- **API layer** (`src/Cardscape.Api/`): minimal API endpoints
  for auth, workspaces, boards, lists, cards, comments, labels,
  notifications, search, activities. JWT bearer scheme.
  ProblemDetails error envelope.
- **Web layer** (`src/Cardscape.Web/`): Blazor WebAssembly SPA
  with Radzen components. Auth (login / register / logout),
  workspace and board list pages, board detail (kanban with
  add-list / add-card), card detail (with comments). CORS
  configured for the dev ports.
- **Wolverine 6.23.1** as the in-process mediator (replacing
  MediatR).
- **Mapperly 4.3.1** as the compile-time DTO mapper.
- **FluentValidation 11.11.0** for command/query validation.
- **Tests**: 179 unit tests (domain value objects + aggregates +
  application handlers) and 10 integration tests (full HTTP
  stack via `WebApplicationFactory<Program>`).
- **Docker** multi-stage build for the API. `docker compose.yml`
  for the API + Postgres stack, `docker-compose.dev.yml` for
  the SQLite-only dev experience. Healthcheck, named volumes,
  non-root user, env-driven config.
- Initial ADRs on multi-provider strategy and the MCP server.

### Changed

- API switched from MediatR to Wolverine (see
  `src/Cardscape.Application/`). The pipeline is
  `WolverineDomainEventDispatcher` + `WolverineMessageBus`
  + `IMessage`/`ICommand`/`IQuery` records.
- RowVersion concurrency check demotes no-op `Modified`
  entities to `Unchanged` so a star/complete on a freshly-loaded
  aggregate does not produce a spurious 0-row UPDATE.

### Removed

- MediatR (`MediatR.Contracts` / `MediatR` packages and the
  `IRequestHandler<,>` / `IRequest<,>` shape).
- `MediatRDomainEventDispatcher` and the `UnitOfWorkBehavior`
  / `LoggingBehavior` MediatR pipeline behaviours (replaced by
  Wolverine middleware).

### Security

- JWT signing key defaults to a clearly-marked dev-only string;
  production deployments **must** set `Jwt__SigningKey` (or the
  matching `Jwt:SigningKey` appsettings value) to a 32+ byte
  secret.
- Passwords hashed with Pbkdf2 (HMAC-SHA256, 100k iterations,
  16-byte salt).
- CORS policy is intentionally restrictive: only the dev
  ports `5206` and `7188` are allowed, with credentials.
  Production deployments are expected to terminate TLS and
  serve the API behind a same-origin reverse proxy.

---

## [Unreleased-scaffold]

The initial pre-alpha scaffold (`v0.0.0` / Phase 0). Items
below were present before `v0.1.0-mvp`.

### Added

- Solution scaffold: 6 source projects, 5 test projects, .NET 11
  preview 6 SDK, EF Core 10.0.10 LTS.
- Multi-provider persistence scaffolding (SQLite, PostgreSQL,
  MariaDB). SQLite-only test matrix for now. See
  [ADR 0001](docs/adr/0001-multi-provider-strategy.md).
- RPL-1.5 LICENSE.
- Project-local `.agents/` folder with the working contract and
  5 skills.
- `Cardscape.Mcp` project skeleton: stdio transport,
  `ApiTokenAuthenticationHandler` placeholder, `ICurrentUser`
  resolver. No tools yet. See
  [ADR 0002](docs/adr/0002-mcp-server.md).
- `docs/` set: working contract, ADRs, architecture, development
  conventions, API conventions, feature inventory, implementation
  plan, product positioning.
- Root `README.md` (the public pitch) and
  [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md)
  (name, tagline, pillars, vocabulary, voice).
- Community files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
  `SECURITY.md`, `SUPPORT.md`, this `CHANGELOG.md`.
- GitHub issue templates (bug, feature, question) and pull
  request template.
- GitHub Discussion categories (announcements, ideas, Q&A, show
  and tell).
- Public website on the `site` branch: single-page HTML + CSS,
  no build step. Deployable to GitHub Pages, Netlify, Cloudflare
  Pages, or Vercel.

### Changed

- Repository rebranded to vendor-neutral product positioning.
  Working tree and git history rewritten to drop every
  reference to the legacy competitor and its brand names.
  See commit `289a370`.

### Removed

- `docs/roadmap/00-trello-features-analysis.md` (renamed to
  `docs/roadmap/00-feature-inventory.md` and rewritten as a
  Cardscape-voice feature inventory).
- All references to vendor-specific brand names (Butler,
  Power-Ups, Atlassian Intelligence) in code, docs, and
  commit history.

### Security

- None yet. The first security audit ships with Phase 5 (Polish
  & scale).

---

## Versioning policy

Cardscape uses Semantic Versioning with pre-1.0 caveats:

- **0.y.z** — pre-1.0, "moving fast". Minor bumps can include
  breaking changes. The project's API and schema are not
  stable until 1.0.
- **1.0.0** — first stable release. API and schema are stable
  from here. Breaking changes bump the major version.

Until 1.0, the minor version denotes a phase completion (see
[`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md)).

| Version | Phase | Status |
|---|---|---|
| `v0.0.0` | (unreleased scaffold) | not used |
| `v0.1.0-mvp` | Phase 1 — MVP | target end of August 2026 |
| `v0.2.0-core-mcp` | Phase 2 — Core + MCP server | target end of October 2026 |
| `v0.3.0-extensions` | Phase 3 — Extensions & automation | target end of December 2026 |
| `v0.4.0-enterprise` | Phase 4 — Enterprise & AI | target end of Q1 2027 |
| `v1.0.0` | (Phase 5 — Polish & scale mature) | not yet targeted |

The release process (tags, NuGet, Docker, notes) is in
[`docs/development/04-release-process.md`](docs/development/04-release-process.md).

---

## Types of changes

- **Added** for new features.
- **Changed** for changes in existing functionality.
- **Deprecated** for soon-to-be-removed features.
- **Removed** for now-removed features.
- **Fixed** for any bug fixes.
- **Security** for vulnerability fixes and security-policy
  changes.
