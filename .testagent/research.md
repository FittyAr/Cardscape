# Test research

## Bounded target inventory

- `tests/Cardscape.ArchitectureTests/ArchitectureTests.cs`: existing NetArchTest rules inspect compiled type dependencies, not the effective `ProjectReference` graph.
- `src/*/*.csproj`: seven production projects whose direct references form the architecture graph.
- `src/Cardscape.Infrastructure/BackgroundJobs/BackgroundJobHandlerRegistry.cs`: immutable registry built once from DI handlers.
- `tests/Cardscape.UnitTests`: xUnit + FluentAssertions conventions, with global imports.

## Existing conventions

- Test framework: xUnit (`[Fact]`).
- Assertions: FluentAssertions.
- Test names: `Member_Condition_ExpectedResult`.
- Architecture tests live in `Cardscape.ArchitectureTests`; infrastructure unit tests live under `Cardscape.UnitTests/Infrastructure`.

## Acceptance checklist

- [ ] Assert the complete effective direct `ProjectReference` graph for every project under `src`.
- [ ] Document and preserve the deliberate `Cardscape.Api -> Cardscape.Web` reference used to host Blazor WASM.
- [ ] Detect an invalid future project reference even when no type from it is used.
- [ ] Registry resolves a registered handler and exposes its discriminator.
- [ ] Registry rejects null/empty/whitespace handler types.
- [ ] Registry rejects duplicate types and uses ordinal discriminator identity.
- [ ] Build and run the two narrow test projects.
- [ ] Review assertion strength and behavior gaps.

## Phase 1 follow-up: Seeder authorization and hosted options validation

### Bounded target inventory

- `src/Cardscape.Api/Endpoints/Seeder/SeederEndpoints.cs`: four routes under an `AdminOnly` route group, additionally gated by `Cardscape:Seeder:Enabled`.
- `tests/Cardscape.IntegrationTests/Fixtures/CardscapeWebApplicationFactory.cs`: real API/JWT/SQLite test host and `WithWebHostBuilder` configuration seam.
- `src/Cardscape.Infrastructure/Hosting/RetentionSweeper.cs`: five retention settings and their safe defaults.
- `src/Cardscape.Infrastructure/Hosting/RevocationSweeper.cs`: sweep interval, initial delay, enabled switch and defaults.
- `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`: real bind/validate/`ValidateOnStart` registrations.

### Existing conventions

- xUnit v3 `[Fact]`/`[Theory]`, FluentAssertions, and `Member_Condition_ExpectedResult` names.
- Authorization integration tests register a user through the public API; admin tests promote the user in Development and re-login so the JWT has `is_admin=true`.
- Host option tests exercise the public composition extension and assert `OptionsValidationException.OptionsType`.

### Acceptance checklist

- [x] All four Seeder routes return 401 to anonymous callers while enabled.
- [x] All four Seeder routes return 403 to authenticated non-admin callers while enabled.
- [x] Admin can access status/options and receives 202 from run/wipe while enabled.
- [x] Retention invalid interval, negative grace period, invalid retention days, and invalid batch size fail startup validation.
- [x] Revocation sweeper zero/negative interval and negative initial delay fail startup validation.
- [x] Defaults pass startup validation and retain their exact safe values.
- [x] Narrow tests compile and pass; assertions and gaps are reviewed.

## Phase 1 follow-up: abstraction ownership

### Bounded target inventory

- `src/Cardscape.Infrastructure/Hosting/RetentionSweeper.cs`: declares `IRetentionSettings` plus an adapter that only forwards `IOptions<RetentionSettingsOptions>`.
- `src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`: registers the redundant adapter.
- `tests/Cardscape.ArchitectureTests/ArchitectureTests.cs`: intended to reject Infrastructure-owned public interfaces, but filtered out correctly named `I*` interfaces and therefore could not detect them.
- `tests/Cardscape.UnitTests/Hosting/RetentionSweeperTests.cs`: two behavioral sweeper tests use a hand-written settings stub.

### Acceptance checklist

