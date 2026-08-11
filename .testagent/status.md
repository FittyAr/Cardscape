# Test status

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow build/tests: completed:
  - Architecture: 11 passed, 0 failed.
  - Registry filter: 8 passed, 0 failed.
- Assertion/gap review: completed inline. Exact graph equivalence catches added, removed, and unknown projects. Registry assertions verify identity, errors, ordinal matching, empty state, source snapshotting, and defensive exposure. No weak or assertion-free generated test remains.

## Phase 1 follow-up: Seeder authorization and hosted options validation

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: completed:
  - `InfrastructureOptionsValidationTests`: 9 passed, 0 failed.
  - `SeederAdminEndpointTests`: 5 passed, 0 failed.
- Assertion/gap review: completed inline after the installed .NET extension referenced by the analysis skills could not be located. The authorization tests assert exact HTTP outcomes for every route and identity class. Startup validation asserts both exception type and options type. Pseudo-mutation review found that a no-throw-only defaults test would not detect changed default values; exact assertions for all eight defaults were added. No assertion-free, trivial-only, self-referential, or unawaited assertion remains.

## Phase 1 follow-up: abstraction ownership

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: completed — Architecture 11/11; RetentionSweeper 2/2.
- Assertion/gap review: the architecture assertion now examines all public Infrastructure interfaces instead of filtering out `I*` types. The two retention tests retain exact state assertions for eligible, ineligible, already-anonymised and non-deleted users, plus the empty database path.
- Full validation: completed — Release build 0 warnings/0 errors; suite 735 passed, 0 failed, 1 skipped.

## Phase 1 follow-up: Seeder public surface

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: Architecture 12/12; Seeder endpoints 5/5.
- Assertion/gap review: `Seeder_DeclaresNoPublicInterfaces` enumerates the compiled assembly and reports every offender. Existing endpoint tests retain exact 401/403/200/202 assertions across all routes, so direct report injection cannot silently change authorization or success semantics.
- Full validation: Release build 0 warnings/0 errors; suite 736 passed, 0 failed, 1 skipped.

## Phase 1 follow-up: realtime boundary ownership

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: Architecture 13/13; MCP cross-process E2E 5/5.
- Assertion/gap review: the new test compares the complete public interface set, so additions, removals and process-specific contracts all fail with explicit differences. `Api_Notifier_Can_Call_Mcp_Directly_Across_Processes` still asserts the recorded HTTP method, path, secret and board payload through the concrete API notifier.
- Full validation: Release build 0 warnings/0 errors; suite 737 passed, 0 failed, 1 skipped.

## Phase 1 follow-up: pending TOTP lifetime

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: `InMemoryPendingTotpLoginStoreTests` 6/6.
- Assertion/gap review: tests distinguish one tick before expiration from the exact exclusive boundary, prove destructive removal after expiration, prove single-use after success, and cover null/empty/whitespace/unknown tokens. The initial compile failure was traced to the deliberate TestCommon fake sharing the production type name and fixed with an explicit production-type alias.
- Full validation: Release build 0 warnings/0 errors; suite 743 passed, 0 failed, 1 skipped.

## Phase 1 follow-up: calendar feed contract

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: renderer unit 1/1; board export/iCalendar integration 6/6.
- Assertion/gap review: `RenderBoardAsync_WithDueCard_UsesInjectedClockAndRfc5545Dates` asserts exact DTSTAMP, DTSTART, DTEND, summary, description and calendar boundaries from valid domain aggregates. Existing integration tests retain authorization, 404, media type, empty feed and VEVENT behavior.
- Full validation: Release build 0 warnings/0 errors; suite 744 passed, 0 failed, 1 skipped.

## Phase 1 follow-up: current-user composition

