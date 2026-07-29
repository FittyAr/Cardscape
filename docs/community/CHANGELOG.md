# Changelog

All notable changes to Cardscape are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [v1.0.1] — 2026-07-29

Patch release. **One bugfix** + **one regression test**
(399/399 tests green, build clean).

### Fixed

- **OpenAPI document failed to build** — every endpoint
  class in `src/Cardscape.Api/Endpoints/**` defines its body
  type as a nested `private sealed record class` so the
  endpoint class owns the type. Multiple endpoint classes
  happened to use the same short names (`RenameBody` in
  `CardEndpoints` and `ListEndpoints`, `MoveBody` in
  `CardEndpoints` and `ListEndpoints`). Swashbuckle's default
  schemaId generator produces a `RenameBody` schemaId for
  both, then throws `InvalidOperationException: Can't use
  schemaId "RenameBody" for type
  "Cardscape.Api.Endpoints.Lists.ListEndpoints+RenameBody".
  The same schemaId is already used for type
  "Cardscape.Api.Endpoints.Cards.CardEndpoints+RenameBody"`.
  The exception surfaces as a 500 on
  `GET /swagger/v1/swagger.json` and the Swagger UI shows
  "Failed to load API definition".

  Fix: `builder.Services.AddSwaggerGen(c => c.CustomSchemaIds(
  t => t.FullName?.Replace("+", ".")))` in
  `src/Cardscape.Api/Program.cs`. The full type name
  (`Cardscape.Api.Endpoints.Cards.CardEndpoints.RenameBody`
  vs
  `Cardscape.Api.Endpoints.Lists.ListEndpoints.RenameBody`)
  is unique. The `+` → `.` rewrite is cosmetic — keeps the
  generated schemaIds looking like normal dotted type names
  in the OpenAPI document.

### Added

- **Regression test** `Swagger_Document_Builds_And_Is_Served_As_Valid_Json`
  in
  `tests/Cardscape.IntegrationTests/Endpoints/SwaggerTests.cs`.
  Hits `GET /swagger/v1/swagger.json` against the real
  `CardscapeWebApplicationFactory`, asserts 200, parses the
  body as JSON, and checks the `openapi` and `paths` keys
  are present and non-empty. Future refactors that re-introduce
  a schemaId collision will fail this test before they reach
  production.

---

## [v1.0.0] — 2026-07-29

First production release. Full Trello parity across the
kanban, calendar, planner, automation, and AI-integration
surfaces. **313 unit tests + 85 integration tests green.**
API starts clean against SQLite, PostgreSQL, or MariaDB.
Production configuration has zero hard-coded secrets.

### Phase 5 — enterprise + AI features (`v0.7.0-jobs` through `v0.7.10-polish`)

Phase 5 closed the gap between the Phase 4 surface and a
production Trello replacement. It shipped across ten
incremental pre-release tags; this v1.0.0 cuts the first
proper semver line on top of that work.

#### Background jobs infrastructure — `v0.7.0-jobs`

- **`IBackgroundJobScheduler`** in `Cardscape.Application` —
  the only public surface for queueing a job. Supports
  `EnqueueAsync(jobType, payloadJson, scheduledFor?)` and
  returns the job id.
- **`BackgroundJob` aggregate** with `Status`
  (Pending/Running/Completed/Failed/DeadLettered),
  exponential backoff (5 s base, capped at 5 min, max 5
  attempts), and a `WorkerId` claim field for atomic
  claim-and-process across instances.
- **`IBackgroundJobHandler`** contract plus the
  `ExecuteBackgroundJobCommandHandler` router that maps a
  job type string to its handler. The handler is the only
  place that touches EF Core / repositories.
- **`BackgroundJobDispatcherService`** (IHostedService)
  uses `IServiceScopeFactory` to spawn a fresh DI scope per
  tick, claims a batch atomically with `UPDATE … RETURNING`,
  and re-enqueues each job as a Wolverine
  `ExecuteBackgroundJobCommand`. The Wolverine pipeline
  invokes the handler on a worker thread.
- **REST** `GET /api/jobs?status=…&limit=…` for the
  admin / observability page; `GET /api/jobs/{id}` to
  inspect a single job.
- **MCP tools** `jobs_list`, `jobs_get`.
- **Tests** — 5 unit (scheduler contract, retry policy,
  claim atomicity) and 4 integration (dispatcher end-to-end
  through the Wolverine pipeline).

#### Custom Fields Web UI — `v0.7.1-custom-fields`

- **`CustomFieldDefinition`** + **`CustomFieldValue`**
  aggregates in `Cardscape.Domain.CustomFields/`. Per-board,
  one of `Text / Number / Date / Checkbox`. The value
  table is keyed by `(CardId, DefinitionId)` and validated
  against the definition's kind at write time.
- **REST** under `/api/boards/{boardId}/custom-fields` and
  `/api/cards/{cardId}/custom-fields`: list, create,
  rename, delete, set value, clear value.
- **MCP tools** `boards_list_custom_fields`,
  `boards_create_custom_field`, `boards_rename_custom_field`,
  `boards_delete_custom_field`, `cards_set_custom_field_value`,
  `cards_clear_custom_field_value`, `cards_list_custom_field_values`.
- **Web UI** — `/boards/{id}/custom-fields` management page
  (Radzen `DataGrid` with inline edit + delete confirm);
  card detail shows the live values with kind-aware
  editors.
- **Migration** `IssueCustomFields` adds
  `custom_field_definitions` and `custom_field_values`.
- **Tests** — 25 unit (definition validation, value
  coercion, type guards) and 7 integration (lifecycle,
  RBAC, kind mismatch rejection).

#### Rate limiting — `v0.7.2-rate-limit`

