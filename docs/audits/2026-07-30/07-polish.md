# 07 — Polish & scale audit (2026-07-30)

Scope: `docs/roadmap/03-execution-plan-v1.1.0.md` §5 — Priority 5
(`5.1 i18n infrastructure`, `5.2 i18n English + Spanish`,
`5.3 PWA manifest`, `5.4 C# API client SDK`, `5.5 Public status page`,
`5.6 Import from other kanban tools`, `5.7 Export per-board archive`,
`5.8 MCP subscriptions`). Read-only audit. The plan uses plain
bullets under each `### 5.X` heading — there are **no**
`- [ ]` / `- [x]` task checkboxes in §5, so nothing to flip on
the fully-DONE items; the gap is documented in the summary.

---

## 5.1 i18n infrastructure

- **Verdict**: **PARTIAL**
- **Plan asks for**:
  - `Microsoft.Extensions.Localization` available (transitive OK).
  - `src/Cardscape.Web/Resources/SharedResource.resx` (English).
  - `src/Cardscape.Web/Resources/SharedResource.es.resx` (Spanish).
  - `builder.Services.AddLocalization(opts => opts.SetDefaultCulture("en").AddSupportedCultures("en", "es"))` + `app.UseRequestLocalization(...)` in `Program.cs`.
  - Culture resolved from `Accept-Language` (no UI picker yet).
- **Evidence**:
  - `src/Cardscape.Web/Resources/SharedResource.cs:1-11` — empty marker class used as the `IStringLocalizer<SharedResource>` type key.
  - `src/Cardscape.Web/Resources/SharedResource.resx` and `SharedResource.es.resx` both exist (`15 501` and `16 453` bytes respectively).
  - `src/Cardscape.Web/Program.cs:33-36` — `AddLocalization(options => { options.ResourcesPath = "Resources"; })`. **Note**: the call does **not** use the plan's `SetDefaultCulture("en").AddSupportedCultures("en", "es")` shape; the supported culture array is held locally as `string[] supportedCultures = { "en", "es" };` (`Program.cs:31`) and is unused.
  - `src/Cardscape.Web/Program.cs:38-43` — default culture is applied through `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture`, not via `UseRequestLocalization`.
  - `src/Cardscape.Web/Program.cs:22-29` — explicit comment: **"Blazor WebAssembly does not run the server-side UseRequestLocalization middleware; the client picks the culture explicitly via the CulturePicker and stores the choice in localStorage."** So `app.UseRequestLocalization(...)` is intentionally absent because this is a WASM project, not an ASP.NET Core host.
  - `src/Cardscape.Web/Program.cs:51` — `IStringLocalizer<SharedResource>` is registered as a scoped service (`StringLocalizer<SharedResource>`), so the localizer reaches the `Resources/SharedResource.*.resx` files.
- **Notes**:
  - The infrastructure works (en + es resources resolve), but the exact API the plan calls out — fluent `AddLocalization(SetDefaultCulture/AddSupportedCultures)` + `UseRequestLocalization` — is not used. The plan and the implementation disagree about the hosting model; the implementation is correct for Blazor WASM but is not what the plan literally specifies.
  - Culture resolution from `Accept-Language` (as the plan wants) is **not** wired — the comment at `Program.cs:25-29` says the CulturePicker drives selection. The plan explicitly says "The current culture is resolved from the `Accept-Language` header" — that does not match.

---

## 5.2 i18n: English + Spanish

- **Verdict**: **DONE** (with minor drift)
- **Plan asks for**: extract every user-visible string from the 25 Blazor pages into `SharedResource.resx`; translate ~150 most-visible strings to Spanish in `SharedResource.es.resx`; document the workflow in `docs/i18n/02-translation-workflow.md` (the "practical extraction guide").
- **Evidence**:
  - `SharedResource.resx` has **137** `<data name="...">` entries.
  - `SharedResource.es.resx` has **140** `<data name="...">` entries (one duplicate `OAuthAppsRegisterBlurb` → 139 unique Spanish keys).
  - **Drift**:
    - 1 EN key missing in ES: `OAuthAppsRegisterNewBlurb`.
    - 3 unique ES keys not in EN: `OAuthAppsDocsHint`, `OAuthAppsRegisterBlurb`, `OAuthAppsScopes`.
  - `docs/i18n/02-translation-workflow.md:1-259` — exists, 259 lines. The plan said "already exists; the new content is the practical extraction guide." The existing doc covers the file-based translation workflow (sibling `.es.md` files) for Markdown artifacts; it does **not** describe the resx-based extraction guide for Blazor pages that the plan alludes to.
