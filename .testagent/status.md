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

## Remove fictitious refresh sessions

- Status: complete.
- Root cause: a generated opaque value was never associated with a user or persisted, so the endpoint substituted an unverified access-token payload for server-side session identity.
- Chosen boundary: access-token-only authentication with explicit re-login; no compatibility DTO, endpoint, callback field or browser storage remains.
- Focused validation: authentication handlers 15/15; Auth/OAuth/SAML integration flows 13/13; Release build 0 warnings / 0 errors.
- Regression evidence: `Register_Then_Login_Returns_Token_For_Same_User` asserts both register and login JSON omit the two refresh fields; `Refresh_Route_Is_Not_Mapped` asserts exact 404; `FullHandshake_Issue_Exchange_UserInfo_Revoke` rejects an OAuth `refresh_token` extension field.
- Pseudo-mutation review: restoring the route, either auth response property, or the OAuth placeholder is killed by exact negative route/property assertions. Removing access-token issuance remains covered by positive non-empty token and full handshake assertions.
- Assertion review: the new checks use exact HTTP status, negative JSON property membership and positive protocol behavior; none is assertion-free, tautological or truthiness-only.
- Full validation: Release suite 815 passed / 0 failed / 1 skipped.

## Canonical JWT expiration

- Status: complete.
- Root cause: the token abstraction exposed only the serialized JWT while response builders independently guessed its expiration.
- Chosen boundary: no duplicate response metadata; consumers rely on the signed `exp` claim and hosts reject invalid JWT configuration before serving traffic.
- Host ownership: shared Infrastructure validates issuer/audience/lifetime; only the API host requires the HMAC signing secret, so MCP/Seeder do not receive an unnecessary secret.
- Focused validation: JWT/options 16/16; auth endpoint 6/6; cross-process E2E 7/7; Release build 0 warnings / 0 errors.
- Regression evidence: `IssueAccessToken_UsesConfiguredLifetimeForSignedExpiration` asserts exact 17-minute `nbf`/`exp`, issuer and audience; `JwtOptions_WithInvalidValue_FailStartupValidation` covers empty issuer/audience and both lifetime bounds; the two `AddApiAuthentication_*` tests cover missing/short production keys.
- Pseudo-mutation review: hardcoding 60 minutes, loosening either bound, accepting blank issuer/audience, moving the signing secret back into shared Infrastructure or weakening API key checks is killed by exact token, startup and E2E assertions.
- Assertion review: exact timestamps, values, option types, exception messages and host boot behavior are asserted; no generated test is assertion-free, tautological or truthiness-only.
- Full validation: Release suite 822 passed / 0 failed / 1 skipped.
# External authentication boundary (2026-08-11)

- Implementation and validation complete: focused tests 11/11; Release build 0 warnings/0 errors; full suite 833 pass, 0 fail, 1 skip.
- Pseudo-mutation targets: changing the default bearer scheme, removing any provider `SignInScheme`, restoring Apple's conflicting callback, extending cookie lifetime, or accepting absolute/network paths must fail a named assertion.
- Assertion review: assertions inspect exact schemes, cookie policy/lifetime, callback path, and normalized output; no truthiness-only or self-referential assertions.
- Final pseudo-mutation review added exact provider-continuity cases (missing, mismatched, casing and valid). The remaining remote network handshake belongs to provider middleware and is not simulated.
- Final assertion-quality review: 3 logical test methods / 11 data executions; all have meaningful exact equality or state assertions, zero assertion-free/trivial/self-referential tests. The documented .NET extension file was absent from the installed skill package, so FluentAssertions classification was applied from the base catalog.
# SCIM administration authorization boundary (2026-08-11)

- Focused SCIM integration tests: 5/5 passing.
- Pseudo-mutation review: member-for-owner, omitted list/revoke authorization, omitted token/workspace equality, and omitted LastUsed persistence are killed by exact 403/404/state/timestamp assertions.
- Assertion review: both new tests contain meaningful HTTP equality plus persisted-state assertions; zero assertion-free, trivial-only or self-referential tests. The .NET extension file advertised by the installed analysis skill is absent, so FluentAssertions was classified from the base catalog.
- Release build: 0 warnings, 0 errors. Full suite: 835 passed, 0 failed, 1 skipped.
# Workspace 2FA enforcement (2026-08-11)

