# Error handling

> The project's convention for representing, propagating, and
> presenting errors. The pattern is `Result<T>` for the
> Application layer, `ProblemDetails` for the API surface, and
> a single exception-handling boundary in the API host.
>
> This is a **design** document. The code lands in Phase 1.

---

## 1. The principle

Errors are **values, not exceptions** in the Application
layer. An operation that can fail returns a `Result<T>`, not
throws an exception. Exceptions are reserved for the
**unexpected** — bugs, infrastructure failures, programmer
errors.

Why:

- **The compiler enforces error handling.** A `Result<T>`
  forces the caller to handle the failure case; a thrown
  exception is invisible to the type system.
- **The error is data.** A `Result<T>` carries the error
  code, the message, and any metadata. An exception carries
  a stack trace, which the caller cannot use.
- **The HTTP layer translates cleanly.** A `Result.Failure`
  maps to an HTTP `ProblemDetails`; a thrown exception maps
  to a 500.

---

## 2. The `Result<T>` type

```csharp
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(T value) { ... }
    private Result(Error error) { ... }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
}

public readonly struct Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    // Non-generic version for void operations.
}
```

The `Error` type:

```csharp
public sealed record Error(
    string Code,        // e.g. "card.not_found"
    string Message,     // human-readable
    ErrorKind Kind,     // validation, not_found, conflict, forbidden, etc.
    IReadOnlyDictionary<string, object?>? Metadata = null);

public enum ErrorKind
{
    Validation,
    NotFound,
    Forbidden,
    Unauthorized,
    Conflict,
    RateLimited,
    DependencyFailure,  // downstream service is down
    Internal,           // bug; should not happen in normal operation
}
```

The `Code` is a stable, dotted identifier the API exposes
to clients. Clients are expected to switch on the code, not
parse the message. The message is human-readable and may be
localized in a future i18n pass.

---

## 3. The pattern in handlers

```csharp
public sealed class CreateCardHandler : IRequestHandler<CreateCardCommand, Result<CardId>>
{
    public async Task<Result<CardId>> Handle(
        CreateCardCommand command, CancellationToken ct)
    {
        var board = await _boards.GetByIdAsync(command.BoardId, ct);
        if (board is null)
            return Result<CardId>.Failure(new Error(
                "board.not_found",
                $"Board {command.BoardId} does not exist.",
                ErrorKind.NotFound));

        // ... domain logic ...

        return Result<CardId>.Success(card.Id);
    }
}
```

The caller:

```csharp
var result = await _mediator.Send(new CreateCardCommand(...));
if (result.IsFailure)
{
    return ToHttpResult(result.Error);
}
// Use result.Value
```

No try/catch in the handler for expected errors. No try/catch
in the API for the same.

---

## 4. Exception handling boundary

There is **one** exception handler in the API host. It is
the boundary between "the system worked but the operation
failed" (Result) and "the system did not work as expected"
(exception).

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var (status, problem) = ex switch
        {
            ValidationException vex    => (400, ToProblem(vex)),
            NotFoundException nex      => (404, ToProblem(nex)),
            ForbiddenException fex     => (403, ToProblem(fex)),
            DbUpdateConcurrencyException => (409, ToProblem(...)),
            OperationCanceledException => (499, ToProblem(...)),  // client cancelled
            _                          => (500, ToProblem(ex)),   // log as Error
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});
```

Unrecognized exceptions are **500 Internal Server Error** and
are logged at `Error` level with the full stack trace and the
correlation ID. They are never leaked to the client beyond
the `traceId` field (which the client can send back when
filing a bug).

---

## 5. The `ProblemDetails` response

The API returns `application/problem+json` per RFC 7807. The
shape:

```json
{
  "type": "https://cardscape.fitty.ar/errors/board.not_found",
  "title": "Board not found",
  "status": 404,
  "detail": "Board 3f9c0a1b-... does not exist.",
  "instance": "/api/v1/boards/3f9c0a1b-.../cards",
  "code": "board.not_found",
  "traceId": "01HZ8X9R2P7K...",
  "errors": {
    "listId": ["must be a positive integer"]
  }
}
```

- `type` — a URL where the error is documented. Stable.
- `title` — a short, human-readable summary. Not localized
  today; can be localized in a future i18n pass.
- `status` — the HTTP status code. Mirrored in the response
  status line.
- `detail` — a longer, human-readable description. Not
  localized.
- `instance` — the URI of the request that failed.
- `code` — the same stable `Code` from the `Error` record.
  Clients can switch on this.
- `traceId` — the W3C trace context `traceparent`. Lets the
  client reference the failing request when filing a bug.
- `errors` — a per-field map of validation errors. Only
  present on `Validation` (400) responses.

The `type` URL is a future home for per-error documentation
("what does `board.not_found` mean and how do I recover?").
The path prefix `https://cardscape.fitty.ar/errors/` is
reserved for this. The URLs are stable; if the docs site
moves, the path stays.

