# MCP server deep dive

> The operational guide for working on the MCP server
> (`src/Cardscape.Mcp/`). The companion to
> [`docs/architecture/03-mcp-server.md`](../architecture/03-mcp-server.md),
> which is the high-level design; this doc is the
> "how do I add a new tool" recipe.
>
> This is a **contributor** document. It is read by
> developers working on the MCP server, not by users of
> the MCP server.

---

## 1. The MCP server in one paragraph

The MCP server is an ASP.NET Core minimal API that
exposes the project's `Application` layer over the Model
Context Protocol. The same `MediatR` pipeline, the same
`FluentValidation` rules, and the same `Result<T>` that
the REST API uses are exposed as MCP tools, resources, and
prompts. The MCP server is a **thin transport layer**; the
domain logic lives in `Cardscape.Application`.

The MCP server is **transport-agnostic**. It supports
**stdio** (the default for local AI clients like Claude
Desktop) and **HTTP+SSE** (for hosted AI clients). The
transport is selected at startup via the
`Cardscape__Mcp__Transport` configuration value.

---

## 2. The project structure

```
src/Cardscape.Mcp/
├── Authentication/
│   ├── ApiTokenAuthenticationHandler.cs
│   ├── ApiTokenAuthenticationOptions.cs
│   ├── ICurrentUserResolver.cs
│   └── McpCurrentUserResolver.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── Tools/
│   ├── CardsTools.cs
│   ├── BoardsTools.cs
│   ├── WorkspacesTools.cs
│   ├── CommentsTools.cs
│   └── ...
├── Resources/
│   ├── BoardResource.cs
│   ├── CardResource.cs
│   └── ...
├── Prompts/
│   ├── StandupSummaryPrompt.cs
│   ├── TriageInboxPrompt.cs
│   └── ...
├── Program.cs
└── Cardscape.Mcp.csproj
```

The structure is by **MCP concept** (tools, resources,
prompts), not by bounded context. A single tool file may
call into multiple bounded contexts (e.g. the
`cards_create` tool calls into the `Cards` and the
`Boards` contexts).

---

## 3. Adding a new tool

The recipe. Each step has a "what" and a "where".

### 3.1 Step 1: define the tool's interface

The tool is a C# method on a class that is registered with
the MCP server. The method's parameters are the tool's
parameters; the return type is the tool's result.

```csharp
// src/Cardscape.Mcp/Tools/CardsTools.cs

[McpServerToolType]
public sealed class CardsTools
{
    [McpServerTool(
        Name = "cards_create",
        Description = "Create a new card on a board. " +
                      "Returns the new card's id.")]
    public async Task<Result<CardDto>> CreateCard(
        [McpServerToolParameter(
            Name = "list_id",
            Description = "The id of the list the card is created in.",
            Required = true)]
        Guid listId,

        [McpServerToolParameter(
            Name = "title",
            Description = "The card's title (1-512 characters).",
            Required = true)]
        string title,

        // ... other parameters ...
        CancellationToken ct = default)
    {
        // Implementation
    }
}
```

The attributes are part of the `ModelContextProtocol`
SDK. The `[McpServerToolType]` attribute marks the class
as a tool container; the `[McpServerTool]` attribute marks
the method as a tool. The parameters are described with
`[McpServerToolParameter]`.

### 3.2 Step 2: implement the tool

The tool's implementation calls into the `Application`
layer. The tool does **not** contain domain logic; the
tool is a thin adapter.

```csharp
public async Task<Result<CardDto>> CreateCard(
    Guid listId,
    string title,
    string? description = null,
    DateTimeOffset? dueDate = null,
    IReadOnlyList<Guid>? labelIds = null,
    IReadOnlyList<Guid>? assigneeIds = null,
    CancellationToken ct = default)
{
    var user = _currentUser.UserId;
    var command = new CreateCardCommand(
        listId, title, description, dueDate, labelIds, assigneeIds);
    var result = await _mediator.Send(command, ct);

    if (result.IsFailure)
    {
        return result.Error;  // Result<CardDto>.Failure(error)
    }

    var card = await _cards.GetByIdAsync(result.Value, ct);
    return Result<CardDto>.Success(card.ToDto());
}
```