- Focused validation: 9 login/policy unit tests and 5 workspace integration tests pass.
- Pseudo-mutation review: removing workspace lookup, reversing the active-credential guard, moving LastLogin before factor completion, checking only the owner, or persisting after a rejected activation is killed by exact error/JWT/state/save-count assertions.
- Assertion review: new tests use exact error/status equality, negative JWT assertions and persisted aggregate/side-effect checks; zero assertion-free, trivial-only or self-referential tests. The .NET extension advertised by the installed analysis skill is absent, so FluentAssertions was classified from the base catalog.
- Release build: 0 warnings, 0 errors. Full suite: 838 passed, 0 failed, 1 skipped.

# TOTP enrollment confirmation (2026-08-11)

- Status: complete.
- Root cause: `/enroll` persisted a credential that login, recovery codes and workspace policy immediately treated as active without proving possession of the authenticator secret.
- Chosen lifecycle: enrollment is pending until `/confirm` accepts a valid authenticator TOTP. Pending material may be rotated; active credentials reject re-enrollment; recovery codes cannot activate or validate a pending setup.
- UI: the settings page remains Radzen-only and now includes an explicit six-digit confirmation form plus a recoverable pending-state warning.
- Focused validation: login/workspace unit tests 11/11; TOTP/workspace integration tests 8/8.
- Pseudo-mutation review: treating pending as active, accepting a recovery code for activation, omitting confirmation persistence, allowing confirmation twice, preserving a pending secret on re-enroll, or replacing an active credential is killed by exact state/status/identity assertions.
- Assertion review: exact HTTP status, pending/active flags, timestamps, counts, rotated identifiers/secrets/codes, negative JWT and persistence side effects are asserted; zero new tests are assertion-free, trivial-only or self-referential. The advertised .NET extension file is absent from the installed skill package, so xUnit/FluentAssertions were classified from repository conventions and the base catalog.
- Full validation: Release build 0 warnings / 0 errors; suite 843 passed / 0 failed / 1 skipped.

# SAML administration tenant isolation (2026-08-12)

- Status: complete.
- Root cause: `GetSamlConnectionQueryHandler` trusted a caller-controlled workspace id and queried the SAML repository without identity or workspace authorization, exposing another tenant's inline IdP metadata to any authenticated user.
- Chosen boundary: configure, read and disable are uniformly owner-only inside Application; endpoint authorization remains defense in depth and transport mapping only.
- Focused validation: `SamlEndpointsTests` 8/8, covering anonymous 401, missing workspace 404, outsider 403/no disclosure, owner 200/exact projection and owner-without-config 204/empty body.
- Pseudo-mutation review: removing authentication, workspace lookup, deleted-workspace handling or owner comparison; querying before authorization; returning a DTO on outsider/no-config paths; or mapping the wrong tenant is killed by exact status, empty-body, negative marker and structured DTO assertions.
- Assertion review: all five new regressions contain meaningful equality, structural or negative assertions; zero assertion-free, trivial-only or self-referential tests. The advertised .NET extension remains absent from the installed package, so xUnit/FluentAssertions were classified from repository conventions and the base catalog.
- Static pairing limitation: the required Roslyn file-based analyzer was attempted once and SDK execution reported no runnable project.
- Full validation: Release build 0 warnings / 0 errors; suite 848 passed / 0 failed / 1 skipped.

# Slack workspace boundary and reconnect (2026-08-12)

- Status: complete.
- Root causes: connect accepted any member instead of the workspace owner; reconnect never changed the team identity or token hash despite claiming rotation; list/link/unlink REST routes discarded their workspace route value.
- Chosen boundary: connect/reconnect is owner-only; the aggregate validates all new installation data before atomically replacing it; every REST and MCP channel command carries a workspace id and Application rejects route/resource mismatches.
- Focused validation: `IntegrationsEndpointTests` 14/14.
- Regression evidence: member connect returns 403 and leaves 204/no connection; valid reconnect preserves id while replacing exact team and token prefix; invalid reconnect returns 400 and preserves all prior fields; cross-workspace list/link/unlink return 403 and preserve the sole active source mapping.
- Pseudo-mutation review: member-for-owner, omitted reconnect assignment, partial mutation before validation, ignored route workspace on any channel operation, or mutation after mismatch is killed by exact status, identity/hash, structured state and negative collection assertions.
- Assertion review: all four new regressions have meaningful equality plus state/negative assertions; zero are assertion-free, trivial-only or self-referential. The advertised .NET extension is still absent, so xUnit/FluentAssertions were classified from repository conventions and the base catalog.
- Static pairing limitation: the required Roslyn analyzer was attempted once and the pinned SDK reported no runnable project; this heuristic could not supply coverage evidence.
- Full validation: Release build 0 warnings / 0 errors; suite 852 passed / 0 failed / 1 skipped.

