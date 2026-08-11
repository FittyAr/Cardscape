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
