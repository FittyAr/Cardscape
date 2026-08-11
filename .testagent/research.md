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