- **`IRateLimiter`** in `Cardscape.Application.Abstractions.Security`
  with a token-bucket implementation in
  `Cardscape.Infrastructure.Security.RateLimiter`. Per-key
  (`ApiTokenId` or `UserId` for JWT), with `RateLimitPerHour`
  + `BurstSize` configured per API token.
- **`RateLimitMiddleware`** in `Cardscape.Api` runs after
  authentication, reads the principal's `ApiToken` claim,
  and returns `429 Too Many Requests` with
  `Retry-After` + `X-RateLimit-*` headers when the bucket
  is empty.
- **API token auth scheme registration** — the production
  `ApiTokenAuthenticationHandler` is registered in
  `Cardscape.Api` (was previously only in `Cardscape.Mcp`)
  and a `BearerPolicy` policy-scheme dispatches between
  `JwtBearer` and `ApiToken` based on the secret's shape
  (dots = JWT, no dots = base64url API token).
- **Tests** — 8 unit (limiter math, bucket refill,
  per-token isolation) and 4 integration (auth scheme
  dispatch, 429 path, headers).

#### Activity UI — `v0.7.3-activity`

- **`IActivityRepository`** with cursor pagination
  (`ActivityCursor` = base64url `unixMs|guid`) and a
  per-card variant.
- **REST** `GET /api/boards/{boardId}/activities/` and
  `GET /api/cards/{cardId}/activities/`.
- **MCP tools** `boards_list_activities`,
  `cards_list_activities`.
- **Web UI** — `/boards/{id}/activity` standalone page plus
  a section in `CardDetail.razor`. "Activity" button in
  `BoardDetail.razor` nav.
- **Tests** — 7 unit (cursor encode / decode, ordering,
  bounds) and 4 integration (end-to-end timeline).

#### Voting — `v0.7.4-voting`

- **`CardVote`** aggregate with `(CardId, UserId)` as the
  natural key. Toggle semantics — one POST flips state,
  no separate "remove" endpoint.
- **REST** `POST /api/cards/{cardId}/votes/` (toggle) and
  `GET /api/cards/{cardId}/votes/` (count + my-vote).
- **MCP tools** `cards_toggle_vote`, `cards_get_votes`.
- **Web UI** — heart button in `CardDetail.razor` header
  with the live count, red when the current user has voted.
- **Migration** `IssueCardVotes` adds `card_votes` with a
  unique `(CardId, UserId)` index.
- **Tests** — 8 unit (toggle, count, re-vote) and 5
  integration (RBAC, double-vote guard, count surfaces).

#### Checklists — `v0.7.5-checklists`

- **`Checklist` + `ChecklistItem`** aggregates. The
  `Checklist` aggregate owns its items as a child
  collection via `OwnsMany` so loading the parent loads
  the children — no separate `ChecklistItemRepository`
  lookup.
- **Eight commands**: create, rename, delete, add item,
  rename item, toggle item, delete item, plus the read
  query.
- **REST** `GET /api/cards/{cardId}/checklists/`,
  `POST …/checklists/`, `PUT /api/checklists/{id}`,
  `DELETE /api/checklists/{id}`,
  `POST /api/checklists/{id}/items/`,
  `PUT /api/checklist-items/{itemId}`,
  `DELETE /api/checklist-items/{itemId}`.
- **MCP tools** `cards_*_checklist` (8 tools).
- **Web UI** — checklist section in `CardDetail.razor`
  with `RadzenCheckBox` per item, a `RadzenProgressBar`
  per checklist, and per-row delete buttons.
- **Migration** `IssueChecklists` adds `checklists` and
  the child `checklist_items` table.
- **Tests** — 12 unit (aggregate behaviour, position
  insert, item toggle) and 6 integration (RBAC, child
  collection load, mutation paths).

#### Recurring cards — `v0.7.6-repeater`

- **`CardRecurrence`** aggregate with `IntervalDays`
  (1-365), `NextOccurrenceAt`, and `IsActive`. Unique
  per `CardId`.
- **`CardRecurrenceDispatcherService`** (IHostedService)
  polls every 5 min, fetches the batch of due
  recurrences, and enqueues a `CloneCardJob` per card.
- **`CloneCardHandler`** (`IBackgroundJobHandler`) clones
  the source card into the same list, at position
  `Max(c => c.Position.Value) + 1`, then calls
  `rule.Reschedule(clock.UtcNow.AddDays(rule.IntervalDays))`.
- **REST** `GET /api/cards/{cardId}/recurrence/`,
  `PUT …/recurrence/`, `DELETE …/recurrence/`.
- **MCP tools** `cards_get_recurrence`,
  `cards_set_recurrence`, `cards_delete_recurrence`.
- **Web UI** — section in `CardDetail.razor` with
  `RadzenNumeric` for the interval (1-365) and a save
  button.
- **Migration** `IssueCardRecurrences`.
- **Tests** — 9 unit (aggregate validation, reschedule
  math) and 5 integration (dispatcher end-to-end, clone
  position, idempotency).

#### Polish + webhooks + attachments + search — `v0.7.10-polish`

This pre-release bundled the final three feature slices
plus the production-readiness work that v1.0.0 ships on.

- **Webhooks** — per-board `WebhookEndpoint` rows with a
  SHA-256-hashed secret. `WebhookEventBroadcaster` fires
  on `card.created`, `card.moved`, `card.completed`,
  `comment.added`. `WebhookDeliveryHandler` retries with
  exponential backoff, signs each delivery with
  HMAC-SHA256, and dead-letters after 5 attempts.
  `GET/POST/DELETE /api/boards/{id}/webhooks/`,
  `GET /api/webhooks/{webhookId}/deliveries/`. MCP
  `boards_list_webhooks`, `boards_create_webhook`,
  `boards_delete_webhook`. Web `/boards/{id}/webhooks`.