# Google Calendar OAuth boundary and fake inbound sync removal (2026-08-12)

- Status: complete.
- Root causes: callback identity was lost at the anonymous OAuth boundary; direct REST accepted credential material; inbound watch/webhook/pull paths were placeholders that could never locate a connection or card.
- Chosen boundary: authenticated start plus purpose-bound ten-minute protected state; callback consumes the protected initiator identity. Only the functional outbound card due-date push remains.
- Focused validation: `IntegrationsEndpointTests` plus Google Calendar IDOR regression 16/16.
- Pseudo-mutation review: removing start authorization, membership check, state protection, protected identity, local-return validation or route removal is killed by exact 401/403/400/302/404, DTO identity/workspace and redirect assertions.
- Assertion review: all new regressions assert exact status and either redirect, error code, persisted DTO fields or negative route existence; zero are assertion-free, trivial-only or self-referential. The advertised .NET extension is absent.
- Static pairing limitation: Roslyn analyzer attempt failed because the file-based command found no runnable project under the pinned SDK.
- Full validation: Release build 0 warnings / 0 errors; suite 853 passed / 0 failed / 1 skipped.

# GitHub repository-board authorization boundary (2026-08-14)

- Status: complete; commit/push pending.
- Root causes: Blazor omitted the server-required board id, while Application treated board membership as permission to operate on any caller-supplied GitHub repository.
- Chosen boundary: a repository operation is valid only when the authenticated board member addresses an active repo link belonging to that exact board.
- Focused validation: `IntegrationsEndpointTests` 16/16; Release build 0 warnings / 0 errors.
- Regression evidence: `GitHub_Operations_ForRepositoryNotLinkedToBoard_ReturnForbidden` asserts exact 403 for pull listing, PR linking and issue creation before any external GitHub response can affect the result.
- Pseudo-mutation review: removing any of the four repo-link lookups, using a repo from another board, or allowing the UI to omit board id is detected by the 403 regression plus compile-time client/page contract.
- Assertion review: the new regression has three exact behavioral assertions and no trivial, assertion-free or self-referential checks.
- Static pairing limitation: the required Roslyn analyzer was attempted once and reported no runnable project.
- Full validation: Release build 0 warnings / 0 errors; suite 854 passed / 0 failed / 1 skipped.

# Remove non-functional Google Drive integration (2026-08-14)

- Status: complete; ready for commit and push.
- Root cause: the feature combined a placeholder UI credential, missing callback, forgeable state, unused workspace id, stale reconnect and an attachment path that authorized too late and never persisted its aggregate.
- Chosen boundary: delete the capability completely instead of maintaining an insecure facade.
- Focused validation: `IntegrationsEndpointTests` 17/17; `ArchitectureTests` 18/18; Release build 0 warnings / 0 errors.
- Regression evidence: `GoogleDrive_RemovedPlaceholderRoutes_ReturnNotFound` asserts exact 404 for picker/connect/attach. Existing page-route and MCP-catalog architecture invariants pass after removal.
- Pseudo-mutation/assertion review: restoring any removed REST mapping kills the exact 404 assertions; restoring an MCP tool without scope classification kills the deny-by-default catalog invariant; the new test has three meaningful equality assertions.
- Static pairing limitation: the analyzer was attempted once and reported no runnable project.
- Full validation: formatter clean; Release build 0 warnings / 0 errors; suite 855 passed / 0 failed / 1 skipped.
- Environment note: the sandboxed host cannot write Windows Event Log, so 12 host-validation cases surface an environmental `UnauthorizedAccessException`; the unrestricted suite passes all 560 executable unit tests.
# Nested attachment/webhook route-resource boundaries (2026-08-14)

