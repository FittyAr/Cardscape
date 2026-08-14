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

## Pre-production compatibility removal

- [x] Collapse Board/List/Card mutation DTOs onto canonical field names and update callers.
- [x] Remove flat legacy Comment edit/delete routes and exercise authorization through canonical nested routes.
- [x] Add a regression proving the flat Comment route is not mapped.
- [x] Delete the unused issue-idempotency command and non-atomic store insertion method.
- [x] Run focused and full Release validation, review assertions, update documentation and publish.

## Canonical wire contract

- [x] Configure API, Web, tests and SDK for named enums with numeric values disabled.
- [x] Centralize SDK request JSON content on its configured serializer options.
- [x] Replace numeric Search and Board Extension kinds with canonical names.
- [x] Remove REST logout, MCP assignment and Google Calendar navigation aliases.
- [x] Add exact positive/negative contract and architecture regressions.
- [x] Run focused/full Release validation, assertion/gap review, document and publish.

## Fail-closed administrative authentication

- [x] Remove the pre-v1.2.0 database fallback from cached admin authorization.
- [x] Change the missing-claim regression to require a fail-closed result even for a live admin row.
- [x] Remove unreachable SAML protocol fallback mappings while preserving the handler and admin endpoints.
- [x] Reconcile operational and ASVS documentation with the strict contract.
- [x] Run focused/full Release validation, assertion/gap review, update the modernization plan and publish.

## Remove fictitious refresh sessions

- [x] Collapse `ITokenService` and `AuthResponse` to the real signed access-token contract.
- [x] Remove refresh issuance from password, TOTP, external OAuth and SAML flows.
- [x] Remove `/api/auth/refresh`, its unverified JWT decoder and obsolete configuration.
- [x] Remove refresh-token parsing and localStorage persistence from Blazor Web.
- [x] Add exact HTTP regressions for response shape and removed route.
- [x] Run focused/full Release validation, assertion/gap review, update documentation and publish.

## Canonical JWT expiration

- [x] Remove `AccessTokenExpiresAt` and callback `expires_at` duplication.
- [x] Make the signed JWT `exp` the only expiration source.
- [x] Add fail-fast validation for issuer, audience, 256-bit signing key and 5-minute-to-24-hour lifetime.
- [x] Prove custom JWT expiration and invalid option boundaries with focused tests.
- [x] Run focused/full Release validation, assertion/gap review, update documentation and publish.
# External authentication boundary (2026-08-11)

- [x] Replace custom state cookies with the framework's protected authentication properties/correlation flow.
- [x] Add and configure a temporary external cookie and bind Google, Microsoft, and Apple to it.
- [x] Separate Apple's remote-handler callback from Cardscape's application callback.
- [x] Validate provider continuity and local return paths at the API boundary.
- [x] Pass the local return path to the Radzen/Blazor callback.
- [x] Add composition and return-path boundary tests.
- [x] Run focused tests and complete solution validation.
# SCIM administration authorization boundary (2026-08-11)

- [x] Make issue/list/revoke owner-only in Application.
- [x] Carry route workspace id into revoke and require token/workspace equality.
- [x] Add integration coverage for outsider IDOR attempts and cross-workspace token mismatch.
- [x] Run focused tests and pseudo-mutation/assertion review.
- [x] Run Release build and full suite.
# Workspace 2FA enforcement (2026-08-11)

- [x] Enforce workspace policy during password login before JWT issuance.
- [x] Move LastLogin persistence after successful second-factor verification.
- [x] Reject enabling the policy until every current member is enrolled.
- [x] Cover the multi-member boundary where only the owner is enrolled.
- [x] Remove storage-only/follow-up documentation.
- [x] Run focused validation and test-quality review.
- [x] Run Release build and full suite.

# TOTP enrollment confirmation (2026-08-11)

- [x] Add an explicit pending/confirmed state to the TOTP aggregate and EF model.
- [x] Make enrollment replace only pending credentials and keep active credentials protected.
- [x] Confirm enrollment only through a valid authenticator TOTP; reject recovery-code activation.
- [x] Require confirmed credentials in login, workspace policy, recovery and disable paths.
- [x] Extend the Radzen settings flow with code confirmation and pending-state recovery.
- [x] Add exact unit/integration regressions for activation, rejection, rotation and enforcement.
- [x] Run focused validation, pseudo-mutation/assertion review, Release build and full suite.

# SAML administration tenant isolation (2026-08-12)

- [x] Require authenticated workspace owner inside `GetSamlConnectionQueryHandler`.
- [x] Preserve 204 for an owner whose workspace has no SAML connection.
- [x] Add exact integration regressions for anonymous, outsider and owner reads.
- [x] Verify outsider responses do not disclose inline IdP metadata.
- [x] Run focused validation, pseudo-mutation/assertion review, Release build and full suite.

# Slack workspace boundary and reconnect (2026-08-12)

- [x] Make connect/reconnect owner-only in Application.
- [x] Add a validated aggregate reconnect transition that actually rotates team/token data.
- [x] Carry workspace id through list/link/unlink commands and REST/MCP adapters.
- [x] Reject cross-workspace route/resource mismatches before mutation.
- [x] Add exact integration regressions for member denial, rotation and cross-tenant routes.
- [x] Run focused validation, pseudo-mutation/assertion review, Release build and full suite.

# Google Calendar OAuth boundary and fake inbound sync removal (2026-08-12)

- [x] Trace identity and credential data across OAuth start, callback and Application.
- [x] Require authentication/membership before redirect and protect identity/workspace/return URL with expiring Data Protection state.
- [x] Complete OAuth without relying on an absent callback JWT.
- [x] Remove the browser credential endpoint and non-functional watch/webhook/pull contracts, state and schema.
- [x] Add regressions for successful anonymous callback, authentication, tenant isolation, state tampering, local redirects and removed routes.
- [x] Run focused validation and pseudo-mutation/assertion review.
- [x] Run final Release build and full suite.

# GitHub repository-board authorization boundary (2026-08-14)

- [x] Trace board/repository identity across REST, Application, Blazor client and Radzen page.
- [x] Require an active board-repository link for pull/issue reads and PR/issue writes.
- [x] Make `boardId` mandatory in the Blazor client without a legacy overload.
- [x] Add a Radzen board selector to pull listing and validate it before the request.
- [x] Add exact integration regression for all unlinked-repository operations.
- [x] Run focused build/tests and pseudo-mutation/assertion review.
- [x] Run full suite and update documentation.
- [x] Commit and push.

# Remove non-functional Google Drive integration (2026-08-14)

- [x] Trace OAuth, credential, attachment and authorization flow end to end.
- [x] Remove Domain/Application/Infrastructure types and persistence schema.
- [x] Remove REST routes, MCP tools/scopes and DI composition.
- [x] Remove Seeder data, Blazor client/page/navigation and localization keys.
- [x] Add exact 404 regressions for every removed REST route.
- [x] Validate focused integration and architecture suites.
- [x] Run full suite, update final evidence, commit and push.
# Nested attachment/webhook route-resource boundaries (2026-08-14)

- [x] Trace parent ids from Minimal API routes through Application authorization.
- [x] Make `CardId` mandatory for attachment item messages and reject mismatches.
- [x] Make `BoardId` mandatory for webhook item messages and reject mismatches.
- [x] Add exact 404 integration regressions for download/delete/update/deliveries.
- [x] Run focused build/tests and review assertion/gap quality.
- [x] Run full suite, update modernization plan, commit and push.
