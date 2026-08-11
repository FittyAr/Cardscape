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
