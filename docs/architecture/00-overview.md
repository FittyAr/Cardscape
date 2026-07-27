# Architecture overview

> Audience: new contributors. This document explains the **shape** of
> the solution. The "why" lives in the ADRs under [`../adr/`](../adr/).

## 1. The Clean Architecture stack

Cardscape follows a Clean Architecture with **seven source
projects** and **five test projects**. The dependency graph is
strict and one-directional:

```
                    ┌────────────────────────┐
                    │     Cardscape.Web      │   (Blazor WASM client)
                    │   no server deps       │
                    └────────────┬───────────┘
                                 │  HTTP (JSON)
                                 ▼
   ┌──────────────────────────────────────────────────────┐
   │                      Cardscape.Api                    │  ← public REST
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
            ▲                                   ▲
            │                                   │
            │         ┌─────────────────────────┐
            │         │     Cardscape.Mcp       │   ← AI integration
            └─────────┤  Model Context Protocol │     (stdio or HTTP+SSE)
                      │  talks to Application   │
                      └─────────────────────────┘
```

Key rules:

- **Domain** depends on nothing (no NuGet packages, no `using` of
  framework types beyond primitives).
- **Application** depends only on Domain. It defines the
  abstractions (`IRepository<T>`, `IUnitOfWork`, `IStorageService`,
  `IEmailService`, etc.).
- **Infrastructure** depends on Application and Domain. It
  provides the concrete implementations: `CardscapeDbContext`
  for EF Core, `AspNetIdentityService` for Identity, etc.
- **Api** depends on Application and Infrastructure. It composes
  the DI container and exposes HTTP endpoints.
- **Mcp** depends on Application and Domain. It composes the
  same DI container as the API, plus an `ICurrentUser` resolver
  from the API token, and exposes the MCP server. It is
  **independent** of the REST API: a deployment can run the
  MCP server without exposing the REST API, and vice versa.
- **Web** depends on nothing server-side. It is a Blazor WASM
  client that calls the API over HTTP.

