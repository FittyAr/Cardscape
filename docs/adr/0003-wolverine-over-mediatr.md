# ADR 0003: Wolverine over MediatR for the command/query bus

- **Status**: Accepted
- **Date**: 2026-07-29
- **Deciders**: Cardscape maintainers

## Context

Cardscape follows Clean Architecture and uses a command/query
separation pattern: every state change is a `record` command
that flows through a handler, which talks to repositories
defined in `Cardscape.Application` and executes against the
EF Core `DbContext` in `Cardscape.Infrastructure`.

The choice of bus implementation is a long-term one — every
feature in the project routes through it. We need:

1. **Compile-time handler discovery** — handlers are
   methods on classes, the bus wires them automatically. No
   runtime reflection on every dispatch.
2. **Strongly-typed pipeline** — pipeline behaviours
   (validation, logging, idempotency) compose with the
   handler without each handler manually invoking them.
3. **Source-generator support** — no per-dispatch IL emit,
   no allocation on the hot path.
4. **`.NET 11` compatibility** — works on the current SDK
   without ceremony.
5. **A mediator + a service bus in one package** — the same
   library can dispatch in-process (`IMessageBus.InvokeAsync`)
   and queue for later (background jobs in Phase 5). The
   project will need both; paying for two libraries is a
   tax we'd rather avoid.

The two realistic options in mid-2026:

| Option | Pros | Cons |
|---|---|---|
| **MediatR** (`mediatr` 12.x) | The canonical choice. Most documentation, most Stack Overflow answers. | Closed source since 2024 (commercial license required for teams > $1M revenue). Source-generator was experimental in 2024 and still has gaps. |
| **Wolverine** (`wolverine` 6.x) | Open source (MIT). Source-generator first-class. Built-in handler discovery, pipeline behaviours, transactional inbox/outbox, scheduled jobs. Single library covers mediator + bus. | Smaller ecosystem; "less Stack Overflow" risk. |

## Decision

We use **Wolverine 6.23.1** as the in-process bus for
every command and query in `Cardscape.Application`. The
DI extension method `AddCardscapeApplication()` registers
the Wolverine handlers with the message bus, and the API
endpoints invoke them with
`bus.InvokeAsync<Result<TResponse>>(command, ct)`.

The Wolverine handlers live in the same `Application`
namespace as the commands and queries (e.g.
`Cardscape.Application.Cards.Commands.CreateCardCommandHandler`).
The pattern is **handler = class with one `Handle` method**,
which the source generator wires up.

## Consequences

Positive:

- **One library** for the in-process bus, the background
  job dispatch (Phase 5), the transactional inbox (Phase
  5), and the deferred-message scheduling. MediatR +
  Hangfire would be two libraries, two extension points,
  two dependency-update cycles.
- **Source-generated** dispatch — the cost of
  `InvokeAsync<TResponse>` is the same as a direct method
  call (after JIT). MediatR uses reflection on the hot
  path in the absence of source generation.
- **Open source, MIT-licensed** — no commercial-license
  risk if Cardscape is adopted by a larger organisation.
- **Wolverine pipeline behaviours** are easy to compose
  (e.g. `ValidationBehavior<TRequest, TResponse>` runs
  FluentValidation rules before the handler is invoked).
  We use this today for validation; we use it for
  idempotency in `v1.1.0-roadmap-execution`.

Negative / accepted:

- **Smaller community.** A new contributor is more likely
  to recognise MediatR at first sight. The Wolverine docs
  are good (https://wolverinefx.net/) but the public body
  of Stack Overflow answers is thinner. The maintainer
  documents the conventions in `docs/development/01-conventions.md`
  and the working rules in `docs/AGENTS.md`.
- **Wolverine versions move fast.** Major versions have
  shipped breaking changes historically. We pin the version
  in `Directory.Packages.props` and update on a quarterly
  cadence; we don't ride the latest preview.
- **No canonical "Wolverine + EF Core" recipe.** The
  combination is well-supported but requires the
  maintainer to wire the `IDbContext` envelope handler
  (one line in `Program.cs`). This is documented.

## When to revisit

This ADR should be revisited when **any** of the following
is true:

1. Wolverine ships a major version that changes the
   source-generator output (every ~12-18 months; the
   project upgrades on a schedule, not on a whim).
2. The MediatR licence is relicensed to a permissive open
   source licence (currently it's commercial for
   >$1M-revenue orgs) **and** the source-generator story
   becomes a first-class citizen.
3. A third contender appears with materially better
   performance or a smaller API surface.

## References

- [Wolverine — official docs](https://wolverinefx.net/)
- [Wolverine on GitHub](https://github.com/JasperFx/wolverine)
- [MediatR licence change announcement (2024)](https://github.com/jbogard/MediatR/blob/master/LICENSE.md)
- `Directory.Packages.props` — `WolverineFx 6.23.1`,
  `WolverineFx.RuntimeCompilation 6.23.1`
- `docs/development/01-conventions.md` — the working
  conventions for Wolverine handlers
