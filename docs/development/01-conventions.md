# Coding conventions

> The C# / .NET conventions enforced across the codebase. The
> `.editorconfig` is the source of truth for whitespace and naming;
> this document captures everything that can't be expressed in
> `.editorconfig`.

## 1. C# language

- **Nullable reference types are enabled.** Don't add
  `#nullable disable` to a file. If a third-party library doesn't
  have annotations, declare a `LibraryNotNullable` shim or use
  `ArgumentNullException.ThrowIfNull(x)` at the boundary.
- **`async` all the way down.** A `public` method that does I/O
  must return `Task` or `Task<T>`. Sync-over-async is a build
  error. Use `await` even inside a `Task.Run`.
- **No `void` for async methods.** `async void` is only for
  event handlers (and Radzen Blazor's lifecycle callbacks are
  rare exceptions we wrap in a try/catch).
- **`var` for built-in types and obvious types. Explicit type
  for everything else.** The `.editorconfig` already encodes
  this; the rule of thumb is "would a stranger reading this line
  in isolation know the type without a tooltip?".
- **File-scoped namespaces.** Always. No `namespace X { }` blocks.
- **`using` directives outside the namespace.** The
  `.editorconfig` enforces `csharp_using_directive_placement =
  outside_namespace:warning`.
- **Pattern matching over `is` with cast.** The `.editorconfig`
  enforces this for `is X x` patterns.
- **`record` for DTOs and value objects.** Use `record class` for
  reference-type DTOs and `record struct` for value-type VOs.
