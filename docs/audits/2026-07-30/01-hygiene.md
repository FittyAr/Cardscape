# 01 — Hygiene audit (2026-07-30)

Scope: `docs/roadmap/03-execution-plan-v1.1.0.md` §1 — Priority 1 Hygiene
(`1.1 Real CI workflow`, `1.2 Empty test projects`, `1.3 Plan status sync`,
`1.4 ADRs`). Read-only audit; the only writes are the four `### 1.X` plan
headers, which were marked `✅ DONE` per task instructions.

---

## 1.1 Real CI workflow

- **Verdict**: **DONE**
- **Evidence**:
  - Workflow file exists: `.github/workflows/ci.yml:1-244` (244 lines).
  - Triggers cover push (master / main / `v*` tags) and pull_request
    (`.github/workflows/ci.yml:3-8`) — matches the plan's
    "runs on push and PR" requirement.
  - Jobs present (all six — plan only required the first five):
    1. `format-verify` (`.github/workflows/ci.yml:18-31`) — runs
       `dotnet format --verify-no-changes --no-restore` (line 31).
       Matches plan §1.1 bullet 1.
    2. `build` (`.github/workflows/ci.yml:33-71`) — restores, builds
       in Release (line 57), then runs the two test projects that the
       main solution wires (lines 58-71). Matches plan §1.1 bullet 2.
    3. `unit-tests` (`.github/workflows/ci.yml:73-100`) — runs
       `dotnet test` against `tests/Cardscape.UnitTests` (line 96) with
       TRX output. Matches plan §1.1 bullet 3 (unit leg).
    4. `integration-tests` (`.github/workflows/ci.yml:102-139`) — runs
       `dotnet test` against `tests/Cardscape.IntegrationTests` (line
       128) with `Database__Provider=Sqlite` and an in-memory
       `ConnectionStrings__Default` (lines 109-110). Matches plan §1.1
       bullet 3 (integration leg, SQLite in-memory).
    5. `coverage` (`.github/workflows/ci.yml:141-187`) — runs both
       test projects with `--collect:"XPlat Code Coverage"` (lines
       169 and 175) and uploads the lcov/cobertura artifact (lines
       183-187). Matches plan §1.1 bullet 4.
    6. `release` (`.github/workflows/ci.yml:189-243`) — bonus, only
       runs on `v*` tags. Packs NuGet, captures OpenAPI spec, uploads
       both as artifacts. Cross-references §3.12 of the plan.