- Research: completed.
- Plan: completed.
- Implementation: completed.
- Narrow validation: Architecture 14/14; MCP E2E 5/5.
- Assertion/gap review: `Mcp_DoesNotReimplementCurrentUser` inspects the compiled MCP assembly for any concrete `ICurrentUser` implementation. The E2E suite now boots both real composition roots without injecting an accessor and exercises authenticated tools plus cross-process notifications.
- Full validation: Release build 0 warnings/0 errors; suite 745 passed, 0 failed, 1 skipped.

## MCP API-token scope enforcement

- Status: complete.
- Root cause: authentication emitted scope claims, but no MCP invocation path consumed them.
- Design: one SDK call-tool filter plus an explicit deny-by-default catalog.
- Narrow validation: policy 11/11; closed-catalog invariant 1/1.
- Pseudo-mutation review: cross-scope, anonymous/null identity, case changes, unknown tools,
  removed denial and invoking `next` before authorization are killed. The filter-registration
  line itself remains covered structurally by build/composition rather than a synthetic SDK host.
- Assertion review: 12 discovered cases, no assertion-free or trivial-only tests; exception/message,
  return value, invocation count, negative side effect and deep catalog equality are all asserted.
- The referenced `.NET` extension file was absent from the installed skill package; framework
  classification used the repository's xUnit/FluentAssertions conventions directly.
- Full validation: Release build 0 warnings / 0 errors; suite 757 passed / 0 failed / 1 skipped.

## MCP resource, prompt and subscription scopes

- Status: complete.
- Confirmed bypass: authenticated write-only tokens reach every non-tool read surface because only
  `tools/call` has a scope filter.
- Narrow validation: 17/17 (tool policy, reusable scope policy and real SDK filter composition).
- Pseudo-mutation review: exact-scope comparison, authentication check, denial exception and handler
  short-circuit are killed; removing any of the eight data-bearing filters fails the composition test.
- Assertion review: no assertion-free/trivial tests; exception message, negative side effect and exact
  filter cardinality are asserted. The skill package still lacks its referenced .NET extension file,
  so xUnit/FluentAssertions classification follows repository conventions directly.
- Residual risk recorded: subscription membership and post-subscription membership revocation.
- Full validation: Release build 0 warnings / 0 errors; suite 763 passed / 0 failed / 1 skipped.

## MCP subscription identity and membership

- Status: complete.
- Chosen boundary: MCP owns session/URI state; Application owns board read authorization.
- Narrow validation: 9/9 covering canonicalization, invalid schemes/ids, member access,
  public-board access, private-board denial and revoked-member pruning before fan-out.
- Pseudo-mutation review: scheme comparison, GUID validation, empty user, membership result,
  stored subscriber identity and the broadcast recheck are killed by focused cases.
- Assertion review: no assertion-free/trivial cases; exact canonical output, exception codes,
  snapshot state and repository invocation count are asserted.
- Full validation: Release build 0 warnings / 0 errors; suite 772 passed / 0 failed / 1 skipped.

## MCP resource URI parsing

- Status: complete.
- Confirmed defect: three of five advertised resource templates cannot pass the current parser.
- Narrow validation before the final fragment case: 20/20 covering all five advertised templates,
  malformed/cross-contract inputs and shared board-subscription parsing.
- Pseudo-mutation review: authority-vs-path extraction, scheme, authority, empty GUID, extra segment,
  query and fragment checks are each killed by focused data rows.
- Assertion review: both theories use behavioral assertions (exact GUID or typed exception plus
  contract message); there are no assertion-free or truthiness-only cases.
- Final narrow validation: 21/21 after adding explicit fragment rejection.
- Full validation: Release build 0 warnings / 0 errors; suite 785 passed / 0 failed / 1 skipped.

## MCP write idempotency boundary

- Status: complete for global sequential replay coverage.
- Confirmed bypass: 57 of 59 catalogued write tools never reach the existing idempotency middleware.
- Chosen boundary: protocol `_meta.idempotencyKey` at the centralized `tools/call` filter, backed by the existing Application policy.
- Narrow validation: 12/12 covering policy behavior, real `CallToolResult` replay and behavioral filter composition.
- Pseudo-mutation review found and closed the missing write-without-key bypass case. Classification,
  canonical ordering, tool-name isolation, owner isolation, validation and handler short-circuit mutations are killed.
