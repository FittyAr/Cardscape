# How to add a feature (vertical slice recipe)

> A feature in Cardscape is added as a **vertical slice**: a single
> change that touches every layer from the entity to the UI. This
> document walks through the recipe step by step, using "rename a
> board" as a running example.

## 1. The shape of a vertical slice

A feature touches **at most** these folders:

```
src/
├── Cardscape.Domain/Boards/                          (1) entity + VOs
├── Cardscape.Application/Boards/
│   ├── Commands/RenameBoardCommand.cs               (2) command
│   ├── Commands/RenameBoardCommandHandler.cs        (3) handler
│   └── Validations/RenameBoardCommandValidator.cs   (4) validator
├── Cardscape.Infrastructure/Persistence/             (5) EF Core mapping
│   └── Configurations/BoardConfiguration.cs
├── Cardscape.Api/Endpoints/Boards/                   (6) HTTP endpoint
│   └── RenameBoardEndpoint.cs
├── Cardscape.Web/Services/Api/                       (7) typed client
│   └── BoardsApiClient.cs
├── Cardscape.Web/Components/Boards/                  (8) Blazor component
│   └── BoardRenameDialog.razor
└── tests/
    ├── Cardscape.UnitTests/Application/Boards/      (9) unit tests
    │   └── RenameBoardCommandHandlerTests.cs
    ├── Cardscape.IntegrationTests/Api/Boards/       (10) integration test
    │   └── RenameBoardEndpointTests.cs
    └── Cardscape.ArchitectureTests/                 (already covers naming/dependencies)
```

A new feature is **one commit** that touches every relevant folder
plus the migration when the schema changes.

## 2. Step-by-step recipe

### Step 1 — Domain: entity or value object

If the feature introduces a new concept, add it in
`Domain/<Context>/`. If it's a behavior on an existing entity,
open the existing file.

```csharp
// src/Cardscape.Domain/Boards/Board.cs
public sealed class Board : AggregateRoot<BoardId>
{
    public BoardName Name { get; private set; } = null!;
    public BoardDescription? Description { get; private set; }

    public Result Rename(BoardName newName)
    {
        if (newName == Name) return Result.Success();

        Name = newName;
        AddDomainEvent(new BoardRenamed(Id, newName, DateTime.UtcNow));
        return Result.Success();
    }
}
```

- Entities are `sealed` unless polymorphism is required.
- Properties are `private set` and mutated only through methods.
- The method returns `Result` (not throws).
- Every state change raises a domain event.

### Step 2 — Application: command

```csharp
// src/Cardscape.Application/Boards/Commands/RenameBoardCommand.cs
public sealed record RenameBoardCommand(
    Guid BoardId,
    string NewName
) : IRequest<Result<BoardDto>>;
```

- Commands are `record` types implementing `IRequest<TResponse>`.
- The response is `Result<T>` (never `T` directly, because
  failure is always a possibility).
- The command name is in the **imperative** ("RenameBoard", not
  "BoardRename").

### Step 3 — Application: handler

```csharp
// src/Cardscape.Application/Boards/Commands/RenameBoardCommandHandler.cs
public sealed class RenameBoardCommandHandler(
    IBoardRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<RenameBoardCommand, Result<BoardDto>>
{
    public async Task<Result<BoardDto>> Handle(
        RenameBoardCommand request,
        CancellationToken ct)
    {
        var board = await repository.GetByIdAsync(new BoardId(request.BoardId), ct);
        if (board is null)
            return Result.Failure<BoardDto>(BoardErrors.NotFound);

        var newName = BoardName.Create(request.NewName);
        var renameResult = board.Rename(newName);
        if (renameResult.IsFailure)
            return Result.Failure<BoardDto>(renameResult.Error);

        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(board.ToDto());
    }
}
```

- Handlers are `sealed` classes.
- Constructor injection of dependencies. Primary constructors
  are encouraged.
- One public method (`Handle`).
- Returns `Result<T>`. No exceptions for control flow.
- Tests are easy because the handler is a regular class.

### Step 4 — Application: validator

```csharp
// src/Cardscape.Application/Boards/Validations/RenameBoardCommandValidator.cs
public sealed class RenameBoardCommandValidator
    : AbstractValidator<RenameBoardCommand>
{
    public RenameBoardCommandValidator()
    {
        RuleFor(x => x.BoardId)
            .NotEmpty().WithMessage("BoardId is required.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("New name is required.")
            .MaximumLength(BoardName.MaxLength)
            .WithMessage($"New name must be at most {BoardName.MaxLength} characters.");
    }
}
```