- **Attachments** — `CardAttachment` rows stored in
  `Storage:LocalRoot` via the `IStorageService` interface.
  25 MB cap enforced both in Kestrel (`MaxRequestBodySize`)
  and in the multipart binding. Drag-drop on
  `CardDetail.razor`. `POST/GET/DELETE /api/cards/{id}/attachments/`.
- **Full-text search** — `ISearchIndex` with a streaming
  in-memory implementation, case-insensitive substring
  match over cards, comments, checklist items, labels,
  and activity. `SearchHitKind` enum + `SearchHit` +
  `SearchPage` DTOs. `GET /api/search?q=…&boardId=…&kind=…&page=…&pageSize=…`.
- **GitHub Actions** — `.github/workflows/ci.yml` runs
  build, format verify, unit, integration, and a separate
  coverage job that uploads the lcov artifact.
- **Production config** —
  `src/Cardscape.Api/appsettings.Production.json` has
  zero secrets. Every required value comes from an
  environment variable (`ConnectionStrings__Default`,
  `Jwt__SigningKey`, `Internal__Secret`,
  `Storage__LocalRoot`).
- **Docs** — this CHANGELOG and the README v1.0.0 status
  table are consolidated in this release.

### Cross-cutting bugfixes

- **`Result<T>.Value` supports `Result<T?>`** — added a
  `_hasValue` flag in `src/Cardscape.Domain/Common/Result.cs`
  so `Result.Success<CardRecurrenceDto?>(null)` does not
  throw when `.Value` is accessed. Required by the
  recurring-cards slice.
- **API token auth scheme registration** — the production
  `ApiTokenAuthenticationHandler` is now in
  `Cardscape.Api.Authentication` (was only in
  `Cardscape.Mcp`). Registered behind a
  `BearerPolicy` policy-scheme that picks the right
  handler by the secret's shape. Without this fix every
  API token call returned 401 and the rate-limit
  middleware could not identify the caller.
- **`OwnsMany` for child collections** — `Checklist.Items`
  is now an owned child collection in
  `ChecklistConfiguration` so loading a `Checklist`
  loads its items in the same query. The previous
  pattern of a separate `IChecklistItemRepository`
  silently left the items collection empty.
- **All mutation handlers call `uow.SaveChangesAsync(ct)`**
  — the checklist handlers (`RenameChecklist`,
  `AddChecklistItem`, `ToggleChecklistItem`,
  `DeleteChecklistItem`) were missing the save. Fixed
  in this release.
- **`Position` struct mapping** — every `Position` value-
  object field is now `HasConversion(p => p.Value, v =>
  Position.From(v))`, including the new
  `ChecklistItem.Position` and the previously missed
  `Card.Position`.

---

## [v0.6.4-extensions] — 2026-07-27

Phase 4, last slice. Per-board extension toggles (Custom Fields,
Voting, Card Repeater) with opaque JSON config — the first move
from the Trello power-ups playbook without committing to a full
plugin loader.

### Added

- **`BoardExtension` aggregate** (`src/Cardscape.Domain/Boards/`)
  with `(BoardId, ExtensionKind)` as the natural key, an opaque
  `ConfigJson` blob, and an `IsEnabled` flag. `ExtensionKind`
  enum: `CustomFields=0`, `Voting=1`, `CardRepeater=2`. Aggregate
  methods `Enable`, `Disable` (idempotent failure), and
  `UpdateConfig` (validates JSON shape and a 8KB size cap).
- **`IBoardExtensionRepository`** with `ListForBoardAsync` and
  `GetByBoardAndKindAsync`; both use `AsAsyncEnumerable` for the
  strongly-typed-id filter (the EF Core 10 / HasConversion trap).
- **REST** under `/api/boards/{boardId}/extensions`:
  - `GET /` — list all extensions for a board (members only).
  - `POST /` — enable; idempotent if the same kind already
    exists (re-enables + optionally updates the config).
  - `DELETE /{kind}` — disable (idempotent failure if already
    off; 404 if never enabled).
  - `PUT /{kind}/config` — replace the config JSON of an
    enabled extension.
- **MCP tools** `boards_list_extensions`,
  `boards_enable_extension`, `boards_disable_extension`,
  `boards_update_extension_config`.
- **Web UI** `/boards/{id}/extensions` (Radzen `Card` toggles
  per kind, no nav link from the global menu — reached from a
  board context).
- **DB** — new table `board_extensions` with unique
  `(BoardId, Kind)` index, migration `IssueBoardExtensions`.
- **Tests** — 10 unit (aggregate + config validation) and 7
  integration (lifecycle, idempotency, RBAC, 404/400 paths).
- **Bugfix** — `BoardRepository.GetWithMembersAsync` had
  regressed to the `EF.Property<Guid>(b, "Id")` form, which
  collides with the `HasConversion` pipeline and throws
  `InvalidCastException: Object must implement IConvertible`
  at materialization. Restored the safe `b.Id == id` form.
  Same trap also required `AsAsyncEnumerable` in the new
  `BoardExtensionRepository` for the typed `BoardId.Value`
  filter.

---

## [v0.6.3-automation] — 2026-07-27

Phase 4, third slice. Board-scoped automation rules that react
to card domain events (move / complete / reopen / created in
list) and run server-side actions (move / assign / set due date
/ mark complete) against the same card. Best-effort: a failed
rule never blocks the originating request.

### Added

- **`BoardAutomationRule` aggregate** with `AutomationTrigger`
  (4 values) and `AutomationAction` (4 values) enums.
  Validation: name required + ≤120 chars, `MoveCardToList`
  requires a target list id argument, `CardCreatedInList`
  requires a trigger list id.
- **`AutomationDispatcher`** — 4 static Wolverine handlers
  (`CardMoved`, `CardCompleted`, `CardReopened`, `CardCreated`)
  that resolve the board id from the source list id, fetch
  enabled rules for the board, and execute matching actions
  via the card domain methods. Try/catch around the whole
  action so a rule failure never surfaces to the user.