- **Notes**:
  - The plan target of "~150 most-visible strings to Spanish" is essentially met (137–140 in the ~150 ballpark).
  - The doc describes the broader translation workflow for docs/Markdown, not the resx extraction flow used by the Blazor code — the doc is internally consistent but is not the "practical extraction guide" promised in the plan.
  - The Spanish keyset has 3 orphan keys; the English keyset has 1 orphan. Both directions need a parity sweep.

---

## 5.3 PWA manifest

- **Verdict**: **DONE**
- **Plan asks for**:
  - `src/Cardscape.Web/wwwroot/manifest.webmanifest` with name, short name, icons (192, 512, maskable), theme/background colors, `display: standalone`, `start_url: /`, `scope: /`.
  - `src/Cardscape.Web/wwwroot/service-worker.js` — cache app shell, network-first for `/api/*`, offline fallback for navigation.
  - Reference from `wwwroot/index.html`.
- **Evidence**:
  - `wwwroot/manifest.webmanifest:1-38` — name/short_name "Cardscape", description, `start_url: "/"`, `scope: "/"`, `display: "standalone"`, `background_color: "#0f172a"`, `theme_color: "#1d4ed8"`, `orientation: "any"`, two `categories`, and **four** icon entries: `icons/icon-192.png` (any), `icons/icon-192-maskable.png` (maskable), `icons/icon-512.png` (any), `icons/icon-512-maskable.png` (maskable).
  - `wwwroot/icons/icon-192.png` (2 626 B), `wwwroot/icons/icon-192-maskable.png` (3 361 B), `wwwroot/icons/icon-512.png` (64 281 B), `wwwroot/icons/icon-512-maskable.png` (18 348 B) — all four PNGs present.
  - `wwwroot/service-worker.js:1-116` — defines `CACHE_VERSION = 'cardscape-v1'`, pre-caches `/`, `/index.html`, `/manifest.webmanifest`, `/favicon.png`, `/css/app.css`; `fetch` handler returns network-first for `/api/`, `/hubs/`, `/_blazor/`, and cache-first with `/index.html` fallback for `request.mode === 'navigate'`.
  - `wwwroot/index.html:13` — `<link rel="manifest" href="manifest.webmanifest" />`; `wwwroot/index.html:53-59` — script registers `service-worker.js` after Blazor boot.
- **Notes**: matches the plan exactly. The audit follow-up table at `docs/roadmap/03-execution-plan-v1.1.0.md:32` says the previous pass had to add `icon-512.png`; both `icon-512.png` and `icon-512-maskable.png` are now present, and the manifest exposes both `any` and `maskable` purposes.

---

## 5.4 C# API client SDK

- **Verdict**: **DONE** (with one structural drift)
- **Plan asks for**:
  - `sdk/Cardscape.Sdk.slnx` — a separate solution file.
  - `sdk/Cardscape.Sdk/Cardscape.Sdk.csproj` — multi-target `netstandard2.0` + `net8.0`.
  - Hand-written typed client (the plan chose hand-written over Kiota).
  - `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`, `<PackageId>Cardscape.Sdk</PackageId>`, `<Version>1.1.0</Version>`.
  - `dotnet pack` produces `Cardscape.Sdk.1.1.0.nupkg`.