- [x] Consume validated `IOptions<RetentionSettingsOptions>` directly and remove the redundant Infrastructure interface/adapter.
- [x] Use the injected clock consistently for retention scheduling calculations.
- [x] Make the architecture rule reject every public interface declared by Infrastructure.
- [x] Preserve anonymisation and empty-database behavior with strong assertions.
- [x] Run narrow architecture and retention tests, then the full solution.

## Phase 1 follow-up: Seeder public surface

### Bounded target inventory

- `src/Cardscape.Seeder/Steps`: `ISeedStep`, its base class and thirteen implementations were public despite having no consumers outside the Seeder assembly.
- `src/Cardscape.Seeder/Reporting/SeedReport.cs`: `ISeedReportProvider` only exposed the already-registered singleton `SeedReport`.
- `src/Cardscape.Seeder/SeedRunner.cs` and DI registration: the public constructor leaked the internal pipeline type.
- `src/Cardscape.Api/Endpoints/Seeder/SeederEndpoints.cs`: consumed the provider wrapper rather than the concrete report state it reads.

### Acceptance checklist

- [x] Keep pipeline interfaces and implementations internal to Seeder.
- [x] Remove the one-property report-provider wrapper and inject `SeedReport` directly.
- [x] Keep `SeedRunner` resolvable without exposing its internal constructor dependencies.
- [x] Add an architecture invariant rejecting public Seeder interfaces.
- [x] Preserve Seeder endpoint authorization and success behavior.
- [x] Run full Release validation and review assertions.

## Phase 1 follow-up: realtime boundary ownership

### Bounded target inventory

- `src/Cardscape.Application/Realtime/IMcpResourceNotifier.cs`: process-specific API-to-MCP HTTP notification port with no Application consumer.
- `src/Cardscape.Api/Realtime/HttpMcpResourceNotifier.cs`: sole implementation, configured and consumed only by the API composition.
- `src/Cardscape.Api/Realtime/CompositeBoardNotifier.cs`: combines the legitimate Application `IBoardNotifier` port with the API-owned MCP HTTP side effect.
- `tests/Cardscape.E2ETests/McpSubscriptionsCrossProcessTests.cs`: resolves the notifier to prove the cross-process call.

### Acceptance checklist

- [x] Remove the API-to-MCP process contract from Application.
- [x] Register and consume the concrete notifier within the API host.
- [x] Preserve the cross-process notification behavior.
- [x] Guard the exact transport-neutral public contracts allowed in `Application.Realtime`.
- [x] Run narrow architecture/E2E validation, assertion review and the full suite.

## Phase 1 follow-up: pending TOTP lifetime

### Bounded target inventory

- `src/Cardscape.Application/Authentication/Abstractions/IPendingTotpLoginStore.cs`: legitimate Application port consumed by the two-step login flow.
- `src/Cardscape.Infrastructure/Authentication/InMemoryPendingTotpLoginStore.cs`: singleton implementation with a five-minute TTL based on ambient wall-clock time.
- `src/Cardscape.Infrastructure/Authentication/RedisPendingTotpLoginStore.cs`: alternate distributed implementation whose expiration is enforced atomically by Redis.

### Acceptance checklist

- [x] Preserve the port because Application consumes it and two backends implement it.
- [x] Use the registered `IClock` in the in-memory implementation.
- [x] Prove successful consumption immediately before expiration.
- [x] Prove expiration at exactly five minutes and destructive removal of expired challenges.
- [x] Prove single-use and invalid-token behavior.
- [x] Run narrow and full Release validation with assertion review.

## Phase 1 follow-up: current-user composition

### Bounded target inventory

- `Application.CurrentUser`: shared claims-to-`ICurrentUser` mapping, composed by `AddCardscapeApplication`.
- `McpCurrentUser`: duplicate MCP mapping with an extra `Scopes` property absent from the interface and unused by consumers.
- `McpHttpContextCurrentUserAccessor`: correct MCP transport adapter existed but production did not register it.
- E2E fixture injected an API-side no-op accessor to make the MCP DI graph validate, masking the production composition gap.

### Acceptance checklist