- **REST** under `/api/boards/{boardId}/automation`:
  list, create, enable, disable, delete.
- **MCP tools** `automation_list_rules`,
  `automation_create_rule`, `automation_enable_rule`,
  `automation_disable_rule`, `automation_delete_rule`.
- **Web UI** `/boards/{id}/automation` — rule list with
  per-row toggle + delete, and a create form with
  `RadzenDropDown` for trigger and action.
- **DB** — new table `board_automation_rules`, migration
  `IssueBoardAutomationRules`.
- **Tests** — 6 unit + 6 integration (lifecycle, RBAC, dispatcher
  end-to-end via the card commands).

### Bugfix

- The Razor page class `Automation.razor` was renamed from the
  injected `IAutomationApiClient` to avoid name shadowing with
  the `Cardscape.Application.Automation` namespace.

---

## [v0.6.2-planner] — 2026-07-27

Phase 4, planner slice. Swimlane roadmap view of every due-date
card on a board. Pure read-only Web UI — reuses the calendar
query, no new endpoints.

### Added

- `/planner` page: one row per list as a swimlane, weeks of
  the month as columns, cards positioned by day-of-month %.
  Completed cards struck through.

---

## [v0.6.1-calendar] — 2026-07-27

Phase 4, calendar slice. Month grid view of due-date cards,
optionally scoped to a single board.

### Added

- `ListCardsDueInRangeQuery(From, To, BoardId?)` plus
  `CalendarEntryDto` and `ICardRepository.ListDueInRangeForBoardAsync`.
- New `IBoardListRepository.ListBoardIdsByListIdAsync` returns
  the `(listId -> boardId)` map in one pass (avoids N+1).
- **REST** `GET /api/cards/calendar?from=...&to=...&boardId=...`
  (cross-board when `boardId` is omitted).
- **MCP tool** `cards_calendar`.
- **Web UI** `/calendar` — month grid, prev/next navigation,
  today highlight, completed cards struck through.
- **Tests** — 4 integration (range filtering, board scoping,
  empty range).

---

## [v0.6.0-inbox] — 2026-07-27

Phase 4, inbox slice. In-app notification inbox with a 60s
polling bell in the global nav.

### Added

- `Notification` aggregate already existed; this slice adds
  the per-user inbox + auto-creation on card assignment.
- **Auto-create** on `AssignCardCommand` (skipped for self-
  assign; payload contains cardId, cardTitle, assignedBy,
  boardId).
- **MCP tools** `inbox_list`, `inbox_unread_count`,
  `inbox_mark_read`, `inbox_mark_all_read`.
- **Web UI** `/inbox` (Radzen `SelectBar` filter All/Unread,
  mark-all-read button, individual mark-read, clickable card
  link to detail) and `Shared/InboxBell.razor` widget in
  `NavMenu` with unread badge and 60s poll.
- **Bugfix** — `NotificationRepository.ListForUserAsync` had to
  switch from a SQL `ORDER BY CreatedAt` (SQLite cannot order
  on `DateTimeOffset` columns) to `AsAsyncEnumerable` +
  client-side sort.
- **Tests** — 6 unit + 4 integration (lifecycle, unread count,
  auto-creation on assign).

---

## [v0.5.0-invitations] — 2026-07-27

Phase 4, first slice. Workspace member invitations: owner
mints by email, invitee redeems a one-shot token, MCP server
exposes the same surface to AI clients.

### Added

- **`WorkspaceInvitation` aggregate** (`src/Cardscape.Domain/
  Workspaces/`): 32-byte random cleartext token (base64url),
  SHA-256 hash persisted, 10-char display prefix kept in the
  cleartext so the UI can show the prefix after the fact. 14-
  day default expiry, max 60 days. Email is normalized
  (trimmed + lowercased) at issue time. `Issue`, `Accept`,
  `Revoke` domain methods with idempotent failure modes.
  Domain events `WorkspaceInvitationIssued`, `Accepted`,
  `Revoked`.
- **`IInvitationService`** in Application owns the
  `RandomNumberGenerator` + base64url + SHA-256 secret
  pipeline. `IssueAsync` mints and persists; `ValidateAsync`
  hashes the cleartext, looks it up, and distinguishes
  `expired` / `revoked` / `already_accepted` with specific
  error codes so the UI can render the right message.
- **`IInvitationEmailService`** with a default `Console…`
  implementation (`src/Cardscape.Infrastructure/Email/`) that
  logs the invite URL. Public base URL read from
  `App:PublicBaseUrl` (default `http://localhost:5206`); in
  production the same interface is implemented by an SMTP /
  SES / SendGrid adapter.
- **`IWorkspaceInvitationRepository`** with
  `FindByTokenHashAsync` (the accept hot path),
  `ListForWorkspaceAsync` (members page, optional include
  terminal rows), and `ListPendingForEmailAsync` (inbox).
  The email query streams client-side via
  `AsAsyncEnumerable` because the strongly-typed `Email`
  value object can't be translated to SQL through the
  converter (same trap as `ApiTokenRepository`).
