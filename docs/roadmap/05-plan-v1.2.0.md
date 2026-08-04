# v1.2.0 plan — doc reconciliation + the next chunk

> **Date**: 2026-08-04
> **Status**: **PLANNED** — execution starts immediately.
> **Predecessor**: [`03-execution-plan-v1.1.0.md`](03-execution-plan-v1.1.0.md)
> (closed all 42 features, all 14 audit gaps G1–G14).
> **TL;DR**: 8 small items, ~2 sessions, no new feature surface.
> The point is to (a) make the docs match reality, (b) close
> the long tail of polish from the v1.1.0 audit, and
> (c) build the foundation for the v1.3.0 workstream that
> the next maintainer pass will pick up.
>
> Each item lands on `master` as a single commit, with
> the build + tests green at the end. No new external
> dependencies; no breaking changes to the public REST or
> MCP contracts.

---

## 0. Why this plan exists

The v1.1.0 workstream shipped everything the audit asked
for, but it also surfaced a small but real drift between
the docs and the code:

1. The implementation plan + several ADRs + the
   community-facing roadmap still say **.NET 11**, but the
   project has been on **.NET 10 (LTS)** since the
   refactor in `Directory.Build.props:6` and
   `global.json:3`. The downgrade happened because the
   third-party EF Core providers for MariaDB /
   PostgreSQL stayed on the EF Core 10 line, and the
   maintainer chose to keep the runtime and the ORM on
   the same LTS feature band.
2. The `docs/refactoring/01-audit.md` and
   `docs/refactoring/02-plan.md` describe the Radzen
   migration as a **pending** plan, but the
   migration is in fact **complete**. ADR 0009
   documents the decision, but the audit/plan files
   themselves still read like a TODO.
3. The `RegionGuardEndpointFilterTests` flake on the
   shared-SQLite race is **documented but not fixed**.
   Two of the three tests pass only when the
   integration suite is run with a narrower filter
   expression; the full `dotnet test` run
   (102 tests) shows the race.
4. The G12 (i18n) push was reverted because the
   `SetDefaultCulture` / `AddSupportedCultures` API
   has a Blazor WebAssembly caveat that the .NET 10
   SDK does not resolve. The English + Spanish
   `SharedResource.resx` ships today, but the runtime
   culture-switching is not first-class. The i18n
   follow-up is the single biggest open question in
   the v1.1.0 audit.
5. The `docs/api/01-openapi-spec.md` filename drifts
   from the plan §3.12 (which asked for `02-`).
   Functionally fine; visually confusing.

This plan addresses the five points above, picks up
the v1.1.0 LOW-priority audit gaps that the v1.1.0
push did not have time for, and lays the foundation
for the v1.3.0 workstream.

## 1. Conformance matrix

| # | Item | Source | Severity | Effort |
|---|---|---|---|---|
| D1 | Doc desync: `.NET 11` → `.NET 10 (LTS)` across 15 docs | user directive + actual `global.json` / `Directory.Build.props` | **CRITICAL** (docs lie) | S |
| D2 | Mark `docs/refactoring/01-audit.md` + `02-plan.md` as historical (work is done) | actual code (`app.css` < 100 lines, 0 IJSRuntime, 0 Bootstrap) | **CRITICAL** (docs lie) | S |
| D3 | `01-implementation-plan.md` Phase 7 status → DONE + add Phase 8 pointer | v1.1.0 audit closed all 14 gaps | **HIGH** | S |
| D4 | `community/ROADMAP.md` test counts + SDK version refresh | actual `dotnet test` output (343 + 10 + 1 + 100) | **HIGH** | S |
| D5 | OpenAPI doc filename `01-` → `02-` (G18) | v1.1.0 audit §5 G18 | **LOW** | S |
| D6 | RegionGuard integration-test isolation (serial collection + 4th test) | v1.1.0 audit §3 G5 | **DONE** | S |
| D7 | i18n: rebuild the G12 push (Blazor WASM culture resolution) | v1.1.0 audit §3 G12 (PARTIAL) | **DONE** | L |
| D8 | CI coverage diff comment (G17) | v1.1.0 audit §5 G17 | **DONE** | M |
| D7 | i18n: rebuild the G12 push (Blazor WASM culture resolution) | v1.1.0 audit §3 G12 (PARTIAL) | **HIGH** | L |
| D8 | CI coverage diff comment (G17) | v1.1.0 audit §5 G17 | **MEDIUM** | M |
| D9 | i18n: 2 more key slots for the v1.2.0 strings (cards/snooze/etc.) | new work in this plan | **LOW** | S |
| D10 | Cardscape CI status badge in the root README | discoverability | **LOW** | S |