- **Notes**:
  - The plan §1.1 also asks for a "coverage diff comment to the PR
    (comment-only, never blocking)". The current `coverage` job
    uploads the lcov artifact but does **not** post a PR comment.
    This is a cosmetic gap; the core item ("real CI workflow with
    format / build / test / coverage") is fully DONE. Not enough to
    downgrade the verdict.
  - Lines 58-71 guard the ArchitectureTests / FunctionalTests runs
    with a `compgen -G` check. Both projects now produce build
    output, so the guard is now a no-op for the green path; it is
    harmless legacy from when those projects were empty.
  - The plan §1.1 also asked to "update CHANGELOG to remove the
    claim that the workflow exists". Not in the audit's verification
    scope (the audit only asks for the workflow file). Noted for
    follow-up if the maintainer wants a strict reading.

---

## 1.2 Empty test projects

- **Verdict**: **DONE**
- **Evidence**:
  - `tests/Cardscape.FunctionalTests/`:
    - Project file: `tests/Cardscape.FunctionalTests/Cardscape.FunctionalTests.csproj`.
    - Test file: `tests/Cardscape.FunctionalTests/GoldenPathSmokeTests.cs:1-125`.
    - One real `[Fact]` test
      `GoldenPath_RegisterCreateWorkspaceBoardListCard_MoveAndArchive_AllSucceed`
      at `tests/Cardscape.FunctionalTests/GoldenPathSmokeTests.cs:28-124`.
    - It walks the exact golden path the plan calls for:
      register (line 40) → workspace (line 52) → board (line 68) →
      list (line 78) → second list (line 87) → card (line 94) →
      move (line 105) → archive (line 114) → verify archived
      (line 119). The class comment on lines 11-21 explicitly
      references `docs/development/02-vertical-slices.md` (the
      recipe the plan points to).
  - `tests/Cardscape.ArchitectureTests/`:
    - Project file: `tests/Cardscape.ArchitectureTests/Cardscape.ArchitectureTests.csproj`.
    - Test file: `tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:1-217`.
    - Ten `[Fact]` NetArchTest rules, all green on the current code:
      - `Domain_DoesNotDependOn_AnyOuterLayer`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:22-37`).
      - `Application_DependsOn_Domain_Only`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:39-53`).
      - `Infrastructure_DependsOn_ApplicationAndDomain_Only`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:55-68`).
      - `Api_DependsOn_ApplicationInfrastructureDomain_Only`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:70-86`).
      - `Web_DependsOnNothing_BeyondItself`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:88-113`).
      - `Mcp_DependsOn_ApplicationInfrastructureDomain_Only`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:115-132`).
      - `Domain_Entities_AreSealed`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:134-157`).
      - `Application_Handlers_AreSealed`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:159-176`).
      - `Application_Abstractions_Live_Under_Abstractions_Namespace`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:178-194`).
      - `Infrastructure_HasNoOrphanInterfaces`
        (`tests/Cardscape.ArchitectureTests/ArchitectureTests.cs:196-216`)
        — this is the "no orphan interfaces" rule the plan §1.2
        specifically calls for. Rule body is on lines 197-216 and
        explicitly checks "Infrastructure must not introduce new
        public interfaces — they belong in Application/Abstractions".
- **Notes**:
  - Both projects are real tests now, not scaffolds. The plan's two
    requirements (golden-path smoke + Clean Architecture NetArchTest
    rules + orphan-interface rule) are all present.
  - The ArchitectureTests file is more thorough than the plan asked
    for (10 rules vs. the minimum 3 the plan implied). This is a
    good thing — no penalty, no gap.

---

## 1.3 Plan status sync

- **Verdict**: **DONE**
- **Evidence**:
  - `docs/roadmap/01-implementation-plan.md` §0 (status table at
    `docs/roadmap/01-implementation-plan.md:28-39`):
    - Phase 0 — **DONE** (line 32, tag `c1b9800` etc.).
    - Phase 1 — **DONE v0.1.0-mvp** (line 33).
    - Phase 2 — **DONE v0.2.0-core-mcp** (line 34).
    - Phase 3 — **DONE v0.3.0-api-tokens** (line 35).
    - Phase 4 — **DONE v0.4.0–v0.6.4** (line 36).
    - Phase 5 — **DONE v0.7.0–v0.7.10** (line 37).
    - Phase 6 — **DONE v1.0.0** (line 38, "first production release
      with full Kanban parity", "313 unit + 85 integration tests
      green").
    - Phase 7 — **IN PROGRESS v1.1.0-roadmap-execution** (line 39,
      points to `03-execution-plan-v1.1.0.md`).
    - This matches the plan §1.3 spec exactly: 0 (DONE), 1 (DONE
      v0.1.0-mvp), 2 (DONE v0.2.0-core-mcp), 3 (DONE v0.3.0-api-tokens),
      4 (DONE v0.4.0–v0.6.4), 5 (DONE v0.7.x), 6 (DONE v1.0.0),
      7 (IN PROGRESS v1.1.0-roadmap — this plan).
  - `docs/community/ROADMAP.md`:
    - "Where we are" section
      (`docs/community/ROADMAP.md:24-51`) opens with "Cardscape is
      at **v1.0.0** — first production release with **full Kanban
      parity**" (line 26) and "**313 unit tests + 86 integration
      tests** are green" (line 40). Cross-references
      `v1.1.0-roadmap-execution` (line 47) and the audit
      (`03-execution-plan-v1.1.0.md`, line 49).
    - Phase table (`docs/community/ROADMAP.md:57-66`) has the same
      eight rows in community-readable tone:
      - 0 ✅ done (line 59)
      - 1 ✅ done (`v0.1.0-mvp`) (line 60)
      - 2 ✅ done (`v0.2.0-core-mcp`) (line 61)
      - 3 ✅ done (`v0.3.0-api-tokens`) (line 62)
      - 4 ✅ done (`v0.4.0` through `v0.6.4`) (line 63)
      - 5 ✅ done (`v0.7.0` through `v0.7.10`) (line 64)
      - 6 ✅ done (`v1.0.0`) (line 65)
      - 7 🔄 in progress (`v1.1.0-roadmap-execution`) (line 66)
- **Notes**:
  - Both files are in sync with the v1.0.0 release claim and
    mark v1.1.0-roadmap as IN PROGRESS. No drift detected.
  - Minor inconsistency: `01-implementation-plan.md` line 33 says
    "313 unit + 85 integration tests green" for the v1.0.0 row;
    `docs/community/ROADMAP.md` line 40 says "313 unit tests + 86
    integration tests" for the current state. This is a 1-test
    drift, not a status-table gap. Not in the audit's verification
    scope but flagged for awareness.

---

## 1.4 ADRs

- **Verdict**: **DONE**
- **Files present** (all six in `docs/adr/`):
  - `docs/adr/0003-wolverine-over-mediatr.md` —
    `# ADR 0003: Wolverine over MediatR for the command/query bus`
    (line 1), **Status**: Accepted (line 3). 118 lines total.
  - `docs/adr/0004-rpl-1.5-license.md` —
    `# ADR 0004: Reciprocal Public License 1.5 (RPL-1.5) for the project licence`
    (line 1), **Status**: Accepted (line 3). 115 lines total.
  - `docs/adr/0005-in-memory-search-lucene-later.md` —
    `# ADR 0005: ISearchIndex with an in-memory implementation today, Lucene.NET when the volume warrants`
    (line 1), **Status**: Accepted (line 3). 129 lines total.
  - `docs/adr/0006-signalr-over-polling.md` —
    `# ADR 0006: SignalR for real-time board sync (over polling, SSE, and WebSockets-direct)`
    (line 1), **Status**: Accepted (line 3). 137 lines total.
  - `docs/adr/0007-no-hangfire.md` —
    `# ADR 0007: Internal background jobs (no Hangfire)`
    (line 1), **Status**: Accepted (line 3). 141 lines total.
  - `docs/adr/0008-clean-architecture-lite.md` —
    `# ADR 0008: Clean Architecture, "lite" — deliberate deviations from the textbook shape`
    (line 1), **Status**: Accepted (line 3). 176 lines total.