---

## 6. The error code catalogue

The codes are stable. Clients depend on them. Adding a code
is non-breaking; renaming or removing a code is breaking
(requires a major version bump).

| Code | HTTP | When |
|---|---|---|
| `validation.failed` | 400 | One or more fields failed validation |
| `validation.required` | 400 | A required field was missing |
| `validation.out_of_range` | 400 | A numeric value was out of range |
| `unauthorized.missing` | 401 | No credentials presented |
| `unauthorized.invalid` | 401 | Credentials are invalid |
| `forbidden.workspace` | 403 | The user is not in the workspace |
| `forbidden.board` | 403 | The user cannot see the board |
| `forbidden.card` | 403 | The user cannot see the card |
| `forbidden.scope` | 403 | The API token does not have the required scope |
| `not_found.board` | 404 | The board does not exist (or the user cannot see it) |
| `not_found.list` | 404 | Same for list |
| `not_found.card` | 404 | Same for card |
| `not_found.user` | 404 | Same for user |
| `not_found.workspace` | 404 | Same for workspace |
| `conflict.duplicate_email` | 409 | A user with this email already exists |
| `conflict.duplicate_slug` | 409 | A workspace with this slug already exists |
| `conflict.version_mismatch` | 409 | Optimistic concurrency check failed |
| `conflict.card_archived` | 409 | Operation is invalid on an archived card |
| `ratelimited.too_many_requests` | 429 | The user or token exceeded the rate limit |
| `dependency.downstream_unavailable` | 503 | An external service is unavailable |
| `internal.unexpected` | 500 | An unexpected exception; check `traceId` |

The catalogue is the **single source of truth** for the API
error surface. It is generated from the code (or vice
versa). New codes require updating both the catalogue and
the tests.

---

## 7. Logging strategy

| Error kind | Log level | Log content |
|---|---|---|
| `Validation`, `NotFound`, `Forbidden`, `Unauthorized`, `Conflict` | `Info` | the request, the error code, the user (if known) — no PII |
| `RateLimited` | `Warning` | the request, the rate-limit bucket, the user |
| `DependencyFailure` | `Error` | the request, the downstream service, the exception type (no body) |
| `Internal` (unhandled) | `Error` | the request, the full exception with stack trace, the `traceId` |

We log **the error code**, not the message. The message
may contain user input (titles, descriptions); logging it at
`Info` would leak user content into the log stream.

We log **the request path and method**, not the body. Bodies
may contain PII, secrets, or large blobs. Path + method is
enough to correlate with a client bug report.

We log **the correlation / trace ID** on every line. The
`traceId` field of the `ProblemDetails` response is the
client's handle to the log entry.

---

## 8. The MCP server

The MCP server maps `Result<T>` to MCP tool results the same
way the REST API maps to HTTP. The MCP protocol's "tool
result" has an `isError` flag; we set it to `true` and put
the `ProblemDetails` JSON in the `content` field. The MCP
client (Claude Desktop, Cursor, etc.) renders the error in
its UI; the AI agent can switch on the `code` field to
decide what to do next.

The MCP server's exception handling boundary is the same as
the REST API's: one handler, mapping exceptions to MCP
errors. The exception is logged with the MCP request ID
(which becomes the `traceId` in the error response).

---

## 9. Anti-patterns (do not do this)

- **Throwing exceptions for expected errors.** A
  `NotFoundException` is not an exception; it is a value.
  Use `Result.Failure`.
- **Catching `Exception` in handlers.** The handler does
  not need to know about exceptions; the boundary catches
  them.
- **Returning a 200 with a JSON body that says "error".**
  Always use the HTTP status code that fits the error.
- **Logging the user-visible message.** The message may
  contain user content. Log the code.
- **Using a single "Something went wrong" error for
  everything.** The `code` is the API. Without it, clients
  cannot react intelligently.
- **Returning 500 for validation errors.** Validation
  failures are 400, not 500. A 500 implies the system is
  broken; a 400 implies the user did something wrong.

---

## 10. Migration path

The pattern lands in Phase 1. Before it lands, the existing
codebase (none yet) is empty, so there is nothing to migrate.
The pattern is enforced from the first commit of the first
handler.

If a future change requires a new error code, the change
adds the code to the catalogue in §6 and the test, in the
same PR.
