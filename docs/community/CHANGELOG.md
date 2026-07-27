# Changelog

All notable changes to Cardscape are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

The current development line. Items here ship with the next
patch or feature release.

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