- **No public `IEnumerable<T>` parameters in domain APIs.** Use
  `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, or a concrete
  `T[]` to make the contract clear.

## 2. Naming

| Element | Convention | Example |
|---|---|---|
| Class / record / struct | PascalCase | `BoardRepository` |
| Interface | IPascalCase | `IRepository<T>` |
| Method | PascalCase | `CreateBoardAsync` |
| Public property | PascalCase | `BoardName` |
| Public field | PascalCase | `MaximumRetries` (only for public constants) |
| Private field | _camelCase | `_repository` |
| Parameter | camelCase | `boardId` |
| Local variable | camelCase | `newBoard` |
| Async methods | end in `Async` | `SaveChangesAsync` |
| Constant | PascalCase | `DefaultPageSize` |
| Static field | _camelCase (private), PascalCase (public) | `_defaultConnectionString` |
| Type parameter | TPascalCase | `TEntity` |
| Files | match the public type | `BoardRepository.cs` |

The naming rules are also enforced by the
`Cardscape.ArchitectureTests` project via Roslyn analyzers
(`IDE1006` for the underscore-private-field rule, etc.).

## 3. Project structure

- **One public type per file.** The file name matches the type
  name. Exceptions: nested types, partial classes (one file per
  partial), `Program.cs` (multiple top-level statements is OK).
- **Folder layout mirrors the namespace.** `Board.cs` lives in
  `Cardscape.Domain.Boards` and declares `namespace
  Cardscape.Domain.Boards;`.
- **No `Common` namespace abuse.** A `Common` folder is allowed
  only for genuinely cross-cutting types (primitives, errors,
  base classes). Specific concerns get specific folders.

## 4. Async / cancellation

- **Every async public method takes a `CancellationToken`.** The
  token is the last parameter. Example:
  ```csharp
  public Task<Board?> GetByIdAsync(BoardId id, CancellationToken ct = default)
  ```
- **No `ConfigureAwait(false)`** in application code. The
  application doesn't run inside an ASP.NET request that
  benefits from it (we're not a library, we don't have a
  SynchronizationContext problem).
- **Cancellation is honored.** Every `await` on a cancellable
  operation is followed by a check or uses a `ct` parameter.

## 5. EF Core

- **EF Core is the default and required persistence API.** Express reads,
  writes, set-based updates/deletes, transactions and migrations through EF
  Core whenever the provider can translate the operation. Raw SQL is allowed
  only after documenting a concrete translation/capability gap; it must be
  parameterized, isolated behind Infrastructure and covered against every
  supported provider affected by the exception.
- **Keep filtering, projection, ordering and pagination server-side.** Do not
  cross into `AsEnumerable()` / `AsAsyncEnumerable()` before those operations
  merely to work around a LINQ expression; first rewrite it using mapped domain
  values, navigations or an EF-translatable projection.
- **Always `AsNoTracking()` for read-only queries.** The only
  exception is when the caller is going to mutate the entity
  and call `SaveChangesAsync`.
- **Use `Include` / `ThenInclude` for eager loading.** Lazy
  loading is disabled by configuration in
  `CardscapeDbContext`.
- **Mapping uses Mapperly source generation, not reflection-based
  mappers.** Every context has a partial `XxxMappers` class under
  `Application/<Context>/Mapping/` annotated with
  `[Mapper]`. Hand-written DTO constructors are still preferred for
  simple shapes; reach for Mapperly only when a projection diverges
  from the entity shape. We do **not** use AutoMapper (slow startup,
  reflection-heavy) or any mapping library from Jimmy Bogard.
- **Domain events** are dispatched in the same transaction as
  the `SaveChangesAsync` call, via an interceptor:
  ```csharp
  public class DomainEventsInterceptor : SaveChangesInterceptor
  {
      public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
          DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
      {
          // collect domain events from aggregate roots,
          // dispatch them after the save succeeds.
      }
  }
  ```
- **No `EF.Functions.*` calls** without a comment explaining
  why the abstraction failed and a reference to the ADR.
- **Migrations live in three folders** (one per provider). See
  [`../AGENTS.md`](../AGENTS.md#7-migrations-incantation).

## 6. Validation

- **FluentValidation** for all user input. Validators are
  classes in `Application/<Context>/Validations/`.
- **Validators run inside Wolverine handlers.** We do not use
  MediatR pipeline behaviors (Wolverine has its own middleware
  composition via `WolverineFx.Handlers`). Each command/query
  handler that needs validation injects the matching
  `IValidator<TRequest>` and runs `validator.ValidateAsync` at the
  top of its `Handle` method.
- **Domain rules live in the entity.** A `Board` cannot be
  renamed to an empty string — that check is in the
  `Board.Rename` method, not in a validator. Validators check
  **input shape**; entities enforce **invariants**.

## 7. Errors

- **No exceptions for control flow.** Use the `Result<T>` monad
  in `Application/Common/Errors/Result.cs` for things that can
  fail in expected ways (e.g. "board not found", "invalid
  transition", "permission denied").
- **Exceptions only for unexpected failures** (DB down, network
  down, programmer error). The global exception middleware
  catches them and returns a 500 with a correlation id.
- **`Result<T>` is preferred over throwing** in command and
  query handlers. The API layer maps `Result<T>` to HTTP status
  codes via a small helper in `Api/Extensions/ResultExtensions.cs`.

## 8. Logging

- **`ILogger<T>` everywhere**, structured logging only.
- **No `Console.WriteLine`.** Anywhere.
- **Log at the right level**:
  - `Trace`/`Debug`: never in production code, useful in tests.
  - `Information`: "request started", "board created", useful
    for audit / observability.
  - `Warning`: "retry succeeded after timeout", "deprecated
    API called".
  - `Error`: an unexpected failure that we recovered from or
    that needs human attention.
  - `Critical`: data loss, security breach.

## 9. Testing

- **xUnit only.** No MSTest, no NUnit.
- **Arrange / Act / Assert** in every test, with comments if
  the intent isn't obvious.
- **`[Fact]` for one-shot, `[Theory]` + `[InlineData]` for
  parameterized.**
- **Test names are sentences:** `GetByIdAsync_WhenBoardDoesNotExist_ReturnsNull`.
- **FluentAssertions** for assertions. `_service.Should().NotBeNull()`
  over `Assert.NotNull(_service)`.
- **Moq** for mocking interfaces. **AutoFixture** for
  generating test data. We do not use NSubstitute or FakeItEasy.
- **No test touches a real database** unless it's an
  integration test with `[Trait("Database", "Sqlite")]`.
- **Provider-agnostic tests** (no `[Trait("Database", ...)]`)
  are encouraged: they test business logic, not storage.

## 10. Comments and documentation

- **XML doc comments on every public type and public member.**
  `GenerateDocumentationFile` is on in
  `Directory.Build.props`, and `TreatWarningsAsErrors` makes
  missing docs a build failure.
- **One-line summary** for the obvious, `<remarks>` for the
  subtle, `<example>` for non-trivial usage.
- **No `// TODO` without an associated issue or ADR.** If you
  must leave a TODO, format it as
  `// TODO(#1234): handle the empty list case`.
- **Comments explain "why", not "what".** The code shows
  what; the comment justifies the choice.

## 11. File headers

No file headers. License is in the `LICENSE` file at the repo
root; we don't repeat it in every source file.

## 12. Where to enforce these rules

- `.editorconfig` covers whitespace, naming, language style.
- `TreatWarningsAsErrors` in `Directory.Build.props` makes
  every Roslyn warning a build failure.
- The `Cardscape.ArchitectureTests` project has
  NetArchTest-based tests that enforce:
  - The dependency graph (Domain → Application →
    Infrastructure → Api).
  - Naming conventions.
  - "Domain doesn't reference Entity Framework".

When the rule can't be expressed in code, it lives in this
document. When the rule is wrong, the rule changes here AND in
the analyzer / test that enforces it.

## 13. References

- [Microsoft C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Microsoft Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/)
- [Steve Smith — Clean Architecture](https://github.com/ardalis/CleanArchitecture)
- [dotNetTips — C# Coding Standards](https://github.com/RealDotNetTips/dotNetTips.Utility.Core)