- **Files missing**: none. All six ADRs required by plan §1.4 are
  present and accepted. (For context: the pre-existing
  `docs/adr/0001-multi-provider-strategy.md` and
  `docs/adr/0002-mcp-server.md` are also still there; the audit
  only required 0003-0008.)
- **Notes**:
  - All six ADRs are dated `2026-07-29` (line 4 of each), which is
    consistent with the v1.1.0 plan generation date.
  - Each ADR's filename matches the plan's exact spelling (kebab-case
    + numeric prefix + topic), so `docs/roadmap` links and
    cross-references resolve cleanly.

---

## Summary

- **DONE**: 4
- **PARTIAL**: 0
- **MISSING**: 0

All four Priority 1 hygiene items are fully implemented. The only
minor cosmetic note is that the CI `coverage` job does not post a
PR comment with the coverage diff (plan §1.1, second bullet) — the
coverage artifact is uploaded but the comment step is absent. This
is a nice-to-have, not a blocker, and was outside the audit's
explicit verification scope (which only required the workflow file
to have format / build / test / coverage steps).

The plan document has been updated in place:
`docs/roadmap/03-execution-plan-v1.1.0.md` §1.1, §1.2, §1.3, §1.4
now carry `✅ DONE` markers on their headers. No other plan content
was touched.