- Assertion review: no assertion-free, trivial-only or self-referential tests. Equality/deep result,
  exception, negative invocation and persisted-state assertions cover independent effects.
- The analysis skill package advertises `extensions/dotnet.md` but does not include it; framework
  classification therefore used the repository's xUnit/FluentAssertions conventions directly.
- Residual risk recorded at that point: concurrent first-use requests could both execute before the
  unique response insert. Resolved by the later "Atomic idempotency reservations" block below.
- Full validation: Release build 0 warnings / 0 errors; final suite run 796 passed / 0 failed / 1 skipped.
- The first full run exposed a transient `DbUpdateConcurrencyException` in
  `BackgroundJobRepository.ClaimBatchAsync`; the failed test passed alone and the complete rerun passed.
  This unrelated claim race is recorded for the next background-jobs block rather than hidden.

## Atomic background-job claims

- Status: complete.
- Evidence: full-suite `DbUpdateConcurrencyException` at `BackgroundJobRepository.ClaimBatchAsync` line 75; isolated lifecycle test passes because no competing claimant remains.
- Root cause: default transactions do not serialize the read/mutate/save sequence, and `TryClaim` leaves RowVersion unchanged.
- Focused validation: 6/6 repository + dispatcher lifecycle tests; the three new claim tests pass.
- Stability validation: the concurrent repository class passed 5 consecutive runs (10/10 test executions).
- Pseudo-mutation review: ordering/batch/future boundaries, persisted increments, `affected == 1`,
  RowVersion stale-snapshot rejection and duplicate claim mutations are killed. The status predicate is
  defensive redundancy when every state transition increments RowVersion.
- Assertion review: no assertion-free, trivial-only or self-referential tests. Assertions cover exact id/time,
  persisted state, collection cardinality/uniqueness, negative future-job state and stale-claim emptiness.
- The installed analysis extension package again lacks its advertised `.NET` reference; xUnit and
  FluentAssertions were classified from repository conventions.
- Full validation: Release build 0 warnings / 0 errors; suite 799 passed / 0 failed / 1 skipped.
- Provider evidence: concurrency behavior ran against real SQLite. The shared EF expression compiles
  with all installed providers; PostgreSQL/MariaDB runtime SQL translation remains CI matrix evidence.

## Authenticated MCP Streamable HTTP transport

- Status: complete.
- Confirmed defect: stdio requests never traversed the ASP.NET authentication handler, so its environment-token fallback could not create a principal.
- Chosen boundary: stateful Streamable HTTP at `/mcp`; stateful mode preserves resource subscriptions and unsolicited update notifications.
- Identity bridge: `RequestContext.User` is copied into an `AsyncLocal` carrier so nested tool scopes see the caller while concurrent async flows remain isolated.
- Focused validation: composition 3/3; MCP E2E 7/7, including anonymous 401 and a real read-token `workspaces_list` call.
- Pseudo-mutation review: removing endpoint authorization, request principal transfer, cross-scope carrier, exact scope, bearer header, or stateful route mapping is killed by focused tests.
- Assertion review: exact 401, successful protocol call, principal identity, handler invocation count, idempotent replay and filter ownership are asserted; no assertion-free or truthiness-only generated case remains.
- Full validation: Release build 0 warnings / 0 errors; suite 802 passed / 0 failed / 1 skipped.

## Atomic idempotency reservations

