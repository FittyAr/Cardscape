# ADR 0008: Clean Architecture, "lite" — deliberate deviations from the textbook shape

- **Status**: Accepted
- **Date**: 2026-07-29
- **Deciders**: Cardscape maintainers

## Context

Cardscape follows Clean Architecture as described in
`docs/architecture/00-overview.md`. The textbook
shape is:

- `Cardscape.Domain` — entities, value objects,
  domain events, aggregate roots. Zero dependencies
  on any other layer.
- `Cardscape.Application` — use cases (commands,
  queries, handlers), abstractions (`IRepository`,
  `IEmailService`), DTOs. Depends only on `Domain`.
- `Cardscape.Infrastructure` — EF Core, repositories,
  email, storage, search. Depends on `Application`
  and `Domain`. Implements the `Application`
  abstractions.
- `Cardscape.Api` — minimal-API endpoints, auth,
  middleware, OpenAPI. Depends on `Application`,
  `Infrastructure`, `Domain`.
- `Cardscape.Web` — Blazor WASM client. Depends on
  nothing in this repo (talks to `Api` over HTTP).
- `Cardscape.Mcp` — MCP server. Depends on
  `Application`, `Infrastructure`, `Domain`.

The textbook has a `Cardscape.Contracts` project
(shared DTOs between the API and the Blazor client)
and a separate `Cardscape.Application.Abstractions`
project (interfaces live alone). The maintainer
considered both, and decided against them.

## Decision

Cardscape uses Clean Architecture **without** the
`Contracts` and the standalone `Abstractions`
projects. The deliberate deviations:

1. **No `Cardscape.Contracts` project.** DTOs are
   duplicated between `Cardscape.Application` and
   `Cardscape.Web.Shared` (`ApiDtos.cs` in the Web
   client). The duplication is intentional: the Web
   client does not take a project reference to
   `Application` or `Api`, so a contract change is a
   conscious edit in both places. The cost of the
   duplication is a small set of record types
   (~80 DTOs in the Web client) that are easy to
   keep in sync with the API. The benefit is that
   the Web client is a single, self-contained
   Blazor WASM payload with no project reference
   to the server-side stack.

2. **Abstractions live alongside the use cases.**
   `Cardscape.Application/Abstractions/Persistence/`,
   `Cardscape.Application/Abstractions/Security/`,
   etc. The interfaces are *Application* concepts;
   the implementations are *Infrastructure*
   details. Splitting the interfaces into a
   separate project would add a `Cardscape.Application.Abstractions`
   assembly, an extra `using` at every call site,
   and a separate NuGet versioning axis for what is
   conceptually one layer.

3. **One migration set, three providers.** The
   `cardscape.slnx` ships a single
   `src/Cardscape.Infrastructure/Persistence/Migrations/`
   set that runs on SQLite, PostgreSQL, and
   MariaDB. The original Phase 0 plan called for
   three separate migration folders, hand-synced.
   In practice the relational abstractions in EF
   Core 10 cover every column type and index shape
   Cardscape needs; the per-provider override is
   reserved for genuinely provider-specific cases
   (full-text search, JSON column type) and is
   documented in
   [`0001-multi-provider-strategy.md`](0001-multi-provider-strategy.md).

4. **MCP is a peer of the API, not a child.** The
   `Cardscape.Mcp` project is its own SDK-style
   project with a `Program.cs`, an `IConfiguration`
   binding, and its own dependency-injection
   composition root. It does not depend on
   `Cardscape.Api`. The maintainer can deploy the
   MCP server without standing up the API (useful
   for offline-AI-tool development) and the API
   can be deployed without the MCP (useful for
   non-AI self-hosters).

5. **The Web client is a Blazor WASM, not a
   server-rendered Razor page.** The maintainer
   considered a server-rendered Blazor app
   (running in the API process) for the simpler
   "edit one file, refresh" loop. The WASM split
   is the right answer for a long-lived project:
   the client is a real client, the API is a real
   API, the boundary is HTTP, the same
   discipline applies as if the Web client were
   React on a different machine.

## Consequences

Positive:

- **Six source projects, five test projects — the
  textbook shape with the textbook number of
  components.** No "thin" projects that exist for
  symmetry but ship zero lines of code.
- **The Web client is portable.** A future
  refactor that swaps Blazor for a different
  client (React, Svelte, native) only touches
  `Cardscape.Web.Shared` and the typed API
  clients; the API is unchanged.
- **MCP is portable.** A future second consumer
  (a CLI, a desktop app, a Slack integration) can
  share the MCP server's `Application`-layer
  consumption without taking a dependency on
  the API.
- **Migrations are easy to read.** One folder,
  ordered by timestamp, every change visible in
  one place. Per-provider overrides live in the
  migration body where they're used.

Negative / accepted:

- **DTOs are duplicated.** A breaking change to
  the API's `BoardDto` requires a matching edit
  in `Cardscape.Web.Shared`. The maintainer
  mitigates this with a "DTO regression"
  integration test that boots the API and the Web
  in the same process and asserts every DTO
  the Web references is still serialisable.
- **The `Abstractions` namespace inside
  `Application` is visible from `Application`
  callers.** This is a coupling concern in
  the textbook; in practice the
  `IApplicationService` boundary is already
  enforced by the dependency graph (Domain has
  no Application reference; Infrastructure
  implements every Application abstraction;
  Api only injects them).

## When to revisit

This ADR should be revisited when **any** of the
following is true:

1. The DTO duplication cost (the number of fields
   that drift between the API and the Web) crosses
   a maintenance threshold the maintainer finds
   unsustainable. The fix is a new
   `Cardscape.Contracts` project the Web can
   reference; the cost is the Web has to ship two
   assemblies (the WASM payload and the contracts
   DLL).
2. The MCP server grows a second consumer that
   needs the same set of tools — at that point the
   `Cardscape.Mcp` project is fine, and the API
   stays as-is.

## References

- `docs/architecture/00-overview.md` — the
  six-project shape
- `docs/architecture/01-bounded-contexts.md` —
  the contexts and their boundaries
- `docs/development/01-conventions.md` — the
  DTO duplication convention
- [`ADR 0001`](0001-multi-provider-strategy.md) —
  the migration strategy
- `src/Cardscape.Web/Shared/ApiDtos.cs` — the
  duplicated DTOs