- The validator is a regular FluentValidation class registered
  in DI and invoked by the corresponding Wolverine handler.
- Validators check **input shape** (length, format, presence).
  Domain rules (e.g. "you can't rename an archived board") live
  in the entity method.

### Step 5 — Infrastructure: EF Core configuration

If the entity already has a configuration, no change. If the
feature changes the schema, update the configuration and add a
migration.

```csharp
// src/Cardscape.Infrastructure/Persistence/Configurations/BoardConfiguration.cs
public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> b)
    {
        b.ToTable("boards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new BoardId(v));
        b.Property(x => x.Name).HasMaxLength(BoardName.MaxLength).IsRequired();
        b.Property(x => x.Description).HasMaxLength(BoardDescription.MaxLength);
        b.HasIndex(x => x.WorkspaceId);
    }
}
```

- Strongly-typed IDs are persisted as their underlying value via
  value converters.
- Indexes are declared explicitly. EF Core does not infer them.

Then add a migration in all three provider folders:

```bash
dotnet ef migrations add RenameBoard \
  --project src/Cardscape.Infrastructure \
  --startup-project src/Cardscape.Api \
  --output-dir Persistence/Migrations/Sqlite
# (repeat for PostgreSQL and MariaDB)
```

Hand-diff the three files. If the schema change is simple
(renaming a column), they should be identical. If it's
complex, write a per-provider override in the `Up` / `Down`
methods and reference the ADR.

### Step 6 — API: endpoint

```csharp
// src/Cardscape.Api/Endpoints/Boards/RenameBoardEndpoint.cs
public static class RenameBoardEndpoint
{
    public static IEndpointRouteBuilder MapRenameBoard(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/boards/{boardId:guid}/name", RenameBoardAsync)
           .WithName("RenameBoard")
           .WithTags("Boards")
           .RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RenameBoardAsync(
        Guid boardId,
        [FromBody] RenameBoardRequest body,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new RenameBoardCommand(boardId, body.NewName), ct);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Problem(result.Error);
    }
}
```

- Endpoints are extension methods on `IEndpointRouteBuilder`.
- One file per endpoint, with the request DTO declared inside
  the file.
- HTTP status codes are returned via the `Result` → `IResult`
  mapping in `Api/Extensions/ResultExtensions.cs`.
- Authentication is enforced via `.RequireAuthorization()`. The
  authorization policy itself lives in
  `Api/Extensions/AuthorizationExtensions.cs`.

Register the endpoint in `Program.cs`:

```csharp
app.MapBoardsEndpoints();
```

(`MapBoardsEndpoints` aggregates all the board endpoint
extensions, defined in `Api/Endpoints/Boards/BoardsEndpoints.cs`.)

### Step 7 — Web: typed API client

```csharp
// src/Cardscape.Web/Services/Api/BoardsApiClient.cs
public sealed class BoardsApiClient(HttpClient http)
{
    public async Task<BoardDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await http.GetFromJsonAsync<BoardDto>($"/api/boards/{id}", ct);

    public async Task<BoardDto?> RenameAsync(Guid id, string newName, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync(
            $"/api/boards/{id}/name",
            new { newName },
            ct);
        return await response.Content.ReadFromJsonAsync<BoardDto>(ct);
    }
}
```

- Use `HttpClient` directly. We do not pull in Refit unless
  the contract surface grows large.
- Methods are `async Task<T>` (no `Result<T>` in the client —
  exceptions are the right pattern for transport failures).
- The DTO returned mirrors the API's DTO. They are
  intentionally duplicated for now; a future
  `Cardscape.Contracts` project can share them.

Register the client in `Program.cs`:

```csharp
builder.Services.AddHttpClient<BoardsApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});
```

### Step 8 — Web: Radzen component

```razor
@* src/Cardscape.Web/Components/Boards/BoardRenameDialog.razor *@
@inject DialogService DialogService
@inject BoardsApiClient BoardsApi

<RadzenStack Gap="16px">
    <RadzenLabel Text="New board name" Component="newName" />
    <RadzenTextBox @bind-Value="@newName" Name="newName" />
    <RadzenStack Orientation="@Orientation.Horizontal" JustifyContent="@JustifyContent.End" Gap="8px">
        <RadzenButton Text="Cancel" Click="@(() => DialogService.Close(null))" />
        <RadzenButton Text="Save" ButtonStyle="@ButtonStyle.Primary" Click="@Save" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public Guid BoardId { get; set; }
    private string newName = string.Empty;

    private async Task Save()
    {
        await BoardsApi.RenameAsync(BoardId, newName);
        DialogService.Close(true);
    }
}
```