- [x] Reuse Application's `CurrentUser` in MCP.
- [x] Register the MCP-owned `ICurrentUserAccessor` adapter in production.
- [x] Remove the duplicate current-user implementation and unused Scopes surface.
- [x] Remove the E2E-only composition workaround.
- [x] Add an architecture invariant preventing MCP from reimplementing `ICurrentUser`.
- [x] Run narrow MCP E2E and architecture validation, then the full suite.

## Phase 1 follow-up: calendar feed contract

### Bounded target inventory

- `src/Cardscape.Application/Calendar/IIcalendarService.cs`: real Application port, but its double-I name describes a technology awkwardly rather than the capability.
- `src/Cardscape.Infrastructure/Calendar/IcsCalendarService.cs`: sole RFC 5545 renderer; imported `IClock` but generated `DTSTAMP` from ambient wall-clock time.
- API endpoint, Wolverine query handler and Infrastructure DI registration consume the port.
- Existing integration tests cover authorization, media type, empty feeds and VEVENT presence, but not deterministic timestamps.

### Acceptance checklist

- [x] Rename the port to `ICalendarFeedRenderer` across all consumers.
- [x] Generate `DTSTAMP` from the registered `IClock`.
- [x] Prove exact RFC 5545 timestamp and all-day date fields for a due card.
- [x] Preserve endpoint integration behavior.
- [x] Run narrow and full Release validation with assertion review.

## Phase 2: MCP API-token scope enforcement

### Bounded target inventory

- `ApiTokenAuthenticationHandler` emits one canonical `scope` claim per token grant.
- `ApiTokenScopes` supports independent `read` and `write` grants, and the Web UI permits either combination.
- All MCP tools were callable after authentication without consuming those claims; read-only tokens could mutate data.
- ModelContextProtocol 2.0 exposes one `AddCallToolFilter` entry point before matched tool invocation.

### Acceptance checklist

- [x] Enforce scopes once in the MCP request pipeline, not in individual tools.
- [x] Require exact `read` or `write` grants; neither grant implicitly includes the other.
- [x] Deny anonymous, insufficient-scope and unclassified calls before tool execution.
- [x] Keep an explicit closed catalog and prove it exactly matches the advertised MCP tools.
- [x] Run narrow and full Release validation with assertion/gap review.

## Phase 2 follow-up: MCP read surfaces

### Bounded target inventory

- Five MCP resources execute workspace/board/card queries without checking token scopes.
- Four data-backed prompts execute card/list/notification queries without checking token scopes.
- Resource subscribe/unsubscribe handlers accept URIs without checking the independent `read` grant.
- The SDK exposes request filters for resource discovery/read, prompt discovery/render/completion and subscriptions.

### Acceptance checklist

- [x] Extract scope-claim authorization from the tool catalog into a reusable MCP-host policy.
- [x] Require exact `read` scope for resource listing/templates/read operations.
- [x] Require exact `read` scope for prompt listing/render operations.
- [x] Require exact `read` scope for prompt/resource completion suggestions.
- [x] Require exact `read` scope before subscribe/unsubscribe reaches the broadcaster.
- [x] Preserve Application membership/tenant authorization as the second authorization boundary.
- [x] Prove allow, deny and short-circuit behavior; run narrow and full validation.

### Residual isolation risk

- Subscription creation still needs URI-level membership validation, and active subscriptions need a
  revocation strategy when board membership changes. The broadcaster currently retains sessions rather
  than user identities, so this requires a dedicated identity-aware subscription block.

## Phase 2 follow-up: identity-aware MCP subscriptions

### Bounded target inventory

- `MembershipGuards.EnsureCanReadBoardAsync` is the existing Application rule for explicit user/board access.
- The MCP subscribe handler currently stores only URI + `McpServer`; it neither validates the URI nor captures user identity.
- The broadcaster sends `board://{id:N}` notifications and can re-resolve scoped repositories before fan-out.
- Subscribe URIs are not canonicalized, so equivalent GUID formats may never match broadcast keys.

### Acceptance checklist

- [x] Accept only board resource URIs and canonicalize them to the broadcaster key.
- [x] Validate the current user through the existing Application membership guard before storing a subscription.
- [x] Store the subscriber user id with the session, without exposing it in admin snapshots.
- [x] Revalidate each distinct subscriber identity before every broadcast and remove unauthorized subscriptions.
- [x] Preserve public-board read semantics from Application.
- [x] Prove URI parsing, allowed/denied membership and revocation behavior; run full validation.