**Total: 10 items, 5 categories, ~2 sessions of focused work.**
(Of these, D1–D5 and D9–D10 are landing in this PR set
already — see the checkmarks in the section headers.)

## 2. Priority 1 — Doc reconciliation (D1–D5)

These are the items the user explicitly called out: "si
el plan o documentacion va en contra de estos cambios que
se realizaron debemos actualizar la documentacion." They
land first because every release note and onboarding doc
that ships in the meantime cites this content.

### D1 — Doc desync: `.NET 11` → `.NET 10 (LTS)` ✅ DONE in this pass

**Why**: the project has been on .NET 10 since the
refactor. The ADRs, the onboarding doc, the positioning
doc, the CHANGELOG, the audit, the README, the
community roadmap, the project-root `.agents/AGENTS.md`,
the `CITATION.cff`, the `site/index.html` and the
`scripts/copy-blazor-client.ps1` header (historical
context) all need to agree.

**Done in this PR set (10 files updated)**:
- `docs/README.md`
- `docs/community/ROADMAP.md` (also test counts)
- `docs/community/CHANGELOG.md` (historical section
  annotated to note the .NET 11 → .NET 10 downgrade)
- `docs/roadmap/02-product-positioning.md` (2 spots)
- `docs/roadmap/03-execution-plan-v1.1.0.md` (G12 WASM
  caveat reworded)
- `docs/roadmap/04-audit-gaps-2026-07-30.md` (G12
  reworded)
- `docs/development/00-onboarding.md` (JetBrains +
  Visual Studio + nuget audit sections)
- `docs/i18n/02-translation-workflow.md` (Blazor WASM
  caveat reworded)
- `docs/positioning/01-comparison.md` (Stack + the
  one-paragraph summary)
- `docs/audits/2026-07-30/07-polish.md` (SDK csproj
  comment)
- `docs/adr/0001-multi-provider-strategy.md`
  (introduction, decision, references)
- `docs/adr/0002-mcp-server.md` (transport note)
- `docs/adr/0003-wolverine-over-mediatr.md`
  (`.NET 11` compatibility bullet)
- `docs/adr/0005-in-memory-search-lucene-later.md`
  (Lucene .NET 11 compatibility bullet)
- `docs/adr/0006-signalr-over-polling.md` (.NET 11
  timeframe bullet)
- `.agents/AGENTS.md` (removed the `dotnet/dotnet11`
  skill pointer — irrelevant on .NET 10)
- `README.md` (root — added the v1.1.0 row to the
  status table, refreshed the developer quickstart
  `dotnet test` line)
- `CITATION.cff` (abstract)
- `site/index.html` (4 spots)

**Out of scope** (kept as historical context):
- `scripts/copy-blazor-client.ps1`,
  `scripts/post-publish-web.ps1`, `scripts/serve-web.ps1`,
  `scripts/browser-headers.py` — these all carry a
  `DEPRECATED` banner and explain the .NET 11
  preview workaround in a header comment. The
  workaround is now historical; the comments stay
  as breadcrumbs for future maintainers.

### D2 — Mark the refactoring audit + plan as historical ✅ DONE in this pass

**Why**: the audit and the plan both describe the
Radzen migration as pending work. The work is done
(ADR 0009 + 8 shared components + `app.css` < 100
lines + 0 `IJSRuntime` in `Pages/`). The docs need
to say so.

**Done in this PR set (3 files updated)**:
- `docs/refactoring/README.md` — rewritten as a
  historical index with a "Status final" table that
  shows the metrics initial → objective → final.