- Status: complete; ready for commit and push.
- Root cause: item commands/queries identify only the child even though their public route declares a parent-child hierarchy.
- Required invariant: route parent id must equal the persisted child's parent id before authorization or side effects continue.
- Implementation: attachment download/delete messages now require `CardId`; webhook update/delete/delivery-list messages now require `BoardId`; all reject route-resource mismatch with indistinguishable not-found results.
- Dead contract removed: `UpdateWebhookBody.Events` and its OpenAPI rewrite were deleted because the endpoint never implemented event replacement and the Web client never sent it.
- Focused evidence: `NestedResourceBoundaryTests` 2/2. `AttachmentItemOperations_WithMismatchedRouteCard_ReturnNotFound` covers download/delete plus canonical survival; `WebhookItemOperations_WithMismatchedRouteBoard_ReturnNotFound` covers update/deliveries/delete plus canonical survival.
- Pseudo-mutation review: removing any of the five parent comparisons changes an asserted 404; delete side effects are checked by a later canonical read. Assertion review found 9 meaningful exact-status/state assertions, no assertion-free, trivial or self-referential test.
- Analysis extension limitation: the selected skill references `extensions/dotnet.md`, but that file is absent from the repository package; the xUnit/FluentAssertions classification was performed inline.
- Full validation: formatter clean; suite 857 passed / 0 failed / 1 skipped.
# Kanban import preview/apply fidelity (2026-08-14)

- Status: complete; ready for commit and push.
- Safety invariant: callers must choose preview or apply in the route; omission must never default to a write.
- Fidelity invariant: the same valid archive must produce the same preview counts regardless of persistence mode.
- REST: `/kanban/preview` and `/kanban/apply` are explicit; the unsuffixed route and `previewOnly` form flag are removed. Web selects the route; MCP continues choosing the service mode explicitly.
- Import fidelity: list and label maps are constructed in both modes; apply attaches each known `labelIds` entry to its imported card.
- Focused evidence: `KanbanImportFidelityTests` 2/2. `PreviewAndApply_ForSameArchive_ReturnMatchingCounts_AndPersistCardLabels` asserts count/sample parity, no preview IDs or board write, apply IDs and persisted `LabelCount=1`. `AmbiguousKanbanImportRoute_IsRemoved` asserts exact 404.
- Pseudo-mutation/assertion review: the regressions kill the former preview-zero-cards branch, accidental preview writes, omitted label links and restored ambiguous route. 18 meaningful assertions span equality, structural, collection, state and negative checks; none are trivial or self-referential.
- Analysis extension limitation: `extensions/dotnet.md` referenced by the selected skill is absent from the local package; xUnit/FluentAssertions classification was performed inline.
- Full validation: formatter clean; suite 859 passed / 0 failed / 1 skipped.
# Remove prohibited competitor identity (2026-08-14)

- Status: complete; ready for commit and push.
- Compatibility policy: no legacy REST route, MCP tool name, method, class, resource key or file alias is retained.
- Public replacement: `/api/imports/kanban/{preview|apply}`, `imports_kanban_{preview|apply}`, `ImportKanbanJsonAsync`, `KanbanImportService`, and `ImportKanbanFile`.
- Search evidence: case-insensitive `--hidden --no-ignore` scan reports 0 textual matches outside build outputs; recursive filename scan reports 0 matches. `.git` history is intentionally not rewritten.
- Schema evidence: the owned format uses `description`, `listId`, `labelIds`, `memberIds` and `dueDate`; the focused `KanbanImportFidelityTests` passes 2/2.
- Full validation: formatter clean; suite 859 passed / 0 failed / 1 skipped.
# Webhook secret and outbound HTTP hardening (2026-08-14)

- Status: implementation and validation complete; ready for commit and push.
- Security invariant: persisted database material must not be sufficient to forge a webhook signature.
- Network invariant: delivery never follows redirects and never buffers an unbounded response body.
- Implementation: Data Protection ciphertext is persisted; delivery unprotects it only when signing with the actual shared secret. Public webhook DTOs no longer expose a database-derived prefix.
- HTTP boundary: a named client disables redirects, streams response headers first and reads at most 4 KiB from an error body.
- Focused evidence: signature vector 1/1 and real-host security regressions 2/2.
- Pseudo-mutation/assertion review: the tests fail if signing reuses the persisted hash, if storage becomes cleartext/hash/non-decryptable, or if automatic redirects return. Assertions cover exact cryptographic equality, protected-state inequalities, round-trip equality and concrete handler configuration.
- Analysis extension limitation: `extensions/dotnet.md` referenced by the selected skill is absent from the local package; xUnit/FluentAssertions classification was performed inline.
- Final evidence: formatter completed; Release build 0 warnings / 0 errors; complete suite 862 passed / 0 failed / 1 skipped.
# SAML metadata HTTP boundary hardening (2026-08-14)