## Phase 2 follow-up: MCP resource URI contract

### Bounded target inventory

- `McpResources.ExtractGuid` always reads the final path segment.
- `workspace://{id}`, `board://{id}` and `card://{id}` store the GUID in `Uri.Host`; their path is `/`.
- `cards://board/{id}` and `lists://board/{id}` store `board` in Host and the GUID in the path.
- Subscription authorization already parses the board authority independently, creating two URI interpretations.

### Acceptance checklist

- [x] Centralize URI parsing for authority-based and collection resource templates.
- [x] Validate exact scheme/authority shape, GUID, query and fragment.
- [x] Route all five resource methods through the shared parser.
- [x] Reuse the same board parser for subscription authorization.
- [x] Prove all five valid contracts and malformed/cross-scheme rejection.
- [x] Run narrow and full validation with assertion/gap review.

## Phase 2 follow-up: MCP write idempotency boundary

### Bounded target inventory

- The closed scope catalog classifies 59 advertised tools as `write`.
- Only `lists_create` and `cards_create` currently call `IdempotentToolRunner`; the remaining writes bypass replay protection.
- `CallToolRequestParams` exposes protocol-level `_meta` plus the complete argument dictionary before tool binding.
- Application already owns persistence, owner isolation, expiry, replay and conflict semantics in `IdempotencyKeyMiddleware`.
- The current request hash omits the tool name and depends on JSON property order when callers serialize ad hoc payloads.

### Acceptance checklist

- [x] Apply idempotency once at the `tools/call` boundary to every catalogued write tool.
- [x] Read the optional key from `_meta.idempotencyKey`; reads and writes without a key remain unchanged.
- [x] Canonicalize the tool name and recursively sorted arguments so equivalent JSON hashes identically.
- [x] Include the tool name so one key cannot replay a different tool with identical arguments.
- [x] Remove the two per-tool shims and their duplicated DI dependencies.
- [x] Prove replay short-circuit, payload/tool conflict, owner isolation, key validation and read bypass.
- [x] Add a composition invariant, review gaps/assertions and run narrow plus full validation.

## Phase 2 follow-up: atomic background-job claims

### Bounded target inventory

- `BackgroundJobRepository.ClaimBatchAsync` reads tracked pending rows, mutates them in memory and calls `SaveChangesAsync` inside a default deferred transaction.
- `BackgroundJob.TryClaim` changes status/attempts but does not increment the configured `RowVersion` concurrency token.
- Two dispatchers can therefore read the same candidates; completion by one scope makes the other's batch throw `DbUpdateConcurrencyException`, while simultaneous claims are not excluded by RowVersion.
- EF Core 10 `ExecuteUpdateAsync` can atomically update by converted `Id`, `Status` and original `RowVersion` across the installed SQLite/PostgreSQL/MySQL providers.
- Existing dispatcher integration tests cover lifecycle behavior but not competing repository instances.
- Static pairing automation was unavailable: the Roslyn engine requires SDK 11 file-based apps while the repo pins SDK 10, and the installed skill package omits its documented polyglot script.

### Acceptance checklist

- [x] Claim each candidate with one SQL update guarded by id, pending status and original RowVersion.
- [x] Increment attempts and RowVersion and stamp StartedAt/UpdatedAt atomically.
- [x] Return only rows whose guarded update affected exactly one record.
- [x] Preserve due-time ordering, batch size and future-job exclusion.
- [x] Prove two repositories can compete without exceptions or duplicate claims.
- [x] Prove persisted status, attempt and concurrency-token state after claim.
- [x] Review gaps/assertions, run focused repetition and full Release validation.

## Concurrent idempotency reservations (2026-08-11)

