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
