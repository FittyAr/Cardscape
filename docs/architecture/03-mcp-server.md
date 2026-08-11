# MCP server (Model Context Protocol)

> Companion to [ADR 0002](../adr/0002-mcp-server.md). Read the
> ADR first; this is the **operational guide** for the
> `Cardscape.Mcp` project: layout, transport, authentication,
> tools, resources, prompts, and the recipe for adding a new
> tool.

The MCP server is the project's **AI integration surface**. It
lets any MCP-compatible client (Claude Desktop, Cursor, Windsurf,
Continue, JetBrains AI, custom agents, …) drive Cardscape
conversationally: read boards, create cards, move cards, add
comments, etc.

## 1. The project

```
src/Cardscape.Mcp/
├── Cardscape.Mcp.csproj
├── Program.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs   ← AddCardscapeMcp(...)
├── Authentication/
│   ├── ApiTokenAuthenticationHandler.cs
│   └── ICurrentUserResolver.cs
├── Tools/
│   ├── BoardsTool.cs                   ← list_workspaces, list_boards, get_board
│   ├── CardsTool.cs                    ← list_cards, get_card, create_card, move_card, update_card
│   ├── CommentsTool.cs                 ← add_comment
│   ├── MembersTool.cs                  ← assign_card
│   ├── SearchTool.cs                    ← search
│   └── IdempotencyToolFilter.cs        ← per-request IdempotencyKey handling
├── Resources/
│   ├── BoardResource.cs                ← board://{boardId}
│   ├── CardResource.cs                 ← card://{cardId}
│   └── WorkspaceResource.cs            ← workspace://{workspaceId}
├── Prompts/
│   ├── StandupSummaryPrompt.cs
│   ├── TriageInboxPrompt.cs
│   └── SprintPlanningPrompt.cs
└── Logging/
    └── McpActivityLogger.cs            ← structured logs per tool call
```