- Bounded target: `IdempotencyKeyMiddleware`, `IIdempotencyKeyStore`, the EF repository/entity configuration, the in-memory fake, and focused unit/SQLite integration tests.
- Confirmed gap: both callers execute the handler before either inserts the unique `(OwnerId, Key)` row; the loser only replays after both side effects have occurred.
- Existing schema can represent an in-progress reservation with HTTP 102 plus an empty response, then atomically complete it; no compatibility migration is required.
- Acceptance checklist: one handler execution under concurrent same-payload calls; both callers receive the winner response; different payload conflicts without executing; failed winner releases the reservation; abandoned reservations expire; SQLite proves two independent DbContexts coordinate.
- Conventions: xUnit v3 + FluentAssertions on VSTest; focused `--filter FullyQualifiedName~...` commands under SDK 10.0.302.

## Pre-production compatibility removal (2026-08-11)

- Bounded target: Board/List/Card mutation request records, Comment endpoint routing, their Web callers, and the idempotency store surface.
- Confirmed duplication: mutation DTOs accepted both canonical fields and `new*` aliases; Comments mapped canonical nested and legacy flat edit/delete routes; `IssueIdempotencyKeyCommand` and `IIdempotencyKeyStore.AddAsync` had no production consumer after atomic reservations landed.
- Acceptance checklist: one request field per mutation; no `/api/comments/{id}` mapping; Web sends only canonical shapes; obsolete idempotency command/store method and their isolated tests are removed; focused API tests and full Release validation pass.
- Conventions: xUnit v3 + FluentAssertions on VSTest under SDK 10.0.302.

## Canonical wire contract (2026-08-11)

- Bounded target: JSON enum configuration in API/Web/SDK, Search kind parsing, Board Extension route/body kinds, REST logout alias, MCP assignment alias, and the Google Calendar page route.
- Confirmed duplication: integer and named enums were both accepted; Search accepted `kind=0`; Board Extensions used numeric route segments; `/auth/logout`, `members_assign`, and `/settings/google-calendar` duplicated canonical surfaces.
- SDK defect found during the audit: subclients used `JsonContent.Create` without the configured `JsonOptions`, so enum request bodies remained numeric even after response options were strict.
- Acceptance checklist: camel-case enum names only; numeric JSON/query/route values rejected; SDK applies configured JSON options to every body; all four aliases absent; canonical alternatives remain tested.
- Static pairing automation was attempted once as required, but the installed skill package does not contain its documented analyzer script; established xUnit integration/architecture/SDK suites were paired directly.

## Fail-closed administrative authentication (2026-08-11)

- Bounded target: `AdminOnlyAuthorizationHandler`, its focused unit tests, the SAML protocol/admin route split, and normative operations/security documentation.
- Confirmed compatibility path: with `CacheAdminClaim=true`, tokens without the mandatory `is_admin` claim silently fell through to a users-table lookup and could still authorize.
- Confirmed dead surface: four minimal API SAML protocol routes returned a fallback 501 but were always intercepted by the unconditionally registered `SamlAuthenticationHandler`; only the administration routes in that endpoint module are dispatchable.
- Acceptance checklist: cached mode fails closed when the claim is absent; live-database mode remains available only when explicitly configured; the four unreachable SAML fallback mappings are removed; the actual SAML handler and administration endpoints remain; focused and full Release validation pass.
- Conventions: xUnit v3 + FluentAssertions on VSTest under SDK 10.0.302.

## Remove fictitious refresh sessions (2026-08-11)

- Bounded target: Cardscape authentication token abstraction, register/login/TOTP/external-login responses, REST auth routes, OAuth/SAML callback fragments, Web token persistence and focused unit/integration tests.
- Confirmed security defect: `/api/auth/refresh` checked only that an opaque string was present, decoded an unverified access token for identity, returned that same access token and minted another unpersisted opaque value. No server-side session, hash, rotation, replay detection or revocation existed.
- Confirmed dead client behavior: Web stored the opaque value in localStorage but had no refresh call path; expiration already required a new login.
- Decision: because backward compatibility is explicitly unnecessary, remove the fictitious feature rather than preserve an insecure contract. A future refresh-session feature must start with a persisted, hashed, single-use rotating session aggregate.
- Acceptance checklist: no `/api/auth/refresh`; auth JSON and external callback fragments expose access tokens only; `ITokenService` only issues signed access tokens; Web never stores a Cardscape refresh token; Google integration refresh tokens remain untouched; focused and full Release validation pass.
- Static pairing analyzer attempt: the skill's documented Roslyn file is not runnable under the repository's SDK 10 invocation (`dotnet run` found no project); established xUnit handler and endpoint suites are used directly.