- **Evidence**:
  - `sdk/Cardscape.Sdk/Cardscape.Sdk.csproj:10-27` — `<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>`, `<GeneratePackageOnBuild>true</GeneratePackageOnBuild>`, `<PackageId>Cardscape.Sdk</PackageId>`, `<Version>1.1.0</Version>`. The csproj has a header comment explaining it overrides the repository-wide `net10.0` target with a one-line `<TargetFramework></TargetFramework>` opt-out so `TargetFrameworks` wins.
  - `sdk/Cardscape.Sdk/CardscapeClient.cs`, `Models.cs`, `SubClients.cs`, `IsExternalInit.cs` — the four source files of the hand-written SDK. `SubClients.cs:86-89` includes the `api/boards/{boardId}/export` endpoint, fulfilling the SDK's "30 most-used" intent.
  - `sdk/Cardscape.Sdk/bin/Debug/Cardscape.Sdk.1.1.0.nupkg` (78 161 B) — the build artifact the plan asks for.
  - `sdk/Cardscape.Sdk/bin/Debug/Cardscape.Sdk.1.1.0.snupkg` — symbols package also produced.
  - `sdk/Cardscape.Sdk/README.md:1-30` — usage / install (`dotnet add package Cardscape.Sdk --version 1.1.0`).
  - **Drift** — **no separate `sdk/Cardscape.Sdk.slnx`** exists. The SDK project is included in the **root** `Cardscape.slnx:10-12` under a `/sdk/` folder:
    ```xml
    <Folder Name="/sdk/">
      <Project Path="sdk/Cardscape.Sdk/Cardscape.Sdk.csproj" />
    </Folder>
    ```
    The plan's intent ("new solution folder `sdk/` with a `Cardscape.Sdk.slnx`") is satisfied as a folder + project in the main solution; the dedicated `Cardscape.Sdk.slnx` is missing.
- **Notes**: structurally fine, builds and packages. The only divergence from the literal plan is the missing separate `sdk/Cardscape.Sdk.slnx`. The packaging, multi-target, and NuGet artifact all match.

---

## 5.5 Public status page

- **Verdict**: **DONE**
- **Plan asks for**: `docs/status.md` with a static table of components and a "last incident" line; document the incident-response procedure (already exists at `docs/operations/04-incident-response.md`).
- **Evidence**:
  - `docs/status.md:1-51` — page exists. Top header `# Cardscape status` (line 1), `_No incidents in the last 90 days._` placeholder (line 29) for the "last incident" line.
  - `docs/status.md:12-25` — components table with 10 rows: Web app, API, MCP server, Real-time hub, Authentication, File storage, Search, AI features, Background jobs, Database.
  - `docs/status.md:43-45` — links to `docs/operations/04-incident-response.md`.
  - `docs/status.md:47-51` — section "Subscribe" with the public RSS feed reference.
- **Notes**: the audit follow-up table at `docs/roadmap/03-execution-plan-v1.1.0.md:31` lists the status page as a known follow-up gap that was filled. This audit confirms it is in place.

---

## 5.6 Import from other kanban tools

- **Verdict**: **PARTIAL**
- **Plan asks for**:
  - New bounded context `Cardscape.Domain.Import/`.
  - `IImportService` with method `ImportAsync(Stream json, ImportTarget target, ct)`.
  - Default implementation parses Kanban `boards.json`.
  - Endpoint `POST /api/imports/kanban` (multipart, `?workspaceId=…`).
  - Web UI: `/workspaces/{id}/import` with file picker **and live preview of the parsed import**.
  - MCP tools: `imports_kanban_preview`, `imports_kanban_apply`.
