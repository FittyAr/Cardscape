# Architecture overview

> Audience: new contributors. This document explains the **shape** of
> the solution. The "why" lives in the ADRs under [`../adr/`](../adr/).

## 1. The Clean Architecture stack

Cardscape follows a Clean Architecture with **five source projects**
and **five test projects**. The dependency graph is strict and
one-directional:

```
                    ┌────────────────────────┐
                    │     Cardscape.Web      │   (Blazor WASM client)
                    │   no server deps       │
                    └────────────┬───────────┘
                                 │  HTTP (JSON)
                                 ▼
   ┌──────────────────────────────────────────────────────┐
   │                      Cardscape.Api                    │  ← presentation
   │   minimal API endpoints, JWT bearer, Swagger,         │
   │   DI composition root, provider selection             │
   └──────┬───────────────────────────────────┬────────────┘
          │                                   │
          ▼                                   ▼
   ┌────────────────────┐          ┌────────────────────────┐
   │   Application      │  ←────   │    Infrastructure     │  ← technical
   │   use cases        │          │    EF Core, Identity,  │
   │   (MediatR + FV)   │          │    Storage, Email     │
   └────────┬───────────┘          └────────────────────────┘
            │
            ▼
   ┌─────────────────────────┐
   │       Domain            │  ← pure
   │   entities, VOs,        │     (no external
   │   events, errors        │      references)
   └─────────────────────────┘
```

Key rules:

- **Domain** depends on nothing (no NuGet packages, no `using` of
  framework types beyond primitives).
- **Application** depends only on Domain. It defines the
  abstractions (`IRepository<T>`, `IUnitOfWork`, `IStorageService`,
  `IEmailService`, etc.).
- **Infrastructure** depends on Application and Domain. It provides
  the concrete implementations: `CardscapeDbContext` for EF Core,
  `AspNetIdentityService` for Identity, `LocalFileStorageService`
  for Storage, etc.
- **Api** depends on Application and Infrastructure. It composes the
  DI container and exposes HTTP endpoints.
- **Web** depends on nothing server-side. It is a Blazor WASM client
  that calls the Api over HTTP. It has its own DTOs (mirroring the
  Api's DTOs, kept in sync manually or via a future shared
  contracts project — out of scope for the MVP).

The dependency direction is enforced by the `Cardscape.ArchitectureTests`
project via NetArchTest. If a Domain class ever does
`using Microsoft.EntityFrameworkCore;`, the test fails the build.

## 2. Directory layout

```
src/
├── Cardscape.Domain/
│   ├── Boards/                  ← entity, value objects, events for one BC
│   │   ├── Board.cs
│   │   ├── Events/              ← BoardCreated.cs, BoardArchived.cs, etc.
│   │   └── ValueObjects/        ← BoardId.cs, BoardName.cs, BoardColor.cs
│   ├── Lists/                   ← column within a board
│   ├── Cards/                   ← task
│   ├── Labels/                  ← color tag
│   ├── Members/                 ← user + membership
│   ├── Comments/
│   ├── Attachments/
│   ├── Activities/              ← audit log
│   └── Common/                  ← abstractions, primitives, errors, enums
│
├── Cardscape.Application/
│   ├── Abstractions/            ← IRepository<T>, IUnitOfWork, IEmailService, ...
│   ├── Boards/                  ← bounded-context folder
│   │   ├── Commands/            ← CreateBoard.cs, RenameBoard.cs, ArchiveBoard.cs
│   │   ├── Queries/             ← GetBoardById.cs, ListBoardsForWorkspace.cs
│   │   ├── DTOs/                ← BoardDto.cs, CreateBoardRequest.cs
│   │   ├── EventHandlers/       ← reacts to BoardCreated domain events
│   │   └── Validations/         ← CreateBoardCommandValidator.cs
│   ├── Lists/  Cards/  Labels/  Members/  Authentication/
│   └── Common/                  ← Behaviors (validation, logging), Mapping, ...
│
├── Cardscape.Infrastructure/
│   ├── Persistence/             ← EF Core
│   │   ├── CardscapeDbContext.cs
│   │   ├── Configurations/      ← IEntityTypeConfiguration<T> per aggregate root
│   │   ├── Migrations/          ← one folder per provider
│   │   │   ├── Sqlite/
│   │   │   ├── PostgreSQL/
│   │   │   └── MariaDB/
│   │   ├── Repositories/        ← BoardRepository.cs, ...
│   │   ├── Interceptors/        ← AuditableEntitySaveChangesInterceptor.cs
│   │   └── Seeds/               ← initial data
│   ├── Identity/                ← ASP.NET Identity + Cardscape profile
│   ├── Storage/                 ← attachment storage abstraction
│   ├── Email/                   ← email sender
│   ├── Caching/                 ← Memory + Redis
│   ├── BackgroundJobs/          ← Hangfire
│   ├── RealTime/                ← SignalR
│   └── DependencyInjection/    ← AddInfrastructure(IConfiguration)
│
├── Cardscape.Api/
│   ├── Endpoints/               ← Boards/, Lists/, Cards/, Members/, Auth/
│   ├── Middleware/              ← exception handling, request logging
│   ├── Filters/                 ← endpoint filters
│   ├── Extensions/              ← ServiceCollectionExtensions, WebApplicationExtensions
│   ├── OpenApi/                 ← Swagger conventions
│   ├── HealthChecks/            ← liveness + readiness
│   └── Program.cs
│
└── Cardscape.Web/
    ├── Components/
    │   ├── Layout/              ← MainLayout.razor, NavMenu.razor
    │   ├── Pages/               ← route components
    │   ├── Boards/              ← domain-specific Radzen wrappers
    │   ├── Cards/
    │   └── Shared/              ← generic UI (CardView, UserAvatar, ...)
    ├── Services/
    │   ├── Api/                  ← typed HTTP clients (Refit)
    │   ├── State/                ← in-memory + browser storage
    │   └── Interop/              ← JS interop for drag-and-drop, etc.
    ├── Features/                 ← optional Fluxor / Mediator-on-client
    ├── wwwroot/                  ← static assets
    ├── _Imports.razor
    └── Program.cs
```