- Always use the `@` prefix on Radzen enum properties
  (`@Orientation.Horizontal`, `@JustifyContent.End`, etc.).
- Always end the file with `<RadzenComponents />` in the
  `MainLayout.razor`.
- See `.agents/skills/radzen-blazor/SKILL.md` for the
  component-by-component reference.

### Step 9 — Unit test

```csharp
// tests/Cardscape.UnitTests/Application/Boards/RenameBoardCommandHandlerTests.cs
public sealed class RenameBoardCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenBoardDoesNotExist_ReturnsNotFound()
    {
        // arrange
        var repo = new Mock<IBoardRepository>();
        var uow = new Mock<IUnitOfWork>();
        var handler = new RenameBoardCommandHandler(repo.Object, uow.Object);

        // act
        var result = await handler.Handle(
            new RenameBoardCommand(Guid.NewGuid(), "New name"),
            CancellationToken.None);

        // assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BoardErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenBoardExists_RenamesAndReturnsDto()
    {
        // arrange
        var board = Board.Create(
            new BoardId(Guid.NewGuid()),
            BoardName.Create("Old name"));
        var repo = new Mock<IBoardRepository>();
        repo.Setup(r => r.GetByIdAsync(board.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(board);
        var uow = new Mock<IUnitOfWork>();
        var handler = new RenameBoardCommandHandler(repo.Object, uow.Object);

        // act
        var result = await handler.Handle(
            new RenameBoardCommand(board.Id.Value, "New name"),
            CancellationToken.None);

        // assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("New name");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- Arrange / Act / Assert, with comments when the intent isn't
  obvious.
- Test names are sentences: `Method_Scenario_ExpectedBehavior`.
- One assertion per test is a guideline, not a rule.

### Step 10 — Integration test (later)

Once we have an integration test project wired to a real SQLite
file, add:

```csharp
[Trait("Database", "Sqlite")]
public sealed class RenameBoardEndpointTests
    : IClassFixture<CardscapeApiFactory>
{
    [Fact]
    public async Task RenameBoard_WithValidPayload_ReturnsUpdatedBoard()
    {
        // arrange
        var client = _factory.CreateClient();
        // create a board, get its id, then PUT a new name
        // ...

        // act
        var response = await client.PutAsJsonAsync(
            $"/api/boards/{boardId}/name",
            new { newName = "Renamed" });

        // assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await response.Content.ReadFromJsonAsync<BoardDto>();
        board!.Name.Should().Be("Renamed");
    }
}
```

Integration tests are tagged with `[Trait("Database", "Sqlite")]` so
the test matrix stays honest about which engines it actually
exercises.

## 3. Checklist before opening a PR

- [ ] Domain layer changed? An entity / VO / event updated.
- [ ] Application layer changed? Command, handler, validator.
- [ ] Infrastructure layer changed? EF Core configuration +
      migration in all three providers.
- [ ] API endpoint added or changed? Registered in
      `Program.cs`.
- [ ] Web client method added? Registered in `Program.cs`.
- [ ] Blazor component uses `@` for Radzen enums.
- [ ] Unit tests added or updated.
- [ ] Integration test added with the right `[Trait]`.
- [ ] XML doc comments on public types and members.
- [ ] `dotnet build` is green.
- [ ] `dotnet test` is green.
- [ ] `git diff` is small enough to review in one sitting.

## 4. Anti-patterns

- **Don't add a service layer on top of Wolverine.** Handlers
  already are the application services. A `BoardService` that
  calls handlers is two layers for one job.
- **Don't put business logic in the endpoint.** Endpoints are
  thin adapters from HTTP to `IRequest<Result<T>>`. If you
  find yourself writing domain logic in the endpoint, move it
  to the handler or the entity.
- **Don't use `IQueryable<T>` outside `Infrastructure`.**
  Application layer returns materialized DTOs. Persistence
  specifics stay in the persistence layer.
- **Don't bypass the DI container.** New services are
  registered in the appropriate `AddXxx` extension method
  (`AddApplication`, `AddInfrastructure`, `AddApi`,
  `AddWeb`).