- **Evidence**:
  - `src/Cardscape.Application/Abstractions/Import/IImportService.cs:14-27` — interface exists. **Drift**: the method is named `ImportKanbanJsonAsync(Stream json, Guid targetWorkspaceId, CancellationToken)` — the plan asked for `ImportAsync(Stream json, ImportTarget target, ct)`. There is no `ImportTarget` type; the workspace is taken as a `Guid` directly.
  - `src/Cardscape.Domain/Import/ImportResult.cs` — domain type returned by the service.
  - `src/Cardscape.Infrastructure/Import/KanbanImportService.cs:1-330` — concrete implementation: deserializes a Kanban `boards.json` array, walks `lists`, `cards`, `labels`, `members`, writes them into the target workspace.
  - `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:224` — `services.AddScoped<IImportService, KanbanImportService>();` is registered.
  - `src/Cardscape.Api/Endpoints/Import/ImportEndpoints.cs:14-67` — `MapGroup("/api/imports").RequireAuthorization()` exposes `POST /kanban` reading `targetWorkspaceId` + multipart `file` form parts, returns the `ImportResult` JSON on success.
  - `src/Cardscape.Web/Pages/WorkspaceImport.razor:1` — `@page "/workspaces/{id:guid}/import"`, file picker at line 19, submit at line 22. **Drift**: there is **no live preview of the parsed import** anywhere on the page; it just submits and displays the result counts (`result.ImportedBoardIds.Count`, etc.) at lines 38-41.
  - `src/Cardscape.Mcp/Tools/MissingTools.cs:105-124` — both `imports_kanban_preview` and `imports_kanban_apply` MCP tools exist. **Drift**: `KanbanApply` is a one-liner that calls `KanbanPreview` (line 124), and `KanbanPreview` directly calls `import.ImportKanbanJsonAsync(stream, targetWorkspaceId, ct)` (line 118). The XML doc at lines 111-117 explicitly says: *"The v1.1.0 IImportService has one method (ImportKanbanJsonAsync) that both previews and applies. A future PR adds a dry-run flag so the preview tool can read the parsed shape without writing to the DB."* So `imports_kanban_preview` does **not** preview — it applies.
  - `docs/community/CHANGELOG.md:133-135` — feature is announced; spec reference `docs/extensions/02-kanban-import.md` is referenced in `ImportEndpoints.cs:12` but **does not exist** (`docs/extensions/` only contains `01-build-your-own-mcp-client.md` and `README.md`).
- **Notes**: the import pipeline is functionally complete end-to-end (Kanban JSON → service → DB → API → Web UI → MCP). Two semantic drifts:
  1. The "live preview" promised by the plan does not exist — the Web UI shows the import result **after** applying, and the MCP `imports_kanban_preview` tool applies instead of previewing. The dry-run gap is acknowledged in the source comments as deferred work.
  2. The interface method name is `ImportKanbanJsonAsync` not the plan's `ImportAsync(Stream, ImportTarget, ct)`. The `ImportTarget` discriminator is gone — there is only Kanban today, so the `target` discriminator is unused in practice.

---

## 5.7 Export per-board archive

- **Verdict**: **DONE** (with minor drift)
- **Plan asks for**:
  - `Cardscape.Domain.Export/` bounded context.
  - `IExportService.ExportBoardAsync(boardId, ct)` returning a `BoardExportArchive` (a `Stream` of a ZIP with `board.json` + `attachments/`).
  - Endpoint `GET /api/boards/{id}/export` (auth required, member-only).
  - MCP tool: `boards_export`.