## Canonical JWT expiration (2026-08-11)

- Bounded target: `JwtOptions`, infrastructure DI validation, `JwtTokenService`, authentication response DTOs, password/TOTP/external/SAML callbacks and focused unit tests.
- Confirmed contract drift: the JWT `exp` used configurable `AccessTokenMinutes`, while four application handlers and SAML independently advertised `now + 1 hour`; OAuth/SAML callbacks emitted `expires_at`, but Blazor never consumed it.
- Decision: remove redundant `AccessTokenExpiresAt`/`expires_at`; the signed JWT `exp` is the sole expiration contract. Validate JWT configuration at host startup instead of accepting unsafe or nonsensical lifetimes.
- Acceptance checklist: no duplicated expiration fields/calculations; configured lifetime is reflected exactly in JWT `exp`; issuer/audience/signing key/lifetime fail startup validation outside safe bounds; default settings remain valid; focused and full Release validation pass.
- Static pairing analyzer attempt: the documented Roslyn file again cannot run under the pinned SDK 10 command because no runnable project/file-based app is available; existing handler/options suites plus a focused token-service test are paired directly.
# External authentication boundary (2026-08-11)

- Scope: `ExternalLoginEndpoints`, API authentication composition, and the Blazor OAuth callback.
- Existing convention: xUnit v3, FluentAssertions, Release builds with warnings as errors.
- Static pairing analyzer was attempted once with the documented Roslyn command; SDK 10 rejected the file-based script because it is not a runnable project. Manual inspection was limited to this bounded scope.
- Defect: remote handlers had no temporary sign-in scheme; Apple reused the application callback as the middleware callback; custom state cookies were written but never validated or read.
- Acceptance checklist: external cookie is short lived; every remote provider signs into it; bearer remains the API default; Apple has a distinct middleware callback; return URLs reject external/network paths; SPA receives the validated local path.
# SCIM administration authorization boundary (2026-08-11)

- Scope: SCIM token issue/list/revoke commands and their workspace-scoped REST routes.
- Finding: issue checked only membership; list checked no caller; revoke accepted only a token id and ignored the route workspace. Any authenticated user could enumerate or revoke another workspace's credential.
- Decision: SCIM credential administration is owner-only, matching other high-impact workspace settings. Token revocation must match both route workspace and stored token workspace.
- Static pairing analyzer was attempted once; SDK 10 rejected the documented file-based Roslyn script because it is not a runnable project.
- Acceptance: outsider receives 403 for issue/list/revoke; a token cannot be revoked through another workspace route; valid owner roundtrip remains green.
# Workspace 2FA enforcement (2026-08-11)

- Scope: password login and the workspace `RequireTwoFactor` toggle.
- Finding: the flag was storage/UI-only; login never read it and still issued JWTs. Successful-login timestamps were also written before TOTP verification.
- Decision: enabling requires active TOTP for every current member; login denies JWT for inconsistent required workspaces; LastLogin is recorded only after all factors succeed.
- Static pairing analyzer was attempted once and failed because the documented Roslyn file is not a runnable project under the pinned SDK 10.
- Acceptance: incomplete enrollment returns 409 without changing the flag; required workspace without an active credential returns `auth.totp.enrollment_required`, no JWT, no LastLogin write.

# TOTP enrollment confirmation (2026-08-11)

- Scope: TOTP aggregate/service, persistence mapping, REST lifecycle, settings UI, login/workspace enforcement and focused unit/integration tests.
- Finding: enrollment persisted a credential that every consumer treated as active before the user proved possession of the authenticator secret. Recovery codes were usable immediately and the workspace policy could be enabled against an unconfirmed setup.
- Decision: persist enrollment as pending; only a valid TOTP code can activate it. Pending credentials cannot satisfy login/workspace policy, consume recovery codes or be disabled as active credentials. Re-enrollment replaces pending setup so lost setup material cannot strand an account.
- UI convention: the existing settings page already uses Radzen exclusively; add confirmation with Radzen form/input/validator/button and expose pending state in the status contract.
- Acceptance checklist: pending enrollment is not active; valid TOTP confirms exactly once; invalid confirmation preserves pending state; recovery codes cannot confirm; re-enrollment rotates a pending secret; login/workspace checks require confirmed credentials; focused and full Release validation pass.

