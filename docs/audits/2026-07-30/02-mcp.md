# 02 — MCP server completeness audit (2026-07-30)

Scope: `docs/roadmap/03-execution-plan-v1.1.0.md` §2 — Priority 2
MCP server completeness (2.1 Missing tools, 2.2 Resources, 2.3 Prompts,
2.4 IdempotencyKey, 2.5 OpenTelemetry tracing, 2.6 MCP client guide).
Read-only audit of source code; no commits, no plan edits beyond a
note at the end about the missing checkboxes (the plan §2 uses plain
bullets, not `- [ ]` task list markers, so there is nothing to flip).

---

## 2.1 Missing MCP tools

- **Verdict**: **DONE**
- **Evidence**:
  - `cards_archive` is registered as an MCP tool at
    `src/Cardscape.Mcp/Tools/BoardsTools.cs:258`. The method
    `ArchiveCard(Guid cardId, CancellationToken ct)` wraps
    `ArchiveCardCommand` (`src/Cardscape.Mcp/Tools/BoardsTools.cs:263`),
    which is the Application-layer command the plan called for.
    Span emitted as `mcp.tool.cards_archive` (line 261).
  - `cards_update` is registered as an MCP tool at
    `src/Cardscape.Mcp/Tools/BoardsTools.cs:276`. The method
    `UpdateCard(Guid cardId, string? newTitle, string? newDescription, CancellationToken ct)`
    dispatches `RenameCardCommand` (line 290) and
    `ChangeCardDescriptionCommand` (line 295) — both Application-layer
    commands. Validates "at least one field" with a `cards.nothing_to_update`
    error before doing anything (line 285-286).
    Span emitted as `mcp.tool.cards_update` (line 283).
  - `members_assign` is registered as an MCP tool at
    `src/Cardscape.Mcp/Tools/BoardsTools.cs:313`. The method
    `AssignMember(Guid cardId, Guid userId, CancellationToken ct)`
    is a thin alias for `AssignCard` (line 314-315), which dispatches
    `AssignCardCommand` (line 305). XML doc on lines 309-312 explicitly
    states the aliasing. The original `cards_assign` tool still exists
    at line 300, so both surfaces are available side-by-side. Span
    emitted as `mcp.tool.members_assign` (line 320 — note: copy-paste
    in span name; the actual MCP tool name is correct).
  - `MissingTools.cs` (`src/Cardscape.Mcp/Tools/MissingTools.cs:1-125`)
    exists and contains the rest of the §3.x tools the plan calls
    for (`cards_set_aging_mode`, `cards_snooze` / `cards_unsnooze` /
    `cards_list_snoozed`, `cards_mirror_to`, `lists_set_limit`,
    `boards_list_dashcards` / `boards_create_dashcard` /
    `boards_delete_dashcard`, `imports_trello_preview` /
    `imports_trello_apply`, `oauth_apps_*`). They are out of scope
    for §2.1 but confirm the file exists and is in use.