The dependency direction is enforced by the
`Cardscape.ArchitectureTests` project via NetArchTest.

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
│   ├── Members/                 ← user + membership + api token
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
├── Cardscape.Mcp/                          ← ★ AI integration
│   ├── Tools/                              ← McpTool classes per BC
│   │   ├── BoardsTool.cs                   ← list_workspaces, list_boards, get_board
│   │   ├── CardsTool.cs                    ← list_cards, get_card, create_card, move_card, ...
│   │   ├── CommentsTool.cs                 ← add_comment
│   │   ├── MembersTool.cs                  ← assign_card
│   │   └── SearchTool.cs                   ← search
│   ├── Resources/                          ← McpResource classes
│   │   ├── BoardResource.cs                ← board://{boardId}
│   │   ├── CardResource.cs                 ← card://{cardId}
│   │   └── WorkspaceResource.cs            ← workspace://{workspaceId}
│   ├── Prompts/                            ← templated user instructions
│   ├── Authentication/                    ← ApiTokenAuthenticationHandler
│   ├── Extensions/                        ← AddCardscapeMcp
│   └── Program.cs
│
└── Cardscape.Web/
    ├── Components/
    │   ├── Layout/              ← MainLayout.razor, NavMenu.razor
    │   ├── Pages/               ← route components
    │   ├── Boards/              ← domain-specific Radzen wrappers
    │   ├── Cards/
    │   ├── Settings/            ← profile, API tokens, theme
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
| `Members` | user, member profile, presence, **API token** | Identity + collaboration + AI auth |
| `Checklists` | checklist, checklist item | Subtasks on a card |
| `Comments` | comment, reaction | Conversation on a card |
| `Attachments` | file attachment, link attachment | Files on a card |
| `Activities` | activity event | Append-only audit log |
| `Notifications` | in-app + email subscription | Phase 1 |
| `Search` | full-text index | Phase 1 |
| `Extensions` | extension definition, board-extension join | Extension API (Phase 3) |
| `Automation` | rule, button, schedule | Automation engine (Phase 3) |
| `Integrations` | webhook, OAuth app, third-party mapping | Phase 3 |
| `Inbox` | inbox item | Personal capture (Phase 2) |
| `Planner` | scheduled reminder, calendar event | Phase 2 |
| `AI` | AI request log, AI suggestion | Phase 4 (Cardscape AI) |
| `MCP` | (thin) tool / resource / prompt dispatch | Phase 2 (this ADR's pillar) |
| `Admin` | org-wide settings, SSO, audit log | Phase 4 |

See [`01-bounded-contexts.md`](01-bounded-contexts.md) for the
full catalog and the context map.

## 4. Cross-cutting concerns

- **Authentication / authorization** lives in `Cardscape.Api`
  (cookie-based JWT for the web client) and `Cardscape.Mcp`
  (API-token-based for the AI client). Both share the same
  `ICurrentUser` abstraction in the Application layer.
- **Validation** is implemented with **FluentValidation** as
  MediatR pipeline behaviors.
- **Logging** is `ILogger<T>` everywhere, structured logging only.
- **Exception handling** is a single middleware in `Cardscape.Api`
  and a single filter in `Cardscape.Mcp` that maps domain
  errors to HTTP/MCP status codes.
- **Health checks** live in each deployable project
  (`Cardscape.Api/HealthChecks/`, `Cardscape.Mcp/HealthChecks/`)
  and expose `/health/live` and `/health/ready`.

## 5. The AI integration pillar

The MCP server in `src/Cardscape.Mcp/` is Cardscape's
**differentiator** for the open-source release. It is a thin
transport layer on top of the Application layer:

- Every MCP tool is a one-line wrapper around an existing
  command or query handler.
- The Application layer doesn't know whether it's being
  called from the REST API, the MCP server, or a future
  third client (CLI, PowerShell module, etc.).
- Idempotency keys make AI retries safe.
- The same `Result<T>` error-handling shape is used; the MCP
  SDK maps `Result.Failure` to `McpToolException`.

See [`03-mcp-server.md`](03-mcp-server.md) and
[ADR 0002](../adr/0002-mcp-server.md) for the design and the
decision.

## 6. Where to add a new feature

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
7. **For the REST surface**: add an endpoint in
   `Api/Endpoints/<Context>/`.
8. **For the AI surface**: add a tool method in
   `Mcp/Tools/<Context>Tool.cs`. The body is one
   `await _sender.Send(new MyCommand(...))`.
9. Add a typed client in `Web/Services/Api/`.
10. Add a component in `Web/Components/<Context>/`.
11. Add unit tests, integration tests (with the right trait),
    and (if it's a write operation) a happy-path MCP smoke
    test.

## 7. What we don't have yet (and why)

- **No shared contracts project** between Api and Web. We
  duplicate DTOs intentionally until the surface stabilizes.
- **No MediatR on the client.** The Blazor WASM client uses
  typed `HttpClient` (Refit) to call the API.
- **No gRPC / GraphQL.** REST + JSON over HTTP only.
- **No event bus / message broker.** Domain events are
  dispatched in-process via MediatR.
- **No MCP subscription support yet** (Phase 2 deliverable).
  Resources are read-only for now; live subscriptions are
  scheduled for Phase 5 (RealTime) when SignalR is wired in.

## 8. References

- [`../adr/0001-multi-provider-strategy.md`](../adr/0001-multi-provider-strategy.md)
- [`../adr/0002-mcp-server.md`](../adr/0002-mcp-server.md)
- [`01-bounded-contexts.md`](01-bounded-contexts.md)
- [`03-mcp-server.md`](03-mcp-server.md)
- [`../development/01-conventions.md`](../development/01-conventions.md)
- [`../development/02-vertical-slices.md`](../development/02-vertical-slices.md)
- [Microsoft — Clean Architecture with .NET](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/)
- [Model Context Protocol — official spec](https://modelcontextprotocol.io/)