# SAML administration tenant isolation (2026-08-12)

- Scope: `GetSamlConnectionQueryHandler`, SAML admin GET endpoint and focused integration coverage.
- Finding: configure/disable were owner-only, but GET loaded the connection directly by caller-controlled workspace id without `ICurrentUser` or workspace authorization. Any authenticated user could read another tenant's SAML configuration, including inline IdP metadata XML.
- Decision: all SAML administration operations are owner-only in Application; the endpoint remains only a transport adapter. Outsiders receive 403 and no configuration body, while the owner retains the exact projection.
- Acceptance checklist: anonymous GET remains 401; authenticated outsider GET is 403 with no metadata disclosure; owner GET is 200 with its own configuration; missing workspace/config retains truthful not-found/no-content behavior.
- Static pairing analyzer attempt: the required Roslyn file-based command cannot run under the installed SDK invocation (`dotnet run` reports no project); the existing `SamlEndpointsTests` integration pairing is used and the limitation is recorded.

# Slack workspace boundary and reconnect (2026-08-12)

- Scope: Slack workspace aggregate, connect/list/link/unlink Application handlers, REST/MCP adapters and focused integration tests.
- Findings: any workspace member could replace the Slack connection; reconnect claimed to rotate team/token data but only updated activity state; REST channel routes discarded their workspace id, allowing a valid channel mapping to be addressed through another tenant's URL.
- Decision: connecting/reconnecting Slack is owner-only; reconnect atomically replaces validated team identity and token hash without exposing cleartext; every workspace-scoped REST/MCP channel operation carries and verifies its workspace id.
- Acceptance checklist: member connect is 403; owner reconnect changes team/token prefix and preserves connection id; cross-workspace list/link/unlink is rejected without state change; same-workspace owner/member channel usage remains supported.
- Static pairing analyzer attempt: the required Roslyn command was executed once, but the pinned SDK rejected the file-based script because it found no runnable project. This is a static-pairing limitation, not evidence of line or branch coverage.
- Assertion review: the focused regressions use exact status, identity, hash, structured state and negative collection assertions; zero are assertion-free, trivial-only or self-referential.
- Release build: 0 warnings, 0 errors. Full suite: 852 passed, 0 failed, 1 skipped.

# Google Calendar OAuth boundary and fake inbound sync removal (2026-08-12)

- Scope: OAuth start/callback, Application connection establishment, REST credential surface, sync abstraction/implementation, aggregate persistence and integration coverage.
- Root causes: OAuth callback was anonymous but invoked a handler dependent on `ICurrentUser`, so a real code exchange could not preserve the initiating identity. `/connect` accepted purportedly encrypted credential material from the browser. Webhook lookup always returned an empty list and inbound event-to-card resolution always returned null, making watch/pull an advertised but non-functional feature.
- Decision: authorize the initiating user/workspace before redirect; carry user, workspace and a validated local return path in a ten-minute purpose-bound Data Protection state; complete OAuth from that protected identity. Remove `/connect`, `/watch`, `/webhook`, pull/watch contracts, dead aggregate fields and schema columns. Retain only working outbound due-date push.
- Acceptance: anonymous start is 401; outsider start is 403; successful callback without JWT persists the initiating user's connection; tampered state is 400 before external calls; external return URL falls back locally; removed routes are 404.
- Static pairing analyzer attempt: the documented Roslyn file command was executed once, but SDK 10 reported no runnable project. This heuristic could not provide line or branch coverage evidence.
- Test convention: xUnit v3 integration tests with FluentAssertions and an in-process API host; the advertised `.NET` assertion extension is absent, so repository conventions and the base catalogs were used.
- Release build: 0 warnings, 0 errors. Full suite: 853 passed, 0 failed, 1 skipped.