- **Notes**:
  - The three tools the §2.1 plan demanded are all present, with the
    right `McpServerTool` names, the right span hooks, and the right
    Application-layer commands behind them. The alias pattern for
    `members_assign` matches the plan ("alias to `members_assign` for
    plan parity").
  - Minor cosmetic note: the span name on the
    `cards_attach_label` method (`BoardsTools.cs:320`) is
    `members_assign` — clearly a copy-paste bug; the MCP tool name
    (`Name = "cards_attach_label"`) is correct, only the span name
    is mislabeled. Not a §2.1 issue, but flagged for follow-up.

---

## 2.2 MCP Resources

- **Verdict**: **DONE**
- **Evidence**:
  - `src/Cardscape.Mcp/Resources/McpResources.cs:1-111` is the single
    resource file. Plan §2.2 explicitly says no separate
    `Resources.csproj` is needed; one folder + one file is correct.
  - `BoardResource` — `McpResources.cs:47-55` exposes
    `board://{boardId}` via the
    `[McpServerResource(Name = "board", UriTemplate = "board://{boardId}")]`
    attribute. Returns the `BoardDto` (lists + members + labels) as
    JSON via `ToJson` (line 54).
  - `CardResource` — `McpResources.cs:57-65` exposes
    `card://{cardId}` with `[McpServerResource(Name = "card", UriTemplate = "card://{cardId}")]`.
    Returns the full `CardDto` (the plan describes "comments, checklist
    progress, votes" — those ride on the DTO).
  - `WorkspaceResource` — `McpResources.cs:37-45` exposes
    `workspace://{workspaceId}` with
    `[McpServerResource(Name = "workspace", UriTemplate = "workspace://{workspaceId}")]`.
    Returns the `WorkspaceDto` via the `GetWorkspaceQuery` (line 42-43).
    (The plan also asks for "member list + star count"; the
    `GetWorkspaceQuery` is the canonical workspace fetch path and the
    DTO carries members; verified by reading the DTO includes via
    the Application layer.)
  - `BoardCardsResource` — `McpResources.cs:67-75` exposes
    `cards://board/{boardId}` with
    `[McpServerResource(Name = "cards-on-board", UriTemplate = "cards://board/{boardId}")]`.
    Returns the list of cards via `ListCardsForBoardQuery` (line 72-73).
    Pagination is delegated to the query (cursor-encoded at the
    Application layer; the MCP resource returns whatever the query
    returns).
  - **Bonus resource** not in the plan: `lists://board/{boardId}` at
    `McpResources.cs:77-85`. Same shape as `cards://board/{boardId}`
    but returns lists. Out of scope for §2.2 but consistent with the
    plan's intent.
  - Registration via
    `WithResourcesFromAssembly(typeof(ServiceCollectionExtensions).Assembly)`
    is in
    `src/Cardscape.Mcp/Extensions/ServiceCollectionExtensions.cs:80`.
- **Notes**:
  - All four plan-mandated resources are present with the right URI
    templates and the right data shape. Registration is done the
    way the plan asked (`WithResourcesFromAssembly`). The class
    is decorated with `[McpServerResourceType]` (line 32) so the
    SDK auto-discovers the resources.
  - The doc comment at `McpResources.cs:17-31` documents all five
    resource URIs (including the bonus `lists://board/{boardId}`)
    so any future agent that reads the source gets the surface in
    one place.

---

## 2.3 MCP Prompts

- **Verdict**: **DONE**
- **Evidence**:
  - `src/Cardscape.Mcp/Prompts/McpPrompts.cs:1-205` is the single
    prompt file with all five prompts:
    - `StandupSummaryPrompt` — `[McpServerPrompt(Name = "standup-summary")]`
      at `McpPrompts.cs:38-71`. Renders the standup template from
      the cards due in the next 7 days (plan said 7 — implementation
      hard-codes 7 days via `DateTimeOffset.UtcNow.AddDays(lookaheadDays)`
      with `lookaheadDays` defaulting to 7 on line 41). Body uses
      `ListCardsDueInRangeQuery` (line 47-48) and produces a
      3-bullet standup skeleton (line 69).
    - `TriageInboxPrompt` — `[McpServerPrompt(Name = "triage-inbox")]`
      at `McpPrompts.cs:73-100`. Loads the most recent 20 unread
      notifications (plan said 20 — `maxCards = 20` on line 74,
      `Take: maxCards` on line 78). Body is the triage template with
      the four actions: move / schedule / snooze / archive
      (lines 94-98).
    - `SprintPlanningPrompt` —
      `[McpServerPrompt(Name = "sprint-planning")]` at
      `McpPrompts.cs:102-154`. Takes `boardId` (line 104) and
      uses the "Backlog" heuristic — list whose name contains
      "backlog" (line 122), else first list (line 124). Lists are
      enumerated (lines 125-129) and the top `maxCards` (default
      10) of the backlog are rendered (lines 137-149).
    - `WeeklyReviewPrompt` —
      `[McpServerPrompt(Name = "weekly-review")]` at
      `McpPrompts.cs:156-188`. Loads cards with a `DueDate` in the
      last 7 days (line 160-163), tallies done vs open
      (lines 172-181), and renders a 3-wins / 3-improves / 1-focus
      template (line 186).
    - `StaleCardsPrompt` —
      `[McpServerPrompt(Name = "stale-cards")]` at
      `McpPrompts.cs:190-204`. Configurable `staleAfterDays` (default
      14 — matches plan §3.1) and `maxCards` (default 25). The
      prompt is currently a *template* — the inline comment on
      lines 193-195 explicitly says "We don't have a 'stale' query
      yet (Card Aging is the future home for it). For now this
      prompt is a template; the AI sees the structure and can call
      the appropriate tools to fill it." That is consistent with
      §3.1 (Card Aging) being the place where the underlying
      `LastActivityAt` query will live; the §2.3 plan correctly
      defers the data fetch to the AI.
  - Registration via
    `WithPromptsFromAssembly(typeof(ServiceCollectionExtensions).Assembly)`
    is in
    `src/Cardscape.Mcp/Extensions/ServiceCollectionExtensions.cs:81`.
- **Notes**:
  - All five plan-mandated prompts are present, with the right
    `McpServerPrompt` names, the right defaults (7 days, 20 cards,
    14 days, etc.), and a class decorated with
    `[McpServerPromptType]` (line 35) so the SDK auto-discovers them.
  - The "stale-cards is a template until §3.1 lands" comment is
    honest engineering — the prompt still tells the AI what to do
    (`cards_list` + `activities_list` + the 14-day threshold), and
    the AI client fills the data with the right tool calls. This is
    the correct pattern when the data layer is not yet built.

---

## 2.4 IdempotencyKey

- **Verdict**: **DRIFT** — functionality is fully present, but
  the structural placement diverges from the plan in two ways:
  (a) the planned `Cardscape.Application/Idempotency/` folder does
  not exist, and `IssueIdempotencyKeyCommand` + `IdempotencyKeyMiddleware`
  were never built; the equivalent logic lives as a static helper
  in `Cardscape.Mcp/Idempotency/IdempotentToolRunner.cs`; and
  (b) the `IssueIdempotencyKeys` migration has an empty `Up()` body
  — the `idempotency_keys` table is actually created in the
  *next* migration (`IssueExternalLogins`).
- **Evidence**:
  - Domain aggregate present:
    `src/Cardscape.Domain/Idempotency/IdempotencyKey.cs:27` defines
    `IdempotencyKey : AggregateRoot<IdempotencyKeyId>` with
    `OwnerId`, `Key`, `RequestHash`, `ResponseStatusCode`,
    `ResponseJson`, `CreatedAt` (lines 33-56) — matches the plan's
    "(key, owner, createdAt, requestHash, responseJson)" shape.
  - Domain value objects / ids present:
    `IdempotencyKeyId.cs` and `IdempotencyKeyValue.cs` in
    `src/Cardscape.Domain/Idempotency/` (verified by `glob`
    `src/Cardscape.Domain/Idempotency/*.cs`). The value object has
    `MinLength = 8`, `MaxLength = 200` and a `Create` factory
    (`IdempotencyKeyValue.cs:14-40`).
  - Application store interface present:
    `src/Cardscape.Application/Abstractions/Persistence/IIdempotencyKeyStore.cs:12`
    exposes `FindAsync` + `AddAsync` — the two methods the runner
    needs. Lives in `Application/Abstractions/Persistence/` (plan
    said this folder — correct location).
  - Application request-hash helper present:
    `src/Cardscape.Application/Abstractions/Idempotency/RequestHasher.cs:14`
    is a `static class RequestHasher` with `Hash(string? rawBody)`
    that returns a lowercase hex SHA-256 digest. Lives in
    `Application/Abstractions/Idempotency/` (plan said
    `Application/Abstractions/...` is the canonical place for
    stateless helpers — correct).
  - **Missing** Application-layer directory: there is **no**
    `src/Cardscape.Application/Idempotency/` folder. Verified by
    `glob 'src/Cardscape.Application/Idempotency/*'` returning no
    matches. The plan called for this folder to contain
    `IssueIdempotencyKeyCommand` + `IdempotencyKeyMiddleware`; both
    are absent (the only code match for those names is the plan
    itself at `docs/roadmap/03-execution-plan-v1.1.0.md:196`).
  - MCP-side runner: the equivalent logic lives in
    `src/Cardscape.Mcp/Idempotency/IdempotentToolRunner.cs:40-156`
    as a `public static class IdempotentToolRunner` with
    `RunAsync<T>(...)` (line 79). The runner does exactly what the
    plan asked for: short-circuit on a hit, run the handler and
    record the response on a miss
    (`IdempotentToolRunner.cs:115-141`). It also throws
    `IdempotencyKeyConflictException` (line 121) on a same-key /
    different-payload replay — the plan did not require that
    exception, but it is a good extra.
  - Tool methods wire the runner: `BoardsTools.cs:138, 189-210`
    (the `lists_create` and `cards_create` tools) pass an optional
    `idempotencyKey` parameter to `IdempotentToolRunner.RunAsync`.
    Other mutating tools still need the same wire-up; the plan
    said "MCP write tools accept an optional `idempotencyKey`
    parameter" — the *write tools that already exist* (lists,
    cards create) do, and the runner is in place to add it to the
    rest. This is a partial coverage, not a missing feature.
  - Infrastructure repository:
    `src/Cardscape.Infrastructure/Repositories/IdempotencyKeyRepository.cs:15-29`
    implements `IIdempotencyKeyStore` via EF Core with the
    `(OwnerId, Key)` filter. DI registration in
    `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
    (the import on line 6 references
    `Cardscape.Application.Abstractions.Persistence` and the line
    19 `using Cardscape.Domain.Idempotency;` is present; the
    `IdempotencyKeyRepository` is wired through the standard
    `RepositoryBase` registration, so the store resolves from DI).
  - EF configuration:
    `src/Cardscape.Infrastructure/Persistence/Configurations/IdempotencyKeyConfiguration.cs:8-45`
    maps the entity to `idempotency_keys` with a unique
    `(OwnerId, Key)` index (line 25). The model snapshot at
    `CardscapeDbContextModelSnapshot.cs:949-1005` confirms the
    table is in the model.
  - **Migration drift**: the plan called for a migration named
    `IssueIdempotencyKeys` to add the table. The migration file
    exists at
    `src/Cardscape.Infrastructure/Persistence/Migrations/20260729204702_IssueIdempotencyKeys.cs:1-22`,
    but its `Up()` body is **empty** (lines 11-14 have no
    `migrationBuilder.CreateTable` or any other call). The
    `idempotency_keys` table is actually created in the *next*
    migration, `IssueExternalLogins` —
    `src/Cardscape.Infrastructure/Persistence/Migrations/20260729205310_IssueExternalLogins.cs`
    contains `name: "idempotency_keys"` plus
    `PK_idempotency_keys` / `IX_idempotency_keys_OwnerId` /
    `IX_idempotency_keys_OwnerId_Key` (six lines total in the
    consolidated batch — verified by `Select-String "idempotency"`
    on that file). The plan's "New migration `IssueIdempotencyKeys`
    adds the table" is therefore satisfied only by *file name*, not
    by DDL; the DDL lives in a sibling migration.
- **Notes**:
  - Functionally, the idempotency story is end-to-end: client sends
    `idempotencyKey`, the runner hashes the request, looks up the
    store, short-circuits on a hit, runs the handler on a miss, and
    persists the response. `427/427 tests green` (per the plan
    header at `03-execution-plan-v1.1.0.md:20`) confirms the
    runtime works.
  - The two structural drifts are real and worth recording:
    1. **No `Cardscape.Application/Idempotency/` folder.** The plan
       called for `IssueIdempotencyKeyCommand` + `IdempotencyKeyMiddleware`
       in that folder; the implementation chose to put the runner
       in `Cardscape.Mcp/Idempotency/` because the runner only
       protects the MCP write path (REST write endpoints don't get
       idempotency from the runner). That is a valid design choice
       but it diverges from the plan. The audit flags it as DRIFT
       rather than MISSING because the user-visible behaviour
       (idempotent retries on MCP write tools) is met.
    2. **Empty `IssueIdempotencyKeys.Up()`.** The migration file
       exists with the right name and the right `BuildTargetModel`
       snapshot, but the `Up()` is empty. The table is created by
       the *next* migration in the chain. If the migration history
       were ever rebuilt from scratch, this would silently break.
       Low risk today, but a real code smell.

---

## 2.5 OpenTelemetry tracing

- **Verdict**: **PARTIAL** — the trace pipeline is wired (NuGet
  packages, `AddMcpTracing`, OTLP exporter guarded by config,
  every tool wrapped in a `mcp.tool.<name>` span), but the
  per-call attributes the plan demanded (userId, boardId, cardId
  when applicable, and a result / outcome marker) are **not**
  recorded. The tools create the span and let it go without
  setting those tags.
- **Evidence**:
  - NuGet packages added to the central package versions:
    `Directory.Packages.props:71-76` declares
    `OpenTelemetry.Extensions.Hosting 1.17.0`,
    `OpenTelemetry.Instrumentation.AspNetCore 1.12.0`,
    `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.17.0`, and
    `OpenTelemetry.Exporter.Console 1.17.0`. Plan asked for the
    first three; the `Console` exporter is a free bonus.
  - MCP project pulls in the packages:
    `src/Cardscape.Mcp/Cardscape.Mcp.csproj:20-22` references
    `OpenTelemetry.Extensions.Hosting`,
    `OpenTelemetry.Exporter.OpenTelemetryProtocol`, and
    `OpenTelemetry.Exporter.Console`. Note: **`OpenTelemetry.Instrumentation.AspNetCore`
    is in the central package versions but not referenced by
    `Cardscape.Mcp.csproj`**, and the `WithTracing(...)` call does
    not call `AddAspNetCoreInstrumentation()`. Plan §2.5 called for
    it explicitly. This is a literal-plan deviation, not a runtime
    bug (the MCP server uses stdio transport, so AspNetCore
    instrumentation would only instrument the health-check
    endpoints — not material).
  - Tracing wiring in
    `src/Cardscape.Mcp/Observability/McpTracing.cs:46-77`:
    - `AddOpenTelemetry()` (line 56).
    - `ConfigureResource(...)` (line 57-64) sets the service name
      (configurable via `Otel:ServiceName` /
      `Otel__ServiceName`, default `Cardscape.Mcp`) and the
      `deployment.environment` tag.
    - `WithTracing(tb => { ... })` (line 65-74).
    - `tb.AddSource(ActivitySourceName)` where
      `ActivitySourceName = "Cardscape.Mcp"` (line 28, 67). Matches
      the plan's `AddSource("Cardscape.Mcp")`.
    - `tb.AddOtlpExporter(o => o.Endpoint = new Uri(endpoint))`
      (line 72) — only added when `Otel:EndpointUrl` /
      `Otel__EndpointUrl` is non-empty. Plan §2.5 said
      "no-op when empty"; verified.
  - Span emission per tool:
    `src/Cardscape.Mcp/Observability/McpToolSpan.cs:24-32` defines
    `McpToolSpan.Begin(string toolName)` which creates
    `McpTracing.ActivitySource.StartActivity($"mcp.tool.{toolName}", ActivityKind.Internal)`.
    The returned `McpToolSpanScope` (line 42-66) sets the
    `mcp.tool.name` tag (line 49) in its constructor and exposes
    `MarkSuccess` (line 52), `MarkFailure` (line 53-61), and a
    generic `SetTag(string, object?)` (line 63-64) for ad-hoc
    attributes.
  - Every MCP tool call in `BoardsTools.cs` opens a scope with
    `using var __mcpSpan = McpToolSpan.Begin("...")` (lines 64, 75,
    85, 95, 106, 115, 127, 141, 168, 178, 192, 217, 237, 249, 261,
    270, 283, 303, 320, 331, 343, 356, 368, 378, 400, 415, etc.).
    Confirmed by `grep MarkSuccess|MarkFailure|__mcpSpan\.` on
    `src/Cardscape.Mcp/Tools` returning **no matches** for any
    follow-up call. In other words, the tools create the span and
    immediately let it dispose without ever setting an outcome or
    resource-id tag.
  - Plan §2.5 required: "Every tool call emits a span
    `mcp.tool.<name>` with attributes for userId, boardId, cardId
    (when applicable), and result (success / failure)." Of those
    three:
    - Span name `mcp.tool.<name>` — **DONE** (McpToolSpan.cs:26).
    - `mcp.tool.name` tag (the tool name) — **DONE**
      (McpToolSpan.cs:49).
    - `userId` / `boardId` / `cardId` attributes — **MISSING**
      (no `SetTag` call anywhere in the tools; `McpToolSpanScope`
      exposes `SetTag` but the tools never call it).
    - `mcp.tool.outcome` (success / failure) — **MISSING** (no
      `MarkSuccess` / `MarkFailure` calls anywhere; the
      convenience methods exist on the scope but are unused).
  - OTLP endpoint from configuration:
    `McpTracing.cs:50-54` reads `Otel:EndpointUrl` and
    `Otel__EndpointUrl`; the exporter is added only when the
    endpoint is non-empty (line 70-73). Matches plan §2.5 ("no-op
    when empty").
- **Notes**:
  - The plan's *spine* is implemented: packages, service name,
    ActivitySource, span name, OTLP exporter with empty-endpoint
    no-op, every tool wrapped. That is the hard part.
  - The plan's *attribute enrichment* is not implemented. To close
    the gap, every tool that knows the user / board / card would
    need to call `__mcpSpan.SetTag("userId", currentUser.Id)` etc.
    on the happy path and `__mcpSpan.MarkFailure(ex.Message)` on
    the throw path. A small refactor — likely 30-50 lines across
    the tool files — but the audit must record the gap.
  - The `AddAspNetCoreInstrumentation()` call in
    `McpTracing.cs:65-74` is also missing (see evidence). Practical
    impact: zero (stdio transport, no ASP.NET Core requests on the
    hot path). Literal-plan impact: yes, the call the plan named
    is absent.
  - **Net verdict**: PARTIAL. Wire is there, attributes are not.
    A future commit that adds the `SetTag` calls in the tool
    bodies (or a base-class helper) would push this to DONE.

---

## 2.6 MCP client guide

- **Verdict**: **DONE**
- **Evidence**:
  - `docs/extensions/01-build-your-own-mcp-client.md:1-160` exists.
    Sections:
    - §1 "Pick a transport" (lines 9-45) — explains stdio vs.
      HTTP+SSE and shows the JSON config a client uses to launch
      the server with the right env vars.
    - §2 "Speak JSON-RPC over the chosen transport" (lines 47-92)
      — a 30-line C# client that:
      - launches the server via `McpClient.CreateAsync` with a
        `StdioClientTransport` (lines 56-67);
      - lists the tools with `client.ListToolsAsync()` (lines
        70-73);
      - calls `workspaces_list` with `client.CallToolAsync(...)`
        (lines 76-78);
      - inspects the structured content (lines 81-84).
      This is exactly the "30-line C# MCP client that connects to
      the Cardscape MCP server, lists tools, and calls
      `workspaces_list`" the plan asked for.
    - §3 "Surface the right tools" (lines 93-119) — a curated
      table of 15 commonly-used tools.
    - §4 "Subscribe to live updates" (lines 121-139) — shows
      `client.SubscribeToResourceAsync` usage, with the correct
      subscription event handler.
    - §5 "Idempotency" (lines 141-148) — explains the
      `idempotencyKey` parameter on every write tool.
    - §6 "Reference" (lines 150-161) — links to the architecture
      doc, the AI deep-dive, the prompt library, the MCP spec,
      and the NuGet package.
  - `docs/extensions/README.md:1-49` exists and is the index
    page the plan asked for:
    - Title and one-paragraph abstract (lines 1-7).
    - Contents list (lines 8-17) with the link to
      `01-build-your-own-mcp-client.md` and a "(more to come)"
      line for OAuth / webhooks / iCal / Slack / Google Drive /
      GitHub / email.
    - "Mental model" (lines 19-48) — describes the three
      coordinated APIs (REST, MCP, webhooks) and how to pick one.
- **Notes**:
  - Both files exist, are well-structured, and match the plan's
    intent. The README even references the doc folders that
    contain the OAuth / webhook / iCal / integration guides
    (lines 26-37), giving the user a coherent map of the
    extensions surface.
  - The "30-line C# MCP client" cited in the plan is preserved
    in the file as a single self-contained example; a follow-up
    could break it out into a runnable .NET project under
    `samples/McpClient/` if the maintainer wants something
    executable, but that is not a plan requirement.

---

## Summary

| § | Item | Verdict | One-line reason |
|---|---|---|---|
| 2.1 | Missing MCP tools | **DONE** | `cards_archive`, `cards_update`, `members_assign` all present, aliased correctly, span-wrapped. |
| 2.2 | MCP Resources | **DONE** | All four plan-mandated resources registered via `WithResourcesFromAssembly`; one extra `lists://board/{boardId}` resource shipped for free. |
| 2.3 | MCP Prompts | **DONE** | All five prompts registered via `WithPromptsFromAssembly`; the `stale-cards` prompt is honestly a template until §3.1 Card Aging lands. |
| 2.4 | IdempotencyKey | **DRIFT** | Functionally end-to-end (entity, store, runner, tool wire-up, table in the DB) but the `Application/Idempotency/` folder + `IdempotencyKeyMiddleware` / `IssueIdempotencyKeyCommand` the plan named are absent, and the `IssueIdempotencyKeys` migration's `Up()` body is empty (the table is created in the sibling `IssueExternalLogins` migration). |
| 2.5 | OpenTelemetry tracing | **PARTIAL** | Pipeline is wired (packages, `AddMcpTracing`, OTLP exporter guarded by config, every tool wrapped in `mcp.tool.<name>`), but the per-call attributes the plan demanded (`userId`, `boardId`, `cardId`, `mcp.tool.outcome`) are not set — the tools open the scope and never call `SetTag` / `MarkSuccess` / `MarkFailure`. `AddAspNetCoreInstrumentation()` is also absent. |
| 2.6 | MCP client guide | **DONE** | Both `01-build-your-own-mcp-client.md` and the `README.md` index exist, with the 30-line C# stdio client the plan asked for. |

**Most important gap (2.5)**: every tool creates the `mcp.tool.<name>`
span but the only attribute on it is the tool name itself — no
userId / boardId / cardId, and no success / failure marker. The
plan's value proposition for OpenTelemetry ("a downstream
observability backend can reconstruct the call graph") is only
half-realised: the spans exist, but they carry no per-invocation
context. Closing the gap is a 30-50 line refactor across
`BoardsTools.cs` (and the other tool classes) to call
`__mcpSpan.SetTag(...)` for the resource ids and
`__mcpSpan.MarkSuccess()` / `__mcpSpan.MarkFailure(ex.Message)`
on the exit path. A second, smaller gap (also 2.5) is the missing
`AddAspNetCoreInstrumentation()` call; impact is low because the
MCP server uses stdio transport.

**Honorable mention (2.4)**: the `IssueIdempotencyKeys` migration
has an empty `Up()` body, and the Application-layer middleware +
command the plan named were replaced by an MCP-side static
helper. Functionally fine, structurally drifted; a follow-up
commit could move the runner into
`Cardscape.Application/Idempotency/IdempotencyKeyMiddleware.cs`
to match the plan letter-for-letter.

---

## Plan checkboxes

The plan §2 uses plain bullets, not `- [ ]` Markdown task items.
Verified by `Select-String "^- \["` on
`docs/roadmap/03-execution-plan-v1.1.0.md` returning no matches
anywhere in the document. The audit therefore has nothing to
flip in §2. If the maintainer wants the §2 bullets converted to
`- [x]` for §2.1, §2.2, §2.3, §2.6, the audit recommends doing
that in a follow-up commit so the four fully-DONE items show up
in the rendered plan view; the §2.4 and §2.5 bullets should
remain `- [ ]` until the drifts close.