- Status: implementation and validation complete; ready for commit and push.
- Security invariant: persisted metadata URLs are revalidated immediately before a bounded, non-redirecting outbound request.
- Compatibility policy: `file://` metadata loading is deleted because configuration only admits absolute HTTP(S) URLs.
- Focused evidence: bounded-reader unit tests 3/3 and all SAML integrations 9/9.
- Pseudo-mutation/assertion review: regressions kill removal of either declared-length or streamed-length checks and detect re-enabled redirects. Assertions cover exact successful content, absent length, exception type/message and concrete primary-handler state.
- Final evidence: formatter clean; Release build 0 warnings / 0 errors; complete suite 866 passed / 0 failed / 1 skipped.
# Slack per-workspace credential repair (2026-08-14)

- Status: implementation and validation complete; ready for commit and push.
- Tenant invariant: every Slack notification authenticates with the protected credential stored on the selected Slack workspace.
- Compatibility policy: global `Integrations:Slack:BotToken`, `BotTokenHash` and public `BotTokenPrefix` behavior are removed.
- Focused evidence: HTTP service 1/1 and Slack integration slice 8/8.
- Pseudo-mutation/assertion review: regressions fail if ciphertext is unchanged/plaintext/non-decryptable, reconnect does not rotate it, invalid reconnect mutates it, or delivery uses any credential other than the workspace token. Assertions span persistence inequalities, decrypt round-trip, exact Bearer header/URI and domain success.
- Final evidence: formatter clean; Release build 0 warnings / 0 errors; complete suite 867 passed / 0 failed / 1 skipped.
# Google Calendar outbound HTTP hardening (2026-08-14)

- Status: implementation and validation complete; ready for commit and push.
- Network invariant: Google OAuth and Calendar clients never follow redirects and never buffer an unbounded provider response.
- Disclosure invariant: public OAuth errors contain the upstream status, not the upstream response body.
- Focused evidence: Google Calendar integration slice 6/6, including both named-client primary handlers.
- Assertion/gap review: the configuration theory fails for either OAuth or Calendar if redirects return; existing callback tests continue validating token exchange/userinfo behavior. Structural review confirms `ResponseHeadersRead`, 1 MiB success caps, 4 KiB error cap and absence of provider-body interpolation.
- Final evidence: formatter clean; Release build 0 warnings / 0 errors; complete suite 869 passed / 0 failed / 1 skipped.
# Google Calendar persistent event mapping (2026-08-14)

- Status: implementation and validation complete; ready for commit and push.
- Functional invariant: one Google event id is retained per connection/card pair, so later changes update and due-date clearing deletes that exact event.
- Focused evidence: mapping domain regression 1/1 and Google Calendar integrations 6/6.
- Assertion/gap review: the regression kills missing replace/remove behavior and cross-card corruption; source search proves the always-null placeholder is gone. Existing sync-handler tests retain fan-out/success/error coverage.
- Final evidence: formatter clean; Release build 0 warnings / 0 errors; complete suite 870 passed / 0 failed / 1 skipped.
# Canonical card mirror command (2026-08-15)

- Status: implementation and validation complete; ready for commit and push.
- Contract invariant: REST and MCP invoke the same command that atomically creates the mirrored Card and CardMirror pointer.
- Compatibility policy: the duplicate pointer-only command and its untyped MCP result are deleted.
- Focused evidence: architecture regression 1/1.
- Behavioral evidence: mirror integration slice 5/5 and MCP-inclusive E2E 7/7.
- Assertion/gap review: the architecture test fails on any second same-named command and asserts its declaring canonical type; existing integration asserts a distinct persisted target card and pointer behavior.
- Final evidence: formatter clean; complete suite 871 passed / 0 failed / 1 skipped.
# Remove placeholder database log sink (2026-08-15)

- Status: implementation and validation complete; ready for commit and push.
- Operational invariant: every advertised logging sink performs real output; supported paths are console, rolling JSON files and optional OTLP.
- Compatibility policy: `Serilog:Database` is removed rather than retained as an ignored setting.
- Focused evidence: placeholder-sink architecture regression 1/1; architecture suite 20/20.
- Assertion/gap review: the regression fails if either deleted type returns; exhaustive runtime/config search has no active references. Historical changelog mention is intentionally preserved.
- Final evidence: formatter clean; Release build 0 warnings / 0 errors; complete suite 872 passed / 0 failed / 1 skipped.