- Status: research complete; implementation in progress.
- Root cause: the unique constraint arbitrates response persistence only after every contender has already executed its handler.
- Focused validation: Application + REST 14/14; SQLite 1/1 and 5/5 repeated stability runs.
- Pseudo-mutation review: reservation-before-handler, matching wait/replay, different-payload conflict, release-on-failure, exact lease boundary, REST replay header/status/body, and persisted completion mutations are killed. Typed unique detection is runtime-proven on SQLite; PostgreSQL/MariaDB codes compile but remain outside the current CI matrix.
- Assertion review: 6 generated tests use equality/deep collection, exception, negative invocation, state transition, persisted state, HTTP status/header/body and cardinality assertions. No assertion-free, trivial-only, tautological or unawaited assertion remains.
- The analysis extension catalog advertises `extensions/dotnet.md`, but that file is absent from the installed skill package; xUnit/FluentAssertions classification used repository conventions directly.
- Full validation: Release build 0 warnings / 0 errors; suite 808 passed / 0 failed / 1 skipped.

## Pre-production compatibility removal

- Status: complete.
- Removed dual mutation request fields, flat Comment routes, and the superseded non-atomic idempotency insertion path.
- Regression evidence: `Legacy_Comment_Route_Is_Not_Mapped`; existing comment IDOR tests now exercise the canonical card-scoped edit/delete routes.
- SDK evidence: `Boards_Rename_Async_Posts_Only_The_Canonical_Name_Field` and `Cards_Move_Async_Posts_The_Expected_Body` assert canonical values and the absence of `new*` aliases.
- Pseudo-mutation/assertion review: remapping the flat route, changing canonical property names, or restoring aliases is killed by exact path/value/negative-property assertions; no generated test is assertion-free, trivial-only, tautological or unawaited. The advertised `.NET` analysis extension is absent from the installed skill package, so xUnit/FluentAssertions classification used repository conventions.
- Focused validation: SDK 7/7; API route/access tests 16/16.
- Full validation: Release build 0 warnings / 0 errors; suite 806 passed / 0 failed / 1 skipped.

## Canonical wire contract

- Status: complete.
- Added strict enum, removed-alias, canonical SDK serialization, MCP catalog and Blazor route regressions.
- Removed the duplicated `ActivityDto.KindName`; `Kind` is now a named enum across Application and Web.
- Focused evidence: SDK 8/8; integration contract/extensions/revocation 15/15; architecture 6/6; residual enum consumers 48/48.
- Pseudo-mutation review: enabling numeric enum values, accepting numeric Search/Extension kinds, remapping REST/MCP/Blazor aliases, bypassing SDK options, or restoring `KindName` is killed by exact status/body/path/catalog/route/value assertions and compilation of the typed consumers.
- Assertion review: generated tests cover equality, string content, negative collection membership, JSON value kind and exact cardinality; none is assertion-free, trivial-only, tautological or unawaited. The advertised `.NET` analysis extension is absent from the installed package, so xUnit/FluentAssertions classification used repository conventions.
- Full validation: Release build 0 warnings / 0 errors; suite 814 passed / 0 failed / 1 skipped.

## Fail-closed administrative authentication

- Status: complete.
- Security boundary: cached authorization now requires the token-minted `is_admin` claim and never consults persistence when that claim is absent or false.
- Configuration boundary: `CacheAdminClaim=false` deliberately retains the live users-table decision for immediate revocation deployments; this is an active posture, not a compatibility fallback.
- SAML boundary: the authentication request handler is the sole owner of `/saml/{slug}/*`; minimal API retains only authenticated workspace administration routes.
- Focused validation: `AdminOnlyAuthorizationHandlerTests` 7/7; `SamlEndpointsTests` 3/3; Release build 0 warnings / 0 errors.
- Pseudo-mutation review: restoring the missing-claim DB lookup is killed by `CacheEnabled_ClaimMissing_FailsClosedEvenWhenDatabaseGrantsAdmin`; claim true/false and explicitly configured live lookup remain independently covered.
- Assertion review: the changed regression asserts the authorization outcome against an intentionally contradictory admin row; it is neither assertion-free, tautological nor dependent on implementation details.
- Full validation: Release suite 814 passed / 0 failed / 1 skipped.