- **REST surface** under `/api/workspaces/{id}/invitations`
  and `/api/invitations`:
  - `POST /api/workspaces/{id}/invitations` — issue
    (owner-only). Returns the cleartext token exactly once.
  - `GET /api/workspaces/{id}/invitations` — list, with
    optional `?includeTerminal=true` (owner-only).
  - `DELETE /api/workspaces/{id}/invitations/{invitationId}`
    — revoke (owner-only).
  - `GET /api/invitations/pending` — invitee-facing inbox
    (current user's email only).
  - `POST /api/invitations/accept` — redeem a cleartext
    token. Server-side email-mismatch check rejects if the
    authenticated user's email does not match the
    invitation's email (case-insensitive). Idempotent: if
    the user is already a member, returns 200 with the
    workspace DTO.
- **MCP server** gets five new tools:
  `workspaces_invite`, `workspaces_list_invitations`,
  `workspaces_revoke_invitation`, `invitations_list_pending`,
  `invitations_accept`. Same auth pipeline (API tokens) as
  the rest of the MCP surface.
- **Blazor UI**:
  - `/invitations` — read-only inbox. Lists every pending
    invitation addressed to the current user's email
    (workspace name + role + expiry). Explains the email-
    link accept flow.
  - `/invitations/accept?token=…` — the email-link landing
    page. Calls `POST /api/invitations/accept` and shows
    success or a specific error (expired, revoked, wrong
    email).
  - `/workspaces/{id}/members` — owner's members +
    invitations page. Lists current members, lists pending
    invitations (revoke button per row), and an invite form
    (email + role dropdown) that shows the cleartext token
    in a one-shot panel — copy it into the email you send.
- **Migration `IssueWorkspaceInvitations`**: new
  `workspace_invitations` table (17 columns, 3 indexes:
  unique on `TokenHash`, regular on `Email` and
  `WorkspaceId`). `RowVersion` is
  `IsConcurrencyToken().HasDefaultValue(0u)` like every
  other table.

### Changed

- **`WorkspaceRepository.GetWithMembersAsync`** simplified
  from `EF.Property<Guid>(w, "Id") == idValue` to
  `w.Id == id`. The old shadow-property form collided with
  the `HasConversion` pipeline and threw
  `Object must implement IConvertible` at materialization
  time. EF Core 10 handles the strongly-typed id end to end
  when compared directly. No production path had hit this
  yet (no handler was calling the method), but the new
  invitation handler exposes it; the integration test now
  pins the contract.

### Tests

- **`WorkspaceInvitationTests`** (13 unit tests) on the
  domain entity: email normalization, default + custom
  lifetime, hash/prefix validation, accept/revoke
  idempotency, expiry transitions, accept-after-expiry
  rejection, revoke-after-accept rejection.
- **`WorkspaceInvitationTests`** (6 integration tests) over
  the full HTTP stack: full issue → accept → membership
  flow with member-count assertion, wrong-email
  redemption, inbox filtering by current user, non-owner
  rejection, revoke-before-accept rejection, anonymous
  invite rejection.
- **Total: 231 in total** — 200 unit + 31 integration, all
  green.

---

## [v0.4.0-realtime-mcp] — 2026-07-27

Phase 4, prep slice. Cross-process real-time broadcast from
the MCP server back to the Web UI, sharing the SignalR hub
the same Web UI already listens to.

### Added

- **`/api/internal/broadcast` endpoint** in the API
  (`src/Cardscape.Api/Endpoints/Internal/BoardBroadcastEndpoints.cs`).
  Auth via `X-Internal-Secret` header, configurable through
  `Internal:Secret`. The MCP process POSTs a `{boardId?,
  listId?, cardId?, method, payload}` envelope; the API
  resolves the board (via `AsAsyncEnumerable` because the
  strongly-typed id value-object access path can't be
  translated to SQL) and dispatches the matching `IBoardClient`
  method on the `board:{boardId:N}` SignalR group. Returns
  503 if `Internal:Secret` is not configured (sandboxed dev
  safety), 401 on a wrong secret, 400 on an unknown method,
  202 on success.
- **`IBoardPushClient` + `HttpBoardPushClient`** in
  `src/Cardscape.Mcp/Realtime/`. Typed methods for every
  event the MCP can produce (`PushCardCreatedAsync`,
  `PushCardMovedAsync`, `PushCardCompletedAsync`, etc.). The
  MCP calls the push client after every successful mutating
  tool. Failures log a warning and never block the tool
  result.
- **Shared payload records** in
  `src/Cardscape.Application/Realtime/BoardEventPayloads.cs`
  (moved out of `Cardscape.Api/Hubs/IBoardClient.cs`): the
  same `CardEventPayload`, `CardMovedPayload`,
  `ListEventPayload`, `CommentEventPayload` types are used
  by both the API hub (as the parameter of `IBoardClient`
  methods) and the MCP push client (as the request body
  shape).
- **MCP tools widened** to push after success: `lists_create`,
  `cards_create`, `cards_complete`, `cards_reopen`,
  `comments_add`, and `cards_move` (looks up the source
  list id via `ICardRepository.GetByIdAsync` before invoking
  so the `FromListId` field is correct). `workspaces_*` and
  `boards_*` don't push because they don't change board
  state from the realtime-consumer's point of view.
- **MCP DI**: `AddHttpClient("Cardscape.Api", baseUrl:
  config["Cardscape:ApiBaseUrl"])` + a singleton
  `IBoardPushClient` registration in
  `src/Cardscape.Mcp/Extensions/ServiceCollectionExtensions.cs`.
  `appsettings.json` documents the commented
  `Cardscape:ApiBaseUrl` and `Internal:Secret` placeholders
  so a developer can copy them into a real
  `appsettings.Development.json` (the secret is a per-
  environment value and is not committed).

### Tests

- **`BoardBroadcastEndpointTests`** (5 integration tests):
  unconfigured (503), wrong secret (401), unknown method
  (400), happy path with explicit `boardId` (202),
  `listId`-resolver path.
- **Total: 212** — 187 unit + 25 integration, all green.

### Security

- **Cross-process broadcast requires a shared secret**. The
  endpoint refuses to start serving if `Internal:Secret` is
  empty (returns 503). Production deployments must set
  `Internal:Secret` to the same value in both the API and
  the MCP server. Documented in the new
  `docker-compose.yml` env block; the secret is not
  committed.

---

## [v0.3.0-api-tokens] — 2026-07-27

Phase 3. Long-lived API tokens, access-control hardening on
cards/lists, and a user-facing token management page.

### Added

- **`ApiToken` aggregate** (`src/Cardscape.Domain/Security/`):
  long-lived, user-owned bearer tokens. Stores SHA-256 hash of
  the cleartext secret, the 8-char display prefix, granted
  scopes (read/write), optional expiry, and last-used/revoked
  audit fields. Two value objects: `ApiTokenName` (1..80
  chars) and `ApiTokenScopes` (a `Scope` enum with `Read` and
  `Write`). Domain methods `IsActive(now)`, `RecordUse(at)`,
  `Revoke(by, reason, at)`. Domain events `ApiTokenIssued` and
  `ApiTokenRevoked`.
- **`IApiTokenService` + `IApiTokenRepository`**: service owns
  secret generation (`RandomNumberGenerator` + base64url +
  SHA-256); repository exposes `FindByHashedSecretAsync` (the
  MCP hot path) and `ListForUserAsync`. Returns a `Result<>` on
  validation failures so the handler can map them to proper 400
  responses instead of 500s.
- **REST surface** under `/api/security/api-tokens`:
  `GET /` lists, `POST /` mints and returns the cleartext
  secret exactly once, `POST /{id}/revoke` revokes.
- **MCP server now uses API tokens** instead of JWT bearer
  (replaces the v0.2 `JwtBearerAuthenticationHandler` with
  `ApiTokenAuthenticationHandler` in
  `src/Cardscape.Mcp/Authentication/`). The cleartext secret
  travels in the standard `Authorization: Bearer <secret>`
  header; the handler hashes it and calls `IApiTokenService.
  ValidateAsync`, then builds a `ClaimsPrincipal` with the user
  id (`NameIdentifier`), the token id (`token_id`), and one
  `scope` claim per granted scope. `McpCurrentUser` now exposes
  a `Scopes` collection alongside the existing `Roles`.
- **Migration `IssueApiTokens`** adds the `api_tokens` table
  (17 columns, unique index on `HashedSecret`, regular index
  on `UserId`).
- **Web page** at `/account/api-tokens` (Blazor WASM): the
  user-facing token management surface. Lists every token
  with prefix + scopes + timestamps, mints new ones (name +
  read/write checkboxes), and shows the cleartext secret in a
  one-shot warning panel that can only be dismissed with an
  "I've copied it" click. Revoke button per row. Reachable
  via the new "API tokens" entry in the left nav.
- **`MembershipGuards`** (`src/Cardscape.Application/Common/`):
  static helpers every card and list handler now uses to
  enforce access control. `EnsureCanReadBoardAsync` allows
  members always, non-members only on public boards.
  `EnsureCanMutateBoardAsync` requires membership regardless
  of visibility (Trello model). Card / list variants resolve
  the parent board and apply the same rule.
- **`CardscapeAccessControlTests`** integration test suite
  (3 tests): outsider read on a private board returns 403 on
  cards/lists/board; outsider write on the same board returns
  403 on rename/complete/move/create-card/create-list;
  outsider read on a public board returns 200 but write still
  returns 403.
- **`ApiTokenLifecycleTests`** integration test suite
  (4 tests): full issue+list+revoke roundtrip, cross-user
  revoke rejected with 404, anonymous issue rejected with
  401, empty name rejected with 400.
- **`ApiTokenTests`** unit test suite (7 tests) on the
  domain entity: hash + prefix persistence, `IsActive` against
  revoked and expired tokens, revoke idempotency, validation
  failures (empty hash, expiry in the past).

### Changed

- **All 14 card command handlers** (Create, Rename, Description,
  Move, SetDueDate, ClearDueDate, Complete, Reopen, Archive,
  Restore, Assign, Unassign, AttachLabel, DetachLabel) now
  call `EnsureCanMutateCardAsync` after loading the card.
  `MoveCardCommand` additionally checks the destination list is
  on the same board; `AttachLabelToCardCommand` checks the
  label is on the same board. **All 5 list command handlers**
  (Create, Rename, Move, Archive, Restore) now use the guards
  too. `CreateListCommand` swapped its inline `board.IsMember`
  call for the shared helper. **All 4 card/list query handlers**
  (`GetCard`, `GetList`, `ListCardsForBoard`,
  `ListListsForBoard`) enforce read access on private boards.
- **Every `*Configuration.cs` in the Infrastructure layer** now
  applies `IsConcurrencyToken().HasDefaultValue(0u)` to its
  `RowVersion` property (9 root + 5 owned-many configurations).
  `InitialSchema` migration regenerated to match (renamed
  from `20260727192207` to `20260727221445`; `defaultValue: 0u`
  on all 14 `RowVersion` columns). This fixes a runtime bug
  where EF Core 10 excluded the `RowVersion` column from
  INSERTs on a property marked only with `IsConcurrencyToken()`,
  tripping the SQLite `NOT NULL` constraint.

### Removed

- **`ApiTokenAuthenticationHandler` + `ApiTokenAuthenticationOptions`
  + `ICurrentUserResolver` + `McpCurrentUserResolver`**: the
  v0.2 stubs that returned `NoResult()` and threw on use.
  Replaced by the working `ApiTokenAuthenticationHandler` in
  v0.3 (same file name, new implementation backed by
  `IApiTokenService`).
- **`JwtBearerAuthenticationHandler` + `JwtBearerAuthenticationOptions`**:
  the v0.2 MCP JWT-bearer plumbing, replaced by the
  API-token handler.

### Security

- **Card and list endpoints are no longer accessible by id
  alone**. Every read and write now requires the caller to be a
  member of the card/list's parent board. Members can always
  read; non-members can only read public boards. Writes always
  require membership regardless of board visibility.
- **API tokens are SHA-256 hashed** at rest. The cleartext
  secret is generated via `RandomNumberGenerator.GetBytes(32)`
  and base64url-encoded; only the SHA-256 hash and the 8-char
  display prefix are persisted. The cleartext is returned to
  the caller exactly once at issuance and is never logged.
- **Token validation updates `LastUsedAt`** on every
  successful authentication, so users can spot stale tokens.
- **Token revocation is irreversible and idempotent**;
  revoking a token that's already revoked returns
  `security.api_token.already_revoked` (409).

---

## [v0.2.0-core-mcp] — 2026-07-27

Phase 2. Real-time board sync + the MCP server end-to-end
(the differentiator that Cardscape is built around).

### Added

- **SignalR `BoardHub`** at `/hubs/board`. Authenticated with
  the same JWT bearer scheme as the REST API. Clients join
  `board:{boardId}` on demand via the `JoinBoard` method.
  `IBoardClient` declares 14 server-to-client events:
  `CardCreated`, `CardUpdated`, `CardMoved`, `CardCompleted`,
  `CardReopened`, `CardArchived`, `CardRestored`, `ListCreated`,
  `ListRenamed`, `ListArchived`, `ListRestored`, `CommentAdded`,
  `LabelCreated`, plus a few un-implemented placeholders for
  assign/label-attach.
- **`DomainEventBroadcaster`** in the API project: a static
  set of Wolverine handlers that bridge every domain event
  raised by the command handlers to `IBoardNotifier`, which
  fans out to the matching SignalR group. The pattern is
  "command raises domain event → Wolverine bus invokes handler
  in API → handler calls hub". No new abstractions in the
  Application layer.
- **`LabelCreated` event** now carries the `Color` value
  object so the broadcaster can ship it to the hub without
  re-fetching the label.
- **`BoardHubClient`** in the Blazor WASM project. Uses
  `Microsoft.AspNetCore.SignalR.Client` 11.0-preview, brings
  the access token via the `AccessTokenProvider`, and exposes
  a C# event for every server-to-client event. `BoardDetail.razor`
  connects on parameter set, subscribes to the events that
  affect its UI, and reloads the board on each push. A "Live"
  indicator in the top-right shows connection state.
- **MCP server end-to-end** (`src/Cardscape.Mcp/`): the same
  Application + Infrastructure composition the REST API uses
  is now wired up. A JWT bearer auth scheme (matching the API)
  populates `ICurrentUser` so every tool goes through the
  same authorization pipeline. 19 MCP tools are registered
  via `[McpServerTool]` discovery:
  - `workspaces_list`
  - `boards_list`, `boards_get`, `boards_create`, `boards_star`,
    `boards_unstar`
  - `lists_list`, `lists_create`
  - `cards_list`, `cards_get`, `cards_create`, `cards_move`,
    `cards_complete`, `cards_reopen`, `cards_assign`,
    `cards_attach_label`
  - `comments_add`, `comments_list`
  - `labels_list`, `labels_create`

  Every tool is a thin adapter that calls `IMessageBus.InvokeAsync`
  on the same commands/queries the REST API uses. The MCP
  surface therefore inherits authorization, validation, and
  the `Result<T>` pattern for free.

### Changed

- **`Label` aggregate** now passes its `Color` to `LabelCreated`
  in the constructor, so the broadcaster can ship the colour
  payload to the hub without re-fetching.
- **`Cardscape.Mcp`** now references `Cardscape.Infrastructure`
  (it was Application-only) and wires up
  `AddCardscapeApplication` + `AddCardscapeInfrastructure` from
  its own composition root. The stdio transport brings the
  Application's Wolverine handlers online automatically.
- **`Newtonsoft.Json`** pinned to **13.0.3** in
  `Directory.Packages.props`. SignalR.Client 1.2.0 pulls
  11.0.2 transitively; that version has the high-severity
  advisory GHSA-5crp-9r3c-p9vr.

### Removed

- **`ApiTokenAuthenticationHandler`** + `ApiTokenAuthenticationOptions`
  + `ICurrentUserResolver` + `McpCurrentUserResolver`: the
  in-line stubs that returned `NoResult()` and threw on use.
  Replaced by `JwtBearerAuthenticationHandler` + `McpCurrentUser`
  (which implements the Application layer's `ICurrentUser`).
  The proper `ApiToken` first-class entity lands in v0.3.

### Security

- The MCP server's `Authorization: Bearer <jwt>` path is
  validated with the same key, issuer, audience, and clock
  skew as the REST API. Rejecting the token on the MCP side
  produces the same failure message as the API side.
- The `ICurrentUser.IsAuthenticated` check is enforced
  before every MCP tool call: anonymous tool calls fail
  fast with `UnauthorizedAccessException` and a clear message.

---

## [v0.1.0-mvp] — 2026-07-27

Phase 1 complete. The first self-hostable, runnable build of
Cardscape. A user can register, create a workspace, and use the
full kanban surface (boards, lists, cards, comments, labels,
starred boards, due dates) through the Blazor WASM client and
the REST API, on SQLite, PostgreSQL, or MariaDB.

### Added

- **Domain layer** (`src/Cardscape.Domain/`): 9 aggregates
  (Workspace, Board, Card, List, Comment, Label, User, Activity,
  Notification), 12 strongly-typed IDs, 11 value objects with
  `Result<T>`-returning factory methods.
- **Application layer** (`src/Cardscape.Application/`): 32
  command/query handlers, `IClock` / `IUnitOfWork` /
  `ICurrentUser` / `IPasswordHasher` / `ITokenService` /
  `IStorageService` / `IEmailService` abstractions, generic +
  typed repositories.
- **Infrastructure layer** (`src/Cardscape.Infrastructure/`):
  EF Core 10 with the three providers, single migration set,
  Pbkdf2 password hasher, JWT issuance (HS256), local storage
  service, console email service, in-memory search index.
- **API layer** (`src/Cardscape.Api/`): minimal API endpoints
  for auth, workspaces, boards, lists, cards, comments, labels,
  notifications, search, activities. JWT bearer scheme.
  ProblemDetails error envelope.
- **Web layer** (`src/Cardscape.Web/`): Blazor WebAssembly SPA
  with Radzen components. Auth (login / register / logout),
  workspace and board list pages, board detail (kanban with
  add-list / add-card), card detail (with comments). CORS
  configured for the dev ports.
- **Wolverine 6.23.1** as the in-process mediator (replacing
  MediatR).
- **Mapperly 4.3.1** as the compile-time DTO mapper.
- **FluentValidation 11.11.0** for command/query validation.
- **Tests**: 179 unit tests (domain value objects + aggregates +
  application handlers) and 10 integration tests (full HTTP
  stack via `WebApplicationFactory<Program>`).
- **Docker** multi-stage build for the API. `docker compose.yml`
  for the API + Postgres stack, `docker-compose.dev.yml` for
  the SQLite-only dev experience. Healthcheck, named volumes,
  non-root user, env-driven config.
- Initial ADRs on multi-provider strategy and the MCP server.

### Changed

- API switched from MediatR to Wolverine (see
  `src/Cardscape.Application/`). The pipeline is
  `WolverineDomainEventDispatcher` + `WolverineMessageBus`
  + `IMessage`/`ICommand`/`IQuery` records.
- RowVersion concurrency check demotes no-op `Modified`
  entities to `Unchanged` so a star/complete on a freshly-loaded
  aggregate does not produce a spurious 0-row UPDATE.

### Removed

- MediatR (`MediatR.Contracts` / `MediatR` packages and the
  `IRequestHandler<,>` / `IRequest<,>` shape).
- `MediatRDomainEventDispatcher` and the `UnitOfWorkBehavior`
  / `LoggingBehavior` MediatR pipeline behaviours (replaced by
  Wolverine middleware).

### Security

- JWT signing key defaults to a clearly-marked dev-only string;
  production deployments **must** set `Jwt__SigningKey` (or the
  matching `Jwt:SigningKey` appsettings value) to a 32+ byte
  secret.
- Passwords hashed with Pbkdf2 (HMAC-SHA256, 100k iterations,
  16-byte salt).
- CORS policy is intentionally restrictive: only the dev
  ports `5206` and `7188` are allowed, with credentials.
  Production deployments are expected to terminate TLS and
  serve the API behind a same-origin reverse proxy.

---

## [Unreleased-scaffold]

The initial pre-alpha scaffold (`v0.0.0` / Phase 0). Items
below were present before `v0.1.0-mvp`.

### Added

- Solution scaffold: 6 source projects, 5 test projects, .NET 11
  preview 6 SDK, EF Core 10.0.10 LTS.
- Multi-provider persistence scaffolding (SQLite, PostgreSQL,
  MariaDB). SQLite-only test matrix for now. See
  [ADR 0001](docs/adr/0001-multi-provider-strategy.md).
- RPL-1.5 LICENSE.
- Project-local `.agents/` folder with the working contract and
  5 skills.
- `Cardscape.Mcp` project skeleton: stdio transport,
  `ApiTokenAuthenticationHandler` placeholder, `ICurrentUser`
  resolver. No tools yet. See
  [ADR 0002](docs/adr/0002-mcp-server.md).
- `docs/` set: working contract, ADRs, architecture, development
  conventions, API conventions, feature inventory, implementation
  plan, product positioning.
- Root `README.md` (the public pitch) and
  [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md)
  (name, tagline, pillars, vocabulary, voice).
- Community files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
  `SECURITY.md`, `SUPPORT.md`, this `CHANGELOG.md`.
- GitHub issue templates (bug, feature, question) and pull
  request template.
- GitHub Discussion categories (announcements, ideas, Q&A, show
  and tell).
- Public website on the `site` branch: single-page HTML + CSS,
  no build step. Deployable to GitHub Pages, Netlify, Cloudflare
  Pages, or Vercel.

### Changed

- Repository rebranded to vendor-neutral product positioning.
  Working tree and git history rewritten to drop every
  reference to the legacy competitor and its brand names.
  See commit `289a370`.

### Removed

- `docs/roadmap/00-trello-features-analysis.md` (renamed to
  `docs/roadmap/00-feature-inventory.md` and rewritten as a
  Cardscape-voice feature inventory).
- All references to vendor-specific brand names (Butler,
  Power-Ups, Atlassian Intelligence) in code, docs, and
  commit history.

### Security

- None yet. The first security audit ships with Phase 5 (Polish
  & scale).

---

## Versioning policy

Cardscape uses Semantic Versioning with pre-1.0 caveats:

- **0.y.z** — pre-1.0, "moving fast". Minor bumps can include
  breaking changes. The project's API and schema are not
  stable until 1.0.
- **1.0.0** — first stable release. API and schema are stable
  from here. Breaking changes bump the major version.

Until 1.0, the minor version denotes a phase completion (see
[`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md)).

| Version | Phase | Status |
|---|---|---|
| `v0.0.0` | (unreleased scaffold) | not used |
| `v0.1.0-mvp` | Phase 1 — MVP | target end of August 2026 |
| `v0.2.0-core-mcp` | Phase 2 — Core + MCP server | target end of October 2026 |
| `v0.3.0-extensions` | Phase 3 — Extensions & automation | target end of December 2026 |
| `v0.4.0-enterprise` | Phase 4 — Enterprise & AI | target end of Q1 2027 |
| `v1.0.0` | (Phase 5 — Polish & scale mature) | not yet targeted |

The release process (tags, NuGet, Docker, notes) is in
[`docs/development/04-release-process.md`](docs/development/04-release-process.md).

---

## Types of changes

- **Added** for new features.
- **Changed** for changes in existing functionality.
- **Deprecated** for soon-to-be-removed features.
- **Removed** for now-removed features.
- **Fixed** for any bug fixes.
- **Security** for vulnerability fixes and security-policy
  changes.