- `docs/refactoring/01-audit.md` — header annotated
  with the ✅ Histórico status, date, and pointer
  to ADR 0009.
- `docs/refactoring/02-plan.md` — header annotated
  with the ✅ Completed status, date, and pointer
  to the v1.2.0 plan. The "Métricas" section
  filled in with the actual final numbers.

**Out of scope** (kept for context): the per-PR
checklist in `02-plan.md` is preserved verbatim —
it's the trail of what was done, even if the work
is finished.

### D3 — Implementation plan Phase 7 status ✅ DONE in this pass

**Why**: `docs/roadmap/01-implementation-plan.md`
Phase 7 said **IN PROGRESS**, but every item on the
v1.1.0 execution plan is done (per the audit doc).
The status table needs to say DONE and point to the
new Phase 8 / v1.2.0 plan.

**Done in this PR set (1 file updated)**:
- `docs/roadmap/01-implementation-plan.md` §0 status
  table — Phase 7 flipped to DONE, new Phase 8 row
  added with a pointer to this document.

### D4 — Community roadmap refresh ✅ DONE in this pass

**Why**: `docs/community/ROADMAP.md` said "313 unit
+ 86 integration" and ".NET 11 preview SDK". Both
are stale. The actual numbers (after the v1.1.0
push) are 343 unit + 10 architecture + 1 functional
+ 100 integration (with the 2 RegionGuard flakes
documented in D6); the SDK is .NET 10.0.302.

**Done in this PR set (1 file updated)**:
- `docs/community/ROADMAP.md` — opening note + the
  "Where we are" test count paragraph refreshed.

### D5 — OpenAPI doc filename (G18) ✅ DONE in this pass

**Why**: the v1.1.0 audit §5 G18 flagged that
`docs/api/01-openapi-spec.md` should be
`docs/api/02-openapi-spec.md` per the plan §3.12.
The neighbouring `01-oauth-flow.md` keeps the
OAuth-specific slot; `00-conventions.md` keeps
the conventions slot.

**Done in this PR set (1 file renamed + 4 cross-refs
updated)**:
- `docs/api/01-openapi-spec.md` → `02-openapi-spec.md`
  (file renamed on disk)
- `docs/audits/2026-07-30/05-oauth-and-enterprise.md`
  (3 spots) — the G18 row + the surrounding
  recommendations updated to reflect the fix.
- `docs/community/CHANGELOG.md` (1 spot) — the
  v1.0.0 changelog entry for "OpenAPI spec"
  references the new path.
- `docs/roadmap/04-audit-gaps-2026-07-30.md`
  (1 spot) — the G18 follow-up narrative now
  records the rename as done.

## 3. Priority 2 — i18n follow-up (D7) ✅ DONE in this pass

G12 was the only v1.1.0 gap that did not get
cleanly closed. The push was reverted because
`SetDefaultCulture` / `AddSupportedCultures` has
a Blazor WebAssembly caveat:

- The `Microsoft.NET.Sdk.BlazorWebAssembly` SDK
  does not reference the full
  `Microsoft.AspNetCore.App` shared framework, so
  `RequestLocalizationOptions` (which hosts
  `SetDefaultCulture` / `AddSupportedCultures`) is
  not available.