- **Evidence**:
  - `src/Cardscape.Application/Abstractions/Export/IExportService.cs:12-19` — interface exists with `Task<Result<Stream>> ExportBoardAsync(Guid boardId, CancellationToken ct = default);`. **Drift**: the return type is `Result<Stream>` rather than the plan's `Result<BoardExportArchive>` — there is no `BoardExportArchive` value type.
  - `src/Cardscape.Infrastructure/Export/BoardExportService.cs` — concrete implementation.
  - `src/Cardscape.Api/Endpoints/Boards/BoardEndpoints.cs:91-99` — `group.MapGet("/{boardId:guid}/export", ...)` wired to `IExportService.ExportBoardAsync`. The `group` was constructed earlier with `RequireAuthorization()` (consistent with the plan's "auth required, member-only" note).
  - `src/Cardscape.Mcp/Tools/BoardsTools.cs:412-425` — `[McpServerTool(Name = "boards_export")]` invoking `bus.InvokeAsync<Result<Stream>>(new ExportBoardQuery(boardId), ct)` and returning `byte[]` to the MCP client.
  - `sdk/Cardscape.Sdk/SubClients.cs:85-89` — the SDK's `BoardsClient` exposes `api/boards/{boardId}/export` to NuGet consumers.
  - `docs/community/CHANGELOG.md:136-137` — feature is documented in the release notes.
- **Notes**: full vertical slice ships (Application → Infrastructure → API → MCP → SDK). The only literal deviation is the return-type name `Result<Stream>` vs the plan's `Result<BoardExportArchive>`; the behavior is identical (a ZIP stream of `board.json` + `attachments/`, see `IExportService.cs:6-11`).

---

## 5.8 MCP subscriptions

- **Verdict**: **DRIFT** (the broadcaster is essentially dead code; the doc advertises a working feature that is not wired)
- **Plan asks for**:
  - Wire the existing SignalR `BoardHub` broadcaster into the MCP resource layer.
  - When a board's `board://{boardId}` resource changes, the MCP server sends a `ResourceUpdated` event to every subscribed AI client.
  - New file `src/Cardscape.Mcp/Realtime/McpResourceBroadcaster.cs`.
  - Document the flow in `docs/extensions/01-build-your-own-mcp-client.md`.
- **Evidence**:
  - `src/Cardscape.Mcp/Realtime/McpResourceBroadcaster.cs:1-106` — the file exists. The class holds a `Dictionary<Guid, List<Guid>>` of `boardId → clientSessionIds` (line 20) with `Subscribe(boardId, clientSessionId)` (line 32), `Unsubscribe(boardId, clientSessionId)` (line 49), and `BroadcastAsync(boardId, ct)` (line 77). Registered as a singleton in `src/Cardscape.Mcp/Extensions/ServiceCollectionExtensions.cs:73`.
  - `McpResourceBroadcaster.BroadcastAsync` (`McpResourceBroadcaster.cs:77-96`) is a **no-op**: it copies the subscriber list, then `logger.LogDebug(...)`s and returns `Task.CompletedTask`. The XML doc at lines 64-76 says verbatim: *"the MCP SDK that ships with Cardscape 0.7 does not expose a public `SendResourceUpdatedNotificationAsync` on the server-side `IMcpServer`, so this implementation is a placeholder that the v0.8 SDK upgrade can wire in. The subscriber list is the valuable side-effect: it gives a future implementation a single place to look up who wants a notification."*
  - **No callsite of `McpResourceBroadcaster` exists anywhere in the codebase** (grep across `D:/GitHub/Cardscape` finds the type only in its own file and in the DI registration). In particular:
    - `src/Cardscape.Mcp/Resources/McpResources.cs:1-110` defines the `McpResources` class with `[McpServerResource]` handlers (`GetWorkspace`, `GetBoard`, `GetCard`, `ListCardsOnBoard`, `ListListsOnBoard`) — **no** `[McpServerResourceSubscribe]` / `[McpServerResourceUnsubscribe]` handlers; nothing in the file references `McpResourceBroadcaster` or `Subscribe`/`Unsubscribe`.
    - The `IBoardClient` SignalR interface in `src/Cardscape.Api/Hubs/IBoardClient.cs` is the Web UI's hub; `BoardHub.cs:14-57` is a SignalR hub and the `DomainEventBroadcaster` (`src/Cardscape.Api/Realtime/DomainEventBroadcaster.cs:1-285`) fans out Wolverine domain events to SignalR groups. None of these call into `McpResourceBroadcaster`. The MCP↔API bridge for events is `BoardBroadcastEndpoints` (`src/Cardscape.Api/Endpoints/Internal/BoardBroadcastEndpoints.cs:1-336`), which is an HTTP `/api/internal/broadcast` endpoint the MCP can hit to make the API's SignalR hub broadcast — but it does **not** also notify MCP subscribers.
  - `docs/extensions/01-build-your-own-mcp-client.md:121-139` — **section "4. Subscribe to live updates" describes the feature as working**: *"Cardscape's MCP server supports resource subscriptions. When the board's `board://{boardId}` resource changes, the server fires a `ResourceUpdated` notification to every subscribed client. To subscribe: `await client.SubscribeToResourceAsync("board://<board-id>");`"* — but the server has no handler that does this; `McpResourceBroadcaster` is a placeholder; the doc contradicts the code.
- **Notes**:
  - The class scaffold is in place and the subscriber list is the right shape; the SDK call that would actually push `ResourceUpdated` to a subscribed MCP client is the only missing piece, and the source code says the v0.8 MCP SDK upgrade is the trigger for that wire-up.
  - The plan's "Wire the existing SignalR `BoardHub` broadcaster into the MCP resource layer" is not done in the strict sense — there is no plumbing from the existing `BoardHub` / `DomainEventBroadcaster` / `BoardBroadcastEndpoints` SignalR pipeline into `McpResourceBroadcaster`. The broadcaster receives no calls and emits no notifications.
  - The doc at `docs/extensions/01-build-your-own-mcp-client.md:121-139` should be downgraded to "planned" or annotated with a note that `McpResourceBroadcaster` is a stub until the MCP SDK exposes `SendResourceUpdatedNotificationAsync` (or until the project hand-rolls a JSON-RPC notification push).

---

## Summary

| § | Item | Verdict | Headline |
|---|---|---|---|
| 5.1 | i18n infrastructure | **PARTIAL** | Works for Blazor WASM, but `AddLocalization` is not configured with `SetDefaultCulture/AddSupportedCultures`, `UseRequestLocalization` is impossible on WASM, and culture is picked by `CulturePicker` rather than the `Accept-Language` header. |
| 5.2 | i18n: English + Spanish | **DONE** | 137 EN / 140 ES keys (3 ES orphans, 1 EN orphan); ~150 target met. `02-translation-workflow.md` is the file-based workflow, not the resx extraction guide the plan asked for. |
| 5.3 | PWA manifest | **DONE** | Manifest, two icons (192+512, any+maskable), service worker (network-first for API, cache-first for shell, offline `/index.html` fallback), and `index.html` references all in place. |
| 5.4 | C# API client SDK | **DONE** | `netstandard2.0;net8.0` multi-target, `PackageId=Cardscape.Sdk`, `Version=1.1.0`, `GeneratePackageOnBuild=true`, `Cardscape.Sdk.1.1.0.nupkg` produced. No separate `sdk/Cardscape.Sdk.slnx` — SDK is in the root `Cardscape.slnx` under `/sdk/`. |
| 5.5 | Public status page | **DONE** | `docs/status.md` with 10-row components table, "no incidents" line, RSS feed note. |
| 5.6 | Import from Kanban | **PARTIAL** | Pipeline ships end-to-end (Application → Infra → API → Web → MCP). Two semantic drifts: no live preview in the Web UI; MCP `imports_kanban_preview` is identical to `imports_kanban_apply` (no dry-run). |
| 5.7 | Export per-board archive | **DONE** | `IExportService` (returns `Result<Stream>` not `Result<BoardExportArchive>`), `GET /api/boards/{id}/export`, MCP `boards_export`, SDK `BoardsClient` method all in place. |
| 5.8 | MCP subscriptions | **DRIFT** | `McpResourceBroadcaster` is registered but its `Subscribe/Unsubscribe/BroadcastAsync` methods are never called; `BroadcastAsync` is a no-op; no MCP resource-subscribe handler exists; the doc advertises a working feature the code does not implement. |

### Plan checklist update

The plan has **no `- [ ]` / `- [x]` task checkboxes** in §5 — the eight items are formatted as plain bullets under their `### 5.X` headings. `Select-String` over the plan for `^- \[` returns zero hits, so there is nothing to flip on the fully-DONE items. The audit follow-up table at the top of the plan (lines 22-35) and the §6 "Delivery & release" checklist would be the right places to mark P5.3, P5.4, P5.5, and P5.7 as complete; that is out of scope for this read-only audit.

### Top gap

**5.8 MCP subscriptions (`McpResourceBroadcaster`)** is the single most important follow-up: the broadcaster is dead code today, the MCP SDK has no `resources/subscribe`/`resources/unsubscribe` handler that calls it, and `BroadcastAsync` is a logged no-op. The doc at `docs/extensions/01-build-your-own-mcp-client.md:121-139` advertises a feature that is not actually implemented. Second priority is **5.6** — the dry-run gap in `imports_kanban_preview` and the missing live-preview UI on `WorkspaceImport.razor` are both acknowledged in the source as deferred work.