The `_mediator` is injected via the constructor. The
`_currentUser` is the `ICurrentUser` for the request (see
[`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md)
§8). The `_cards` is a repository injected via the
constructor.

### 3.3 Step 3: register the tool

The tool class is registered in the DI container in
`ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<CardsTools>();
```

The MCP SDK discovers the tool via the
`[McpServerToolType]` attribute; no manual registration
of the tool method is needed.

### 3.4 Step 4: test the tool

Every tool has a test. The test:

- Mocks the `IMediator` to return a known `Result<T>`.
- Mocks the `ICurrentUser` to return a known user.
- Calls the tool method directly (not through the MCP
  transport).
- Asserts the result matches the expected `Result<T>`.

```csharp
[Fact]
public async Task CreateCard_ValidInput_ReturnsSuccess()
{
    // Arrange
    var mediator = new Mock<IMediator>();
    mediator.Setup(m => m.Send(It.IsAny<CreateCardCommand>(),
        It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<CardId>.Success(new CardId(Guid.NewGuid())));
    var user = new Mock<ICurrentUser>();
    user.Setup(u => u.Id).Returns(new UserId(Guid.NewGuid()));
    var cards = new Mock<ICardRepository>();
    cards.Setup(c => c.GetByIdAsync(It.IsAny<CardId>(),
        It.IsAny<CancellationToken>()))
        .ReturnsAsync(new Card(...));
    var tools = new CardsTools(mediator.Object, user.Object, cards.Object);

    // Act
    var result = await tools.CreateCard(
        listId: Guid.NewGuid(), title: "Test card");

    // Assert
    Assert.True(result.IsSuccess);
}
```

### 3.5 Step 5: test the tool over MCP

The test in §3.4 tests the tool's C# method. A separate
test exercises the tool over the MCP transport (stdio or
HTTP+SSE). The MCP SDK provides a test client that can
connect to the MCP server in-process and call the tools
as a real AI client would.

```csharp
[Fact]
public async Task CreateCard_OverStdio_ReturnsSuccess()
{
    // Arrange
    var server = await McpServerFixture.StartAsync();
    var client = await McpClient.CreateAsync(
        server.StdioTransport,
        new McpClientOptions { ClientInfo = new("test", "1.0") });

    // Act
    var result = await client.CallToolAsync(
        "cards_create",
        new Dictionary<string, object?>
        {
            ["list_id"] = Guid.NewGuid().ToString(),
            ["title"] = "Test card"
        });

    // Assert
    Assert.NotNull(result);
    Assert.False(result.IsError);
}
```

The `McpServerFixture` brings up the MCP server in-process
for the test. The fixture is shared across the MCP tests
for performance.

### 3.6 Step 6: document the tool

The tool is documented in
[`02-prompt-library.md`](02-prompt-library.md) and in the
API conventions doc (`docs/api/00-conventions.md`, added
with Phase 1+). The documentation includes:

- The tool's name and description.
- The tool's parameters (name, type, required, description).
- The tool's return type and the error codes it can return.
- An example call and an example response.
- The scope the tool requires (e.g. `cards:write`).

A pull request that adds a tool without documentation is
rejected in review.

---

## 4. Adding a new resource

A resource is an addressable piece of data that the AI
client can subscribe to. The pattern is similar to a tool
but read-only and addressable by URI.

```csharp
[McpServerResourceType]
public sealed class CardResource
{
    [McpServerResource(
        Name = "card",
        UriTemplate = "card://{cardId}",
        Description = "A card's full details.")]
    public async Task<ResourceContents> GetCard(
        Uri uri,
        CancellationToken ct = default)
    {
        var cardId = ExtractCardId(uri);
        var card = await _cards.GetByIdAsync(cardId, ct);
        if (card is null) throw new NotFoundException(...);
        return new ResourceContents
        {
            Uri = uri,
            MimeType = "application/json",
            Text = JsonSerializer.Serialize(card.ToDto())
        };
    }
}
```

The resource is registered the same way as a tool. The
test pattern is the same. The documentation pattern is
the same.

---

## 5. Adding a new prompt

A prompt is a templated instruction the user can run. The
prompt is a C# method that returns a string (the
rendered prompt) given a set of parameters.

```csharp
[McpServerPromptType]
public sealed class TriageInboxPrompt
{
    [McpServerPrompt(
        Name = "triage-inbox",
        Description = "Help me triage the cards in the Inbox list.")]
    public async Task<string> Render(
        [McpServerPromptParameter(
            Name = "max_cards",
            Description = "The maximum number of cards to triage. Default: 20.")]
        int maxCards = 20,
        CancellationToken ct = default)
    {
        var user = _currentUser.UserId;
        var inbox = await _inbox.GetForUserAsync(user, maxCards, ct);
        var prompt = $"""
            You are helping me triage my Inbox. Here are the
            {inbox.Count} most recent cards:

            {string.Join("\n", inbox.Select(c => $"- [{c.Id}] {c.Title}: {c.Description}"))}

            For each card, suggest one of:
            - **Move to board**: which board and list?
            - **Schedule**: when should I work on it?
            - **Snooze**: until when should I hide it?
            - **Archive**: it's not relevant.

            Output a table with the card id, the suggested action,
            and a one-line justification.
            """;
        return prompt;
    }
}
```

The prompt is registered the same way as a tool. The
prompt's test renders the prompt and asserts the rendered
output is correct.

---

## 6. The MCP server's dependencies

The MCP server depends on:

- `Cardscape.Application` (the use cases).
- `Cardscape.Domain` (the shared error types).
- `Cardscape.Infrastructure` (the EF Core repositories).
- `ModelContextProtocol` (the .NET MCP SDK, version 1.4.1
  or later).

The MCP server does **not** depend on `Cardscape.Api` or
`Cardscape.Web`. The dependency direction is strict.

The MCP server's `appsettings.json` has the same
configuration keys as the API (the same `Database`,
`Cardscape`, `Otel`, `Smtp` sections). The MCP server
adds the `Cardscape__Mcp__Transport` key to select the
transport.

---

## 7. The MCP server's local development

In local development:

```bash
# Start the API (the MCP server shares the Application
# layer; the API is a separate process for the REST API).
dotnet run --project src/Cardscape.Api

# In another terminal, start the MCP server with stdio.
dotnet run --project src/Cardscape.Mcp -- --mcp-transport=stdio
```

The `--mcp-transport=stdio` flag tells the MCP server to
listen on stdio instead of HTTP+SSE. Claude Desktop uses
stdio; the test harness uses stdio or HTTP+SSE depending
on the test.

The local dev setup is the same as the production setup,
minus the reverse proxy and the monitoring.

---

## 8. The MCP server's deployment

The MCP server is deployed as a separate container (or
the same container as the API, depending on the
deployment). The container is the same image; the entry
point is the MCP server instead of the API.

The Docker image: `ghcr.io/cardscape/cardscape-mcp:0.2.0-core-mcp`.

The Claude Desktop configuration:

```json
{
  "mcpServers": {
    "cardscape": {
      "command": "docker",
      "args": [
        "run", "--rm", "-i",
        "-e", "Cardscape__ApiBaseUrl=https://cardscape.example.com",
        "-e", "Cardscape__ApiToken=<the user's API token>",
        "ghcr.io/cardscape/cardscape-mcp:0.2.0-core-mcp"
      ]
    }
  }
}
```

The user's API token is created in the web UI (Settings →
API tokens). The token is shown once, at creation time,
and never again. The token is sent in the
`Authorization: Bearer <token>` header by the MCP client.

---

## 9. The MCP server's observability

The MCP server's observability follows the conventions in
[`docs/design/02-logging-observability.md`](../design/02-logging-observability.md).
Every tool call is:

- A span (`cardscape.mcp.tool`).
- A metric (`cardscape.mcp.tool.duration` and
  `cardscape.mcp.tool.invocations`).
- A log line at `Info` (success) or `Warning` (handled
  error) or `Error` (unhandled exception).

The trace context is propagated to the `Application`
layer; an end-to-end trace covers the MCP call → handler
→ repository → DB.

The MCP server's dashboard is in
[`docs/operations/03-monitoring.md`](../operations/03-monitoring.md)
§3.2.

---

## 10. The MCP server's error handling

The MCP server's error handling follows the conventions in
[`docs/design/01-error-handling.md`](../design/01-error-handling.md).
A tool that returns a `Result.Failure(error)` is mapped
to an MCP error response:

```json
{
  "isError": true,
  "content": [
    {
      "type": "text",
      "text": "{... the ProblemDetails JSON ...}"
    }
  ]
}
```

The AI client can switch on the `code` field of the
`ProblemDetails` to decide what to do next.

The MCP server's unhandled exceptions are caught by the
single exception-handling boundary (the same boundary the
REST API uses). The exception is logged at `Error` level
and returned to the AI client as a 500-level MCP error.

---

## 11. The MCP server's authentication

The MCP server authenticates with **API tokens** (see
[`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md)
§7). The token is a high-entropy secret, base64url-encoded,
sent in the `Authorization: Bearer <secret>` header. The
secret is hashed with PBKDF2 (Phase 1) or Argon2id
(Phase 4) and looked up in the `ApiToken` entity.

The call-tool request filter checks the token's exact `read` or
`write` grant against the closed tool catalog before invoking the
tool. Neither scope implies the other. Missing grants return
`mcp.scope.forbidden`; tools missing from the catalog are denied
with `mcp.scope.unclassified` until explicitly reviewed.

The MCP server does **not** support cookie auth or JWT
auth. The AI client is not a browser; the bearer token is
the right auth method for an AI client.

---

## 12. The MCP server's testing

The MCP server has three layers of tests:

1. **Unit tests** for the tool methods (the C# methods
   directly, with mocked dependencies). See §3.4.
2. **Integration tests** for the tool methods over the MCP
   transport (in-process MCP server, real MCP client). See
   §3.5.
3. **End-to-end smoke test** with a real AI client
   (Claude Desktop or a mock) and a seeded workspace. See
   [`02-prompt-library.md`](02-prompt-library.md) §6.

All three are run in the CI. A tool that fails any of the
three is not merged.

---

## 13. The MCP server's versioning

The MCP server is versioned **separately from the REST
API** (per [ADR 0002](../adr/0002-mcp-server.md) §10). The
MCP protocol has its own version (`2025-06-18` as of this
writing); the MCP server is compatible with one or more
MCP protocol versions.

A breaking change to the MCP server's tool surface (a
renamed tool, a changed parameter, a changed return type)
bumps the MCP server's major version. The REST API's
version is unaffected.

The version is in the assembly's `Version` field (set in
the `.csproj`). The NuGet package version mirrors the
assembly version.

---

## 14. When to revisit

This document is revisited when:

1. A new MCP SDK version is released (the SDK may add new
   attributes or change the tool registration pattern).
2. A new tool type is added (e.g. a sampling tool, an
   elicitation tool).
3. A new transport is added (e.g. gRPC, WebSocket).
4. A real contributor lands a tool and reveals a gap in
   the recipe.

Until then, this document is the source of truth for
working on the MCP server in Cardscape.
