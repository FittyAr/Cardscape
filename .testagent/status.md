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