## 3. Bounded contexts (the vertical slices)

Cardscape's first vertical slice is `Boards`. The list grows
incrementally as the roadmap unfolds:

| Context | Owns | Notes |
|---|---|---|
| `Workspaces` | workspace, workspace member, workspace invite | The container above boards |
| `Boards` | board, board star, board background, board view | First slice; ships in Phase 1 |
| `Lists` | list (column), list position | Sorts and reorders within a board |
| `Cards` | card, card position, card mirror, card snooze | The atomic unit of work |
| `Labels` | label, label-on-card join | Reusable across the board |
| `Members` | user, member profile, presence | Identity + collaboration |
| `Checklists` | checklist, checklist item | Subtasks on a card |
| `Comments` | comment, reaction | Conversation on a card |
| `Attachments` | file attachment, link attachment | Files on a card |
| `Activities` | activity event | Append-only audit log |
| `PowerUps` | power-up definition, board-power-up join | Extension API (Phase 3) |
| `Automation` | butler rule, butler button, butler schedule | Automation engine (Phase 3) |
| `Inbox` | inbox item | Personal capture (Phase 4) |

Each context owns:

- **Domain** entity + value objects + domain events.
- **Application** commands + queries + DTOs + validators +
  event handlers.
- **Infrastructure** (when needed) repository + EF Core
  configuration.

Two contexts are allowed to reference each other **only** through:

1. Domain events raised by one and handled by the other.
2. Application-layer queries that read across contexts (e.g. a
   `BoardDetailsQuery` returns the board plus its lists plus the
   members' avatars — those reads are expressed in `Application`,
   not in `Domain`).

## 4. Cross-cutting concerns

- **Authentication / authorization** lives in `Cardscape.Api`. The
  Blazor client receives a JWT, stores it in browser storage, and
  attaches it to every request.
- **Validation** is implemented with **FluentValidation** as
  MediatR pipeline behaviors, not as data annotations on the
  domain entities. This keeps `Domain` clean.
- **Logging** is `ILogger<T>` everywhere, structured logging only,
  no `Console.WriteLine`.
- **Exception handling** is a single middleware in `Cardscape.Api`
  that maps domain errors to HTTP status codes and unexpected
  exceptions to a 500 with a correlation id.
- **Health checks** live in `Cardscape.Api/HealthChecks/` and
  expose `/health/live` (process up) and `/health/ready`
  (DB reachable + migrations applied).

## 5. Where to add a new feature

See [`../development/02-vertical-slices.md`](../development/02-vertical-slices.md)
for the recipe. The short version:

1. Add an entity in `Domain/<Context>/`.
2. Add a command (or query) in `Application/<Context>/Commands/`
   (or `Queries/`).
3. Add a handler in the same folder.
4. Add a validator in `Application/<Context>/Validations/`.
5. Add an EF Core configuration in
   `Infrastructure/Persistence/Configurations/`.
6. Add a migration in all three provider folders.
7. Add an endpoint in `Api/Endpoints/<Context>/`.
8. Add a typed client in `Web/Services/Api/`.
9. Add a component in `Web/Components/<Context>/`.
10. Add unit tests and (later) integration tests with the
    `[Trait("Database", "<Engine>")]` attribute.

## 6. What we don't have yet (and why)

- **No shared contracts project** between Api and Web. We duplicate
  DTOs intentionally until the surface stabilizes. Later, we may
  extract `Cardscape.Contracts` shared by both.
- **No MediatR on the client.** The Blazor WASM client uses typed
  `HttpClient` (Refit) to call the Api. A client-side MediatR for
  client-only events is a future possibility.
- **No gRPC / GraphQL.** REST + JSON over HTTP only. We may add
  GraphQL for read-heavy views later (boards, timelines) if the
  payload sizes make REST painful.
- **No event bus / message broker.** Domain events are dispatched
  in-process via MediatR. A real event bus (RabbitMQ, Service Bus,
  Kafka) is a Phase 5+ concern, only when we need cross-process
  communication.

## 7. References

- [`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md)
- [`../development/01-conventions.md`](../development/01-conventions.md)
- [`../development/02-vertical-slices.md`](../development/02-vertical-slices.md)
- [Microsoft — Clean Architecture with .NET](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/)
- [Jason Taylor — Clean Architecture template](https://github.com/jasontaylordev/CleanArchitecture)