The project is a standard ASP.NET Core minimal API
(`Microsoft.NET.Sdk.Web`). The MCP surface is implemented with
the `ModelContextProtocol` NuGet package (the official
[.NET SDK for MCP](https://github.com/modelcontextprotocol/csharp-sdk)).

## 2. Dependency direction

```
Cardscape.Mcp  →  Cardscape.Application  →  Cardscape.Domain
            ↘                          ↗
              Cardscape.Domain (errors, common types)
```

- `Cardscape.Mcp` references `Cardscape.Application` (for
  Wolverine handlers, validators, and DTOs) and `Cardscape.Domain`
  (for shared error types and primitives).
- `Cardscape.Mcp` does **not** reference `Cardscape.Api`,
  `Cardscape.Infrastructure`, or `Cardscape.Web`.

This means:

- The MCP server and the REST API are **independent deployables**.
  Either can run without the other.
- The MCP server does **not** touch the database directly. It
  uses repositories and `IUnitOfWork` from the Application
  abstractions; the concrete `CardscapeDbContext` lives in
  `Cardscape.Infrastructure`, which the MCP server composes via
  `AddCardscapeInfrastructure` (the same method the API uses).
- The MCP server reuses every existing handler. Adding a new
  use case to the Application layer automatically makes it
  available to the MCP server.

## 3. Composition root

`Program.cs` is a thin shell:

```csharp
using Cardscape.Api.Extensions;
using Cardscape.Mcp.Extensions;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardscapeApplication();
builder.Services.AddCardscapeInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddCardscapeMcp(builder.Configuration);

var app = builder.Build();

app.UseCardscapeMcp();           // maps /mcp/sse and /mcp/messages
app.MapCardscapeHealthChecks();  // /health/live and /health/ready

app.Run();
```

The interesting bits live in `AddCardscapeMcp`:

```csharp
public static IServiceCollection AddCardscapeMcp(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddAuthentication(ApiTokenAuthenticationHandler.SchemeName)
            .AddScheme<ApiTokenAuthenticationOptions,
                       ApiTokenAuthenticationHandler>(
                ApiTokenAuthenticationHandler.SchemeName,
                _ => { });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("CardsMcp", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
                !ctx.Resource?.ToString()?.Contains("readonly") ?? true);
        });
    });

    services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new McpServerInfo
            {
                Name = "Cardscape",
                Version = "1.0.0"
            };
        })
        .WithHttpTransport()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
        .WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)
        .WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

    return services;
}
```

`WithToolsFromAssembly` discovers every class annotated with
`[McpTool]` in the `Cardscape.Mcp` assembly and registers it
automatically. The same applies to resources and prompts.

## 4. Transport

### 4.1 stdio (default for local clients)

For local clients (Claude Desktop, etc.), the MCP server is
launched as a child process by the client. The client's
configuration looks like:

```json
{
  "mcpServers": {
    "cardscape": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\Cardscape\\src\\Cardscape.Mcp",
        "--stdio"
      ],
      "env": {
        "CARDS_API_TOKEN": "sk_...",
        "Database__Provider": "Sqlite",
        "Database__SqliteConnectionString": "Data Source=/home/me/.local/share/cardscape/cardscape.db"
      }
    }
  }
}
```

The MCP SDK's `WithStdioServerTransport()` wires up stdin /
stdout for the protocol. The `--stdio` argument tells our
`Program.cs` to suppress the HTTP listener and only listen on
stdio.

### 4.2 HTTP + SSE (hosted deployments)

For hosted deployments (a server reachable by an AI client over
the network), the MCP server exposes:

- `POST /mcp/messages` — receive a JSON-RPC message.
- `GET /mcp/sse` — Server-Sent Events stream for server-pushed
  messages.

Authentication: `Authorization: Bearer <api_token>` header on
both endpoints. The token is verified by
`ApiTokenAuthenticationHandler`.

CORS: not needed. The MCP client is not a browser.

## 5. Authentication: API tokens

A new entity, `ApiToken`, lives in the `Members` bounded context:

```csharp
public sealed class ApiToken : AggregateRoot<ApiTokenId>
{
    public UserId OwnerId { get; }
    public string Name { get; }                   // human label
    public string HashedSecret { get; }           // bcrypt of the secret
    public string SecretPrefix { get; }           // first 8 chars, for display
    public ApiTokenScopes Scopes { get; }         // [Flags] enum: boards:read, boards:write, ...
    public DateTime? ExpiresAt { get; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime CreatedAt { get; }
    public bool IsRevoked { get; private set; }
}
```

The token format is `sk_<32 random base62 chars>`. The full
secret is shown **once** at creation, then hashed (bcrypt) and
only the prefix is kept in cleartext for display.

Scopes (a `[Flags]` enum):

```csharp
[Flags]
public enum ApiTokenScopes
{
    None = 0,
    BoardsRead = 1 << 0,
    BoardsWrite = 1 << 1,
    CardsRead = 1 << 2,
    CardsWrite = 1 << 3,
    CommentsWrite = 1 << 4,
    Search = 1 << 5,
    All = BoardsRead | BoardsWrite | CardsRead | CardsWrite |
          CommentsWrite | Search
}
```

The user creates API tokens from a "Developer settings" page in
the Web client. The card-back-of-the-token is the secret, shown
once.

`ApiTokenAuthenticationHandler` is a standard
`AuthenticationHandler<ApiTokenAuthenticationOptions>` that:

1. Reads the `Authorization: Bearer <secret>` header.
2. Looks up the token by its bcrypt hash.
3. Verifies the secret against the stored hash.
4. Checks the token is not revoked and not expired.
5. Stamps `LastUsedAt` (fire-and-forget).
6. Sets the `ClaimsPrincipal` with the `UserId` claim and the
   `ApiTokenScopes` claim.

The handler is **constant-time** for the bcrypt comparison.

## 6. Current user

The Application layer's handlers read the current user via
`ICurrentUser`. Both hosts reuse Application's `CurrentUser`
mapping and own only the transport adapter that supplies the
authenticated principal. MCP registers
`McpHttpContextCurrentUserAccessor` as `ICurrentUserAccessor`:

```csharp
public sealed class McpHttpContextCurrentUserAccessor(
    IHttpContextAccessor accessor) : ICurrentUserAccessor
{
    public ClaimsPrincipal? GetCurrentPrincipal() =>
        accessor.HttpContext?.User;
}
```

The shared `CurrentUser` turns that principal into the same
`ICurrentUser` contract used by REST. Handlers do not know
whether they were invoked from an API endpoint or an MCP tool.

## 7. Tools (the read/write surface for AI)

A tool is a method on a class annotated with `[McpTool]`. The
SDK discovers them via `WithToolsFromAssembly`.

Example:

```csharp
[McpTool("cards_create", "Create a new card on a list.")]
public sealed class CardsTool(ISender sender)
{
    [McpMethod]
    public async Task<CardDto> CreateCardAsync(
        [McpParameter("listId", "The id of the list to add the card to.")]
        Guid listId,
        [McpParameter("title", "The card title.")]
        string title,
        [McpParameter("description", "Optional description.")]
        string? description = null,
        [McpParameter("dueDate", "Optional due date (ISO 8601).")]
        DateTime? dueDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new CreateCardCommand(
            new ListId(listId),
            CardTitle.Create(title),
            description is null ? null : CardDescription.Create(description),
            dueDate),
            ct);

        if (result.IsFailure)
            throw new McpToolException(result.Error.Code, result.Error.Message);

        return result.Value!;
    }
}
```

Every tool follows these rules:

- One tool class per domain context (`BoardsTool`, `CardsTool`,
  `CommentsTool`, etc.).
- One method per tool. The method's name is irrelevant; the
  attribute is the contract.
- The method is `async Task<T>` (or `Task<T>` if not async).
- The return type is a DTO. Errors are surfaced as
  `McpToolException` so the AI client gets a clear error
  message.
- Parameters are typed: `Guid` for IDs, `string` for text,
  `DateTime?` for optional dates. The SDK handles JSON
  serialization.
- A `CancellationToken` parameter is always last; the SDK
  propagates it.

### 7.1 Initial tools

| Tool | Scope | Description |
|---|---|---|
| `workspaces_list` | `BoardsRead` | Return the caller's workspaces. |
| `boards_list` | `BoardsRead` | List boards in a workspace, optionally filtered. |
| `boards_get` | `BoardsRead` | Get a board with its lists. |
| `cards_list` | `CardsRead` | List cards in a board, with optional filters (list, label, assignee, due date, text). |
| `cards_get` | `CardsRead` | Get a card with full details. |
| `cards_create` | `CardsWrite` | Create a new card. |
| `cards_update` | `CardsWrite` | Update an existing card (partial). |
| `cards_move` | `CardsWrite` | Move a card to a list, optionally at a position. |
| `cards_archive` | `CardsWrite` | Archive a card. |
| `comments_add` | `CommentsWrite` | Add a comment to a card. |
| `members_assign` | `BoardsWrite` | Assign / unassign a member to / from a card. |
| `search` | `Search` | Full-text search across the caller's boards. |
| `labels_add` | `CardsWrite` | Add / remove a label on a card. |
| `checklist_add` | `CardsWrite` | Add a checklist item to a card. |

### 7.2 Idempotency

Write tools (`cards_create`, `cards_update`, `cards_move`,
`cards_archive`, `comments_add`, `members_assign`,
`labels_add`, `checklist_add`) accept an optional
`idempotencyKey` parameter. When provided:

- The same key on the same tool, with the same payload,
  produces the same effect (no duplicate side-effects) for a
  configurable retention window (default 24 hours).
- The key is stored in a new `IdempotencyKey` entity in
  `Application`. The MCP server consults the store before
  invoking the handler; if the key is found, it returns the
  cached response.

This makes the MCP surface safe for AI agents that retry on
errors (which they do).

## 8. Resources (addressable data)

A resource is a class annotated with `[McpResource]`. The SDK
discovers them via `WithResourcesFromAssembly`.

```csharp
[McpResource("board://{boardId}", "A board with its lists, labels, and members.")]
public sealed class BoardResource(ISender sender)
{
    [McpMethod]
    public async Task<BoardDetailsDto> GetAsync(Guid boardId, CancellationToken ct = default)
        => (await sender.Send(new GetBoardByIdQuery(new BoardId(boardId)), ct)).Value!;
}
```

A client can:

- Read `board://<board-id>` to get the current state of a board.
- Subscribe to changes (the MCP SDK supports resource
  subscriptions; we wire it up to the `Activities` stream so
  the AI client sees updates in real time).

### 8.1 Initial resources

| URI | Scope | Description |
|---|---|---|
| `board://{boardId}` | `BoardsRead` | Board JSON. |
| `card://{cardId}` | `CardsRead` | Card JSON. |
| `workspace://{workspaceId}` | `BoardsRead` | Workspace JSON. |
| `cards://board/{boardId}` | `CardsRead` | List of cards in a board. |
| `activities://board/{boardId}` | `BoardsRead` | Recent activity on a board. |

## 9. Prompts (templated instructions)

A prompt is a method that returns a templated message. Useful
for "let the user invoke a structured workflow":

```csharp
[McpPrompt("standup-summary", "Generate a standup summary from your cards.")]
public sealed class StandupSummaryPrompt
{
    [McpMethod]
    public McpPromptResult Build()
    {
        return new McpPromptResult(new[]
        {
            new McpPromptMessage(McpRole.User,
                "Look at all the cards assigned to me in the last 24 hours, " +
                "across all my workspaces. For each one, summarize what I did, " +
                "what's blocked, and what I plan to do next. Format as a daily " +
                "standup.")
        });
    }
}
```

### 9.1 Initial prompts

| Prompt | Description |
|---|---|
| `standup-summary` | Daily standup from the caller's cards. |
| `triage-inbox` | Help triage the cards in the Inbox list. |
| `sprint-planning` | Plan the next sprint from a Backlog list. |
| `weekly-review` | Summarize the last week of activity. |
| `stale-cards` | Find cards that haven't moved in N days. |

## 10. Observability

The MCP server emits structured logs and OpenTelemetry traces
for every tool call. The trace context is propagated to the
`Application` layer.

Log fields per tool call:

- `mcp.tool.name` — the tool name (e.g. `cards_create`).
- `mcp.tool.idempotency_key` — present if the caller provided
  one.
- `mcp.user.id` — the caller's `UserId`.
- `mcp.token.id` — the `ApiToken.Id` (not the secret).
- `mcp.request.duration_ms` — total time spent in the tool.
- `mcp.request.result` — `success`, `idempotent_replay`, or
  `error`.
- `mcp.error.code` and `mcp.error.message` — present on error.

The metrics:

- `mcp.tool.calls.total` — counter, tagged by tool name and
  result.
- `mcp.tool.duration.seconds` — histogram, tagged by tool name.

## 11. Versioning

The MCP server is versioned separately from the REST API. The
MCP protocol has its own version (`2025-06-18` at the time of
this writing). We track MCP SDK upgrades and bump our
`Cardscape.Mcp` major version when the SDK has a breaking
change.

The tool surface is a **contract** with the AI client. Once
documented, renaming a tool, changing its parameters, or
changing its return type is a breaking change. We follow
semver strictly: a breaking change in a tool bumps the
`Cardscape.Mcp` major version.

## 12. Adding a new tool

1. Open the appropriate `Tools/<Context>Tool.cs` (e.g.
   `Tools/BoardsTool.cs` for board-level tools).
2. Add a new method:
   ```csharp
   [McpMethod]
   public async Task<ReturnType> MyNewToolAsync(
       [McpParameter("name", "Description of the parameter.")]
       Type paramName,
       CancellationToken ct = default)
   {
       var result = await _sender.Send(new MyCommand(...), ct);
       if (result.IsFailure)
           throw new McpToolException(result.Error.Code, result.Error.Message);
       return result.Value!;
   }
   ```
3. The SDK discovers the tool automatically on next start.
4. Add unit tests in
   `tests/Cardscape.UnitTests/Application/<Context>/` (the
   handler tests cover the business logic; the tool is a
   transport layer).
5. Add an integration test in
   `tests/Cardscape.IntegrationTests/Mcp/` that boots the MCP
   server, authenticates with a test API token, invokes the
   tool, and asserts on the response.

## 13. Testing

- **Unit tests** for the underlying handlers (in
  `Cardscape.UnitTests`). The MCP tool is a one-liner that
  wraps the handler; if the handler is correct, the tool is
  correct.
- **Integration tests** in `Cardscape.IntegrationTests/Mcp/`
  that boot the MCP server in-process, use a test API token,
  and call the tool via the SDK's in-process client. SQLite
  only, tagged with `[Trait("Database", "Sqlite")]`.
- **Manual / smoke tests** with Claude Desktop in the local dev
  environment. We document the manual test scenarios in
  `docs/development/00-onboarding.md` (the "MCP smoke test"
  section).

## 14. Security considerations

- The MCP server is a trusted process. Anyone with shell access
  to the machine can launch it.
- The HTTP transport is authenticated by API token. The
  `Authorization: Bearer` header is required on every request.
- We do not expose the MCP server over the public internet
  without a reverse proxy that enforces rate limiting and IP
  allow-listing. The default deployment is **local-only**:
  the user runs the MCP server on their workstation, talks to
  Claude Desktop on the same machine.
- API tokens are stored as bcrypt hashes. The cleartext secret
  is shown **once** at creation and never recoverable.
- The MCP server does not log API token secrets. The structured
  log includes only the token's prefix and id, never the
  secret.
- Every write tool supports `IdempotencyKey`, which prevents
  AI-agent retries from causing duplicate side-effects.

## 15. References

- [ADR 0002](../adr/0002-mcp-server.md) — the decision.
- [Model Context Protocol — official spec](https://modelcontextprotocol.io/)
- [ModelContextProtocol — .NET SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [`../development/02-vertical-slices.md`](../development/02-vertical-slices.md)
  — the recipe for adding a new feature in the Application
  layer, which is also the recipe for adding a new MCP tool.
- [`../roadmap/01-implementation-plan.md`](../roadmap/01-implementation-plan.md)
  — the MCP work sits in Phase 2.
