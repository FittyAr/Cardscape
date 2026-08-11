# Test implementation plan

1. [x] Add `SourceProjects_HaveOnlyTheApprovedDirectProjectReferences` to parse every `src` project and compare the complete direct-reference graph, including the API-to-Web hosting exception.
2. [x] Add registry tests for resolution, empty registry, invalid empty discriminator, duplicate discriminator, ordinal matching, source snapshotting, and defensive `RegisteredTypes` snapshots.
3. [x] Build and run `Cardscape.ArchitectureTests` and the registry tests narrowly.
4. [x] Re-open tests, review gaps/assertions, and record results in `status.md`.

## Phase 1 follow-up: Seeder authorization and hosted options validation

1. [x] Add `SeederAdminEndpointTests` covering the 401/403 matrix for all four routes.
2. [x] Prove enabled-admin success with 200 for status/options and 202 for run/wipe.
3. [x] Add host-start validation theories for every invalid retention/revocation boundary and a passing-defaults test.
4. [x] Run narrow integration/unit filters, re-open assertions, and record the gap/quality review.

## Phase 1 follow-up: abstraction ownership

1. [x] Replace `IRetentionSettings`/`RetentionSettings` with direct `IOptions<RetentionSettingsOptions>` consumption.
2. [x] Replace ambient `DateTime.UtcNow` use in the sweeper with the injected `IClock`.
3. [x] Correct `Infrastructure_HasNoOrphanInterfaces` so conventional `I*` names are not accidentally excluded.
4. [x] Run the two narrow test classes and perform assertion/gap review.
5. [x] Run full Release build and suite.

## Phase 1 follow-up: current-user composition

1. [x] Replace MCP's `ICurrentUser` override with `ICurrentUserAccessor` registration.
2. [x] Delete `McpCurrentUser` and the fixture workaround.
3. [x] Update normative MCP architecture documentation.
4. [x] Add `Mcp_DoesNotReimplementCurrentUser`.
5. [x] Run architecture and MCP E2E tests narrowly.
6. [x] Run full Release build and suite.

## Phase 1 follow-up: calendar feed contract

1. [x] Rename `IIcalendarService` to `ICalendarFeedRenderer`.
2. [x] Inject `IClock` into `IcsCalendarService` and remove ambient time.
3. [x] Add a deterministic renderer test using valid domain aggregates.
4. [x] Run calendar unit and endpoint integration tests narrowly.
5. [x] Run full Release build and suite.

## Phase 1 follow-up: Seeder public surface

1. [x] Internalize `ISeedStep`, `SeedStepBase` and all concrete steps.
2. [x] Remove `ISeedReportProvider` and inject the singleton report directly.
3. [x] Encapsulate `SeedRunner` construction in `AddCardscapeSeeder`.
4. [x] Add `Seeder_DeclaresNoPublicInterfaces`.
5. [x] Run architecture and Seeder endpoint tests narrowly.
6. [x] Run full Release build and suite.

## Phase 1 follow-up: pending TOTP lifetime

1. [x] Inject `IClock` into `InMemoryPendingTotpLoginStore`.
2. [x] Replace ambient expiration/consumption time reads.
3. [x] Add boundary, single-use and invalid-token unit tests.
4. [x] Run the new test class narrowly and review assertions.
5. [x] Run full Release build and suite.

## Phase 1 follow-up: realtime boundary ownership

1. [x] Delete `IMcpResourceNotifier` from Application.
2. [x] Make `HttpMcpResourceNotifier` an API-owned concrete collaborator.
3. [x] Update API composition, composite notifier and E2E resolution.
4. [x] Add `Application_RealtimeExposesOnlyTransportNeutralContracts`.
5. [x] Run architecture and cross-process E2E tests narrowly.
6. [x] Run full Release build and suite.

## MCP API-token scope enforcement

- [x] Research SDK filter support and inventory every advertised tool.
- [x] Add a centralized closed scope policy and wire it through `AddCallToolFilter`.
- [x] Add behavioral tests for allowed, cross-scope, anonymous, case-sensitive and unknown calls.
- [x] Add an architecture test comparing the catalog with reflection-discovered tools.
- [x] Run narrow tests and pseudo-mutation/assertion review.
- [x] Run full build and full suite.

## MCP resource, prompt and subscription scopes

- [x] Inventory all non-tool MCP request surfaces and SDK filter hooks.
- [x] Extract reusable exact-scope authorization and keep tool classification separate.
- [x] Register read-scope filters for discovery, reads, prompts, completion and subscriptions.
- [x] Add focused behavioral tests for the reusable policy and real filter composition.
- [x] Review assertions/gaps, run full Release validation and update architecture plan.

## MCP subscription identity and membership

- [x] Reuse the existing explicit-user Application membership guard.
- [x] Add a board-subscription authorizer with strict URI normalization.
- [x] Capture user identity in broadcaster subscriptions and revalidate on fan-out.
- [x] Add focused tests for parsing, membership and revoked-access pruning.
- [x] Review assertions/gaps, run full validation, update plan and publish.

## MCP resource URI parsing

- [x] Reproduce .NET Uri host/path behavior for all five templates.
- [x] Add one strict shared parser and migrate resources/subscriptions.
- [x] Add data-driven valid and invalid contract tests.
- [x] Review assertions/gaps, run full validation, update plan and publish.

## MCP write idempotency boundary

- [x] Add canonical request serialization for tool name plus recursively sorted arguments.
- [x] Add a closed write-tool idempotency policy backed by Application middleware.
- [x] Compose authorization then idempotency in the single call-tool filter.
- [x] Remove `IdempotentToolRunner` and the two per-tool opt-in paths.
- [x] Add focused policy/canonicalization and real composition tests.
- [x] Review assertions/gaps, run Release validation, update documentation and publish.

## Atomic background-job claims

- [x] Add a standalone SQLite integration fixture with two independent DbContexts.
- [x] Add a concurrent-claim test asserting disjoint batches and exact persisted state.
- [x] Replace tracked mutation/save with guarded `ExecuteUpdateAsync` claims.
- [x] Align `BackgroundJob.TryClaim` with the RowVersion/UpdatedAt invariant.
- [x] Run the repository/dispatcher tests repeatedly, then the full Release suite.
- [x] Review assertions/gaps, update architecture plan and publish.

## Authenticated MCP Streamable HTTP transport

- [x] Reproduce the mismatch between stdio registration and HttpContext-based authentication.
- [x] Register the official ASP.NET Core Streamable HTTP transport in stateful mode and map `/mcp` behind authorization.
- [x] Bridge `RequestContext.User` across the SDK's nested DI scopes without shared-request leakage.
- [x] Add anonymous endpoint, nested-scope, filter composition and authenticated real-client tests.
- [x] Run full Release build/suite, review assertions, update normative documentation and publish.

## Atomic idempotency reservations

- [x] Add explicit reservation/completion/release semantics to the domain/store contract.
- [x] Reserve before invoking the handler; wait/replay for matching contenders and reject mismatched payloads.
- [x] Make the in-memory fake thread-safe and add deterministic concurrent/failure tests.
- [x] Add a real SQLite test with independent DbContexts proving one effect and one persisted completed response.
- [x] Run focused stability loops, full Release validation, assertion/gap review, update the modernization plan and publish.