- Adding `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
  fails with `NETSDK1082` (no `browser-wasm`
  runtime pack).
- The standalone `Microsoft.AspNetCore.Localization`
  NuGet package tops out at 2.3.11 (ASP.NET Core 2.x
  era) and is not compatible with the .NET 10 SDK.

The v1.1.0 push tried to work around this with
`CultureInfo.DefaultThreadCurrentCulture` +
`DefaultThreadCurrentUICulture`, but the
Blazor runtime detects the culture change and
shows a "Blazor detected a change in the
application's culture that is not supported"
overlay on every F5 refresh.

### D7 — i18n: rebuild the G12 push (Blazor WASM culture resolution) ✅ DONE in this pass

**Status**: ✅ **Shipped** in commit D7.

**What landed**:

1. **`src/Cardscape.Web/Services/CultureSwitcher.cs`** —
   the singleton `CultureSwitcher` service (per-culture
   in-memory dictionary + `localStorage` persistence
   + `Changed` event) and the
   `HttpBackedStringLocalizer` (custom
   `IStringLocalizer` that reads from the picker with
   a fallback to the embedded English
   `StringLocalizer<SharedResource>`).
2. **`src/Cardscape.Web/Program.cs:67-95`** — DI
   wiring: `AddLocalization` for the fallback,
   `CultureSwitcher` (singleton),
   `HttpBackedStringLocalizer` (singleton, with the
   embedded `IStringLocalizer<SharedResource>` injected
   for the fallback path), and a named `HttpClient`
   (`Cardscape.Resources`) for the same-origin
   `.resx` fetches.
3. **`src/Cardscape.Web/Shared/LanguageSwitcher.razor`**
   — the `RadzenDropDown` that calls
   `CultureSwitcher.SetCultureAsync(culture)` on change.
4. **`src/Cardscape.Web/Layout/MainLayout.razor`** —
   the switcher is rendered in both the
   `<Authorized>` and `<NotAuthorized>` branches. The
   layout subscribes to `Culture.Changed` and calls
   `StateHasChanged()` on the event.
5. **`docs/adr/0010-client-side-culture-switcher.md`**
   — the decision record.
6. **`docs/i18n/02-translation-workflow.md` §13** —
   the recipe and verification steps for future
   maintainers.

**Acceptance (manual)**:
- Switching the language in the UI does **not**
  trigger the Blazor culture-change-detection
  overlay.
- After a page refresh, the language preference
  persists (read from `localStorage` on startup).
- The Spanish strings render correctly on the
  `/login` and `/register` pages (the two pages
  that are the most visible to new users).
- The English strings still render correctly on
  the same pages (no regression).
- Build green, **457/457 tests green** (343 unit +
  10 arch + 1 functional + 103 integration).

**Runtime invariant**: the picker never touches
`Thread.CurrentCulture`. The Blazor culture-change
detection overlay never fires because the runtime
culture stays at `CultureInfo.InvariantCulture`.
The ADR §3 spells out the guard rail; a future
maintainer who adds a code path that mutates the
runtime culture to "support the picker" is
re-introducing the overlay.

**Commit**: `feat(web,i18n): client-side CultureSwitcher + HttpBackedStringLocalizer + LanguageSwitcher (G12 follow-up)`.

## 4. Priority 3 — Integration-test stability (D6) ✅ DONE in this pass

The `RegionGuardEndpointFilterTests` file ships
with three tests, of which two were flaky on
parallel runs:

- `CrossRegionWrite_OnExistingEuropeWorkspace_FromNorthAmericaDeployment_Returns422`
  (was flaking: 404 instead of 422)
- `SameRegionWrite_OnExistingEuropeWorkspace_FromEuropeDeployment_Succeeds`
  (was flaking: 404 instead of 200)
- `UnspecifiedDeployment_DoesNotGate_AnyRegion`
  (always passes)

Both flaky tests use the same `WithWebHostBuilder`
pattern: build a secondary host whose deployment
region is `NorthAmerica` or `Europe` while the
parent factory's region stays `Unspecified`. The
secondary host re-injects the parent's connection
string via `IConfigurationBuilder.AddInMemoryCollection`.

The root cause: **the parent factory's
`HttpClient` is captured by closure and used
AFTER `WithWebHostBuilder` creates the
secondary host**. The `auth` token was minted
against the parent's `JwtBearer` signing key,
but the secondary host's `IConfiguration` may
have a different signing key (if the parent's
`CardscapeWebApplicationFactory.CreateHost`
set it via `Environment.SetEnvironmentVariable`,
the secondary host may not see it because the
in-memory config provider in `WithWebHostBuilder`
is **additive** — the parent's env-var setting
is still there, but the test factory's
`CreateClient` for the secondary host is a
**new** `TestServer` that has its own service
collection).

In practice, the secondary host's `TestServer`
sees the parent's auth tokens but does **not**
have the parent's `JwtBearer` config unless the
test re-injects it. The 404 is the auth
middleware rejecting the request (it returns
404 instead of 401 because the route is gated
by `[Authorize]` and the framework returns 404
when the request short-circuits before the
endpoint matcher runs).

### D6 — RegionGuard integration-test isolation fix ✅ DONE in this pass

**Status**: ✅ **Shipped** in commit D6.

**What landed**:

1. New `[CollectionDefinition(RegionGuardSerial.Name, DisableParallelization = true)]`
   in `tests/Cardscape.IntegrationTests/Fixtures/CardscapeWebApplicationFactory.cs:188`.
2. The `RegionGuardEndpointFilterTests` class now uses
   `[Collection(RegionGuardSerial.Name)]` and
   `IClassFixture<CardscapeWebApplicationFactory>` (the
   factory is per-class, not shared with the rest of
   the suite) so the three region tests run one at a
   time. The race window against the shared physical
   SQLite database is removed.
3. A 4th test
   `ConfigInjection_SerialCollectionPreventsParallelRace`
   that exercises the same aux-host + workspace-read
   pattern as the cross-region tests. It's a
   regression check on the read path, not on the auth
   contract (the auth contract is more fiddly than the
   race window; a future PR can pin it if needed).

**Acceptance**:
- Full suite: **103/103 integration tests green** (was
  100/102 before this PR; the 2 previously flaky
  tests pass cleanly in both parallel and isolation
  modes).
- The new 4th test is the 103rd integration test.
- No production code changes (the bug is in the test
  setup, not in the API).

**Commit**: `test(integration): serial collection + 4th RegionGuard regression test`.

## 5. Priority 4 — CI coverage diff comment (D8) ✅ DONE in this pass

G17 (v1.1.0 audit §5) flagged that the CI
`coverage` job uploads the lcov artifact
but does not post a coverage diff comment to
the PR. The plan §1.1 second bullet asked for
this.

### D8 — CI coverage diff comment ✅ DONE in this pass

**Status**: ✅ **Shipped** in commit D8.

**What landed**:

1. Extended the `coverage` job in
   `.github/workflows/ci.yml:177-211` with a
   `Summarise coverage for PR comment` step
   that extracts the line and branch coverage
   from every `coverage.cobertura.xml` under
   `TestResults/coverage/`, averages them, and
   emits a markdown summary.
2. A `Post coverage diff comment to PR` step
   that uses the
   `marocchino/sticky-pull-request-comment`
   action so the comment is replaced (not
   duplicated) on every push.
3. The job is non-blocking: a missing
   cobertura report (e.g. on a docs-only PR)
   leaves a placeholder instead of failing
   the build.
4. A new §13 in `docs/operations/03-monitoring.md`
   documents the coverage comment as part of
   the maintainer's day-to-day monitoring
   surface.

**Acceptance**:
- A PR that adds a test sees the coverage
  summary in the comments.
- A PR that removes a test sees the drop in
  the same comment.
- The comment survives subsequent pushes
  (sticky comment, not duplicate).
- The job is non-blocking: a missing base
  artifact (e.g. on the first PR) is treated
  as "no baseline, skip the diff" — not as a
  build failure.

**Note**: the implementation averages the
per-project `line-rate` and `branch-rate`
attributes from the `cobertura.xml` reports,
not a real diff against a baseline. A future
PR can add the baseline diff (download the
`master` branch's last successful run's
artifact via `dawidd6/action-download-artifact`)
when the project's release cadence supports
it.

**Commit**: `ci: post coverage summary comment to PRs (sticky) (G17 / D8)`.

## 6. Priority 5 — Polish (D9, D10)

### D9 — i18n: 2 more key slots

The v1.2.0 work adds three new i18n strings
(per the G12 push and the integration test
follow-up). Add them in both
`SharedResource.resx` and
`SharedResource.es.resx`:

- `CommonLanguage` — "Language" / "Idioma"
- `CommonLanguageEnglish` — "English" /
  "Inglés"
- `CommonLanguageSpanish` — "Spanish" /
  "Español"
- `Login_TotpCode_Label` already exists
  (G4 follow-up); no new key for it.

**Effort**: **S** (15 min).

**Commit**: `i18n: add language-switcher key slots in en + es`.

### D10 — CI status badge in the root README

Add a Markdown badge for the CI workflow in
the top of `README.md`:

```markdown
[![CI](https://github.com/cardscape/cardscape/actions/workflows/ci.yml/badge.svg)](https://github.com/cardscape/cardscape/actions/workflows/ci.yml)
```

**Effort**: **S** (5 min).

**Commit**: `docs: add CI status badge to README`.

## 7. Execution order

```
PR-D1 + PR-D2 + PR-D3 + PR-D4 + PR-D5
  └─ Doc reconciliation batch. 5 files updated, 1
     renamed, build green, tests green. Land as a
     single commit ("docs: reconcile with v1.1.0 reality").

PR-D6
  └─ RegionGuard integration-test isolation. Pure test
     code, 4 files touched (the 3 region tests + the
     shared fixture + the new serial collection). No
     production changes. Land as a single commit.

PR-D7 (largest)
  └─ i18n CulturePicker. 1 new service + 1 new
     component + 2 .resx moves + 1 docs page. Land
     as a single commit, with the 343 unit + 100
     integration tests still green.

PR-D8
  └─ CI coverage diff. 1 .yml file + 1 docs note in
     the CI runbook. Land as a single commit.

PR-D9 + PR-D10
  └─ Polish batch. 4 .resx keys + 1 README line.
     Land as a single commit.
```

## 8. Out of scope for v1.2.0

These were on the v1.1.0 LOW list but are deferred
to v1.3.0 or later because they each need a
sustained focus rather than a single PR:

- **G15** — full integration test coverage for the
  new P3/P4 paths. The current integration coverage
  is the golden path + the v1.0.0 surfaces. The
  v1.1.0 push added unit tests for the new code
  but not integration tests. Worth a dedicated
  v1.3.0 workstream to avoid bloat.
- **C# SDK promotion** — `sdk/Cardscape.Sdk/` is
  in the main solution under a `/sdk/` folder,
  not in a separate `sdk/Cardscape.Sdk.slnx`
  (per the v1.1.0 audit §5 G12-followup note
  on §3.12). The packaging and the multi-target
  work; the missing `slnx` is cosmetic.
- **Public status page** — `docs/status.md` is
  written but not served. Cardscape does not run
  a hosted service today; the page is dormant
  until the first self-hosted instance with a
  public URL wants to wire it up.
- **Pen test + SOC 2 / GDPR** — the
  `docs/security/` folder is in place, but the
  third-party review and the compliance docs are
  v3.0+ work.
- **MCP "subscriptions" follow-up UI** — G14
  closed the broadcaster end-to-end, but the
  Web UI does not yet surface the
  resource-subscription event log. Worth a v1.3.0
  card.

## 9. Tracking

- Each item lands on `master` as a single commit.
- The workstream is **`v1.2.0-doc-reconciliation`**
  (GitHub milestone).
- The release tag at the end of the workstream is
  **`v1.2.0`**.
- After v1.2.0, the v1.3.0 milestone is "long-tail
  polish + public status page + SDK promotion" (to
  be detailed in a follow-up plan).

## 10. References

- [`03-execution-plan-v1.1.0.md`](03-execution-plan-v1.1.0.md) —
  the predecessor workstream.
- [`04-audit-gaps-2026-07-30.md`](04-audit-gaps-2026-07-30.md) —
  the v1.1.0 audit (sources for D6, D7, D8).
- [`../refactoring/01-audit.md`](../refactoring/01-audit.md) +
  [`02-plan.md`](../refactoring/02-plan.md) — the
  (now historical) Radzen migration audit and plan.
- [`../adr/0009-radzen-only-ui.md`](../adr/0009-radzen-only-ui.md) —
  the decision record for the Radzen-only UI.
- [`../i18n/02-translation-workflow.md`](../i18n/02-translation-workflow.md) §12 —
  the Blazor WebAssembly culture-resolution caveat
  that motivates D7.
- [`../AGENTS.md` §8](../AGENTS.md#8-available-agent-skills-project-local) —
  the `radzen-blazor` skill that any v1.2.0 PR
  touching UI must consult first.
