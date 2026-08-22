# Release process

> How a Cardscape release is cut, versioned, packaged, and
> announced. The maintainer follows this document for every
> release.

This is a **process** document — it describes what to do, in
what order, and what the outputs are. The actual scripts and
CI configuration that automate parts of this process land
with Phase 1. Until then, the maintainer runs the steps by
hand, following this checklist.

---

## 1. Versioning

Cardscape uses **Semantic Versioning** with pre-1.0 caveats.

- `MAJOR.MINOR.PATCH` (e.g. `0.2.0`).
- Pre-1.0 (`0.y.z`): minor bumps can include breaking
  changes. The schema and the public API are not yet stable.
- At `1.0.0`: schema and public API are stable. Breaking
  changes bump the major version.
- Build metadata: optional, after a `+` (e.g. `0.2.0+meta`).
  Not used for versioning decisions.

### Tag format

```
v<MAJOR>.<MINOR>.<PATCH>[-<phase-suffix>]
```

Examples:

- `v0.1.0-mvp` — Phase 1 complete.
- `v0.2.0-core-mcp` — Phase 2 complete, MCP server ships.
- `v0.2.1` — patch on top of `v0.2.0`.
- `v1.0.0` — first stable release.

The `phase-suffix` is a label, not a SemVer pre-release
identifier. We do not use SemVer pre-releases (`-alpha.1`,
`-rc.1`) until post-1.0.

---

## 2. Branching strategy

- `master` is the development line. Every merged PR lives on
  master.
- `site` is the website branch (orphan, contains only the
  static site).
- `<owner>/<scope>-<short-desc>` for feature branches
  (e.g. `mavis/feat-card-snooze`).
- `<owner>/fix-<short-desc>` for bug-fix branches.
- `release/<tag>` is **not** used. Releases are cut from
  master directly, after a green CI and a green manual
  smoke test.

When a release is being cut, the maintainer creates a
`release-prep/<tag>` branch off master, runs the checklist
below, and merges it back into master. The release tag is
placed on the merge commit. This branch is deleted after
the release.

---

## 3. Pre-release checklist

Before cutting a release, the following must be true.

### Code

- [ ] `dotnet build` is green: `0 errors, 0 warnings`.
- [ ] The ordinary `dotnet test` suite is green on SQLite.
- [ ] `dotnet test --filter "Database=Sqlite"` passes all
      unit, integration, and architecture tests.
- [ ] EF Core migrations apply cleanly to fresh SQLite, PostgreSQL,
      and MariaDB/MySQL databases.
- [ ] The automated integration matrix is green against real SQLite,
      PostgreSQL, and MariaDB/MySQL engines. A release may not replace
      this gate with provider compilation or a manual smoke test.
- [ ] No `TODO` markers in the changed files of this release.
      (Long-lived TODOs are tracked as GitHub issues.)
- [ ] No `// FIXME` markers without an issue reference.

### Docs

- [ ] `README.md` reflects the current status.
- [ ] [`docs/roadmap/01-implementation-plan.md`](../roadmap/01-implementation-plan.md)
      has the phase status updated.
- [ ] [`CHANGELOG.md`](../../docs/community/CHANGELOG.md) has the
      `[<version>]` section filled in with the
      Added / Changed / Removed / Fixed / Security entries
      for this release.
- [ ] `docs/adr/` has any new ADRs from this release.
- [ ] `docs/architecture/` and `docs/development/` are
      current with the implementation.

### Smoke test

- [ ] Manual end-to-end smoke test on SQLite:
      sign up → workspace → board → list → card → move
      → comment → archive → sign out → sign in tomorrow.
- [ ] For Phase 2+: smoke test the MCP server end-to-end
      with Claude Desktop (stdio) against a seeded
      workspace.
- [ ] For multi-DB: at least one manual smoke test on
      PostgreSQL and one on MariaDB (in Docker).

### Dependencies

- [ ] All NuGet package versions pinned in
      `Directory.Packages.props`.
- [ ] No `dotnet list package --vulnerable --include-transitive`
      warnings remain.
- [ ] `dotnet list package --outdated` reviewed; updates
      applied or filed as a follow-up.

---

## 4. Artifacts

A Cardscape release produces the following artifacts.

### Source

- A **git tag** on the release commit.
- A **GitHub Release** with the changelog excerpt and any
  attached binary artifacts (initially: a `Source code
  (tar.gz)` and `Source code (zip)` produced by GitHub).

### NuGet packages

When the project is multi-project (it is), each public
assembly is a NuGet package.

| Package | Source | Ships in |
|---|---|---|
| `Cardscape.Domain` | `src/Cardscape.Domain` | Phase 1+ |
| `Cardscape.Application` | `src/Cardscape.Application` | Phase 1+ |
| `Cardscape.Infrastructure` | `src/Cardscape.Infrastructure` | Phase 1+ |
| `Cardscape.Api` (meta) | `src/Cardscape.Api` | Phase 1+ |
| `Cardscape.Web` (meta) | `src/Cardscape.Web` | Phase 1+ |
| `Cardscape.Mcp` (meta) | `src/Cardscape.Mcp` | Phase 2+ |

`Api`, `Web`, and `Mcp` are metapackages that depend on
`Application` + `Infrastructure` and on the appropriate
ASP.NET / MCP / Radzen packages. They bundle the deployment
shape.

Packages are pushed to **nuget.org** under the
`cardscape` owner (placeholder — updated when the project
gets a real org).

### Docker images

When the deployment story lands (Phase 1+), the release
produces:

- `ghcr.io/cardscape/cardscape-api:<tag>` — the REST API.
- `ghcr.io/cardscape/cardscape-web:<tag>` — the Blazor WASM
  client (or a static-served variant).
- `ghcr.io/cardscape/cardscape-mcp:<tag>` — the MCP server
  (Phase 2+).
- A `docker-compose.yml` example that wires them together
  with a SQLite or PostgreSQL database.

The `latest` tag tracks the most recent release. Older
versions are kept for one minor version back (e.g. when
`v0.2.0-core-mcp` is out, `v0.1.0-mvp` is also kept; when
`v0.3.0-extensions` is out, `v0.2.0-core-mcp` is kept and
`v0.1.0-mvp` is removed).

---

## 5. Cutting the release

The actual steps. The order matters.

1. **Freeze the changelog.** Finalize the
   `[<version>]` section in
   [`CHANGELOG.md`](../../docs/community/CHANGELOG.md). Commit on master.
2. **Run the pre-release checklist.** See §3.
3. **Create the release-prep branch.**
   `git checkout -b release-prep/v<version> master`.
4. **Bump the version** in:
   - `Directory.Build.props` → `<VersionPrefix>`.
   - `src/Cardscape.Domain/Cardscape.Domain.csproj` →
     `<Version>` (if explicit).
   - `src/Cardscape.Application/...` → same.
   - `src/Cardscape.Infrastructure/...` → same.
   - `src/Cardscape.Api/...` → same.
   - `src/Cardscape.Web/...` → same.
   - `src/Cardscape.Mcp/...` → same (Phase 2+).
5. **Update** `docs/roadmap/01-implementation-plan.md` →
   §0 status table: move the phase from "not started" to
   "DONE" with the new tag.
6. **Commit** the version bump on the release-prep branch.
7. **Run** `dotnet build` and `dotnet test` once more.
8. **Merge** the release-prep branch into master with a
   fast-forward or `--no-ff` merge commit. The merge
   commit message is `release: v<version>`.
9. **Tag** the merge commit: `git tag -a v<version> -m
   "v<version>"`. The tag is annotated.
10. **Push** the tag: `git push origin v<version>`. The
    tag triggers the CI release pipeline (added in Phase 1+).
11. **Draft the GitHub Release** on the tag, with the
    changelog excerpt as the body. Title:
    `v<version> — <one-line summary>`. Attach any binary
    artifacts.
12. **Publish the GitHub Release.** This triggers
    notifications for watchers and the announcement discussion.
13. **Delete** the release-prep branch.
14. **Announce** in the `Announcements` Discussion category
    with a link to the GitHub Release.

For Phases 1 and 2 (no CI yet), steps 9-12 are manual. From
Phase 3, the CI pipeline automates the tag → build → push →
release flow.

---

## 6. Hotfix releases

For a critical bug on a released version:

1. **Branch from the tag** (not from master):
   `git checkout -b hotfix/v<x.y.z> v<x.y.(z-1)>`.
2. **Fix the bug.** Add a regression test.
3. **Update** `CHANGELOG.md` with a `Fixed` entry under a
   new `v<x.y.z>` section.
4. **Bump the version** in the same files as §5.
5. **Tag** `v<x.y.z>`.
6. **Push** and draft the GitHub Release.
7. **Merge** the hotfix branch back into master (so master
   has the fix too). The merge may need a rebase if master
   has moved on.

Hotfixes are **not** used for pre-1.0 (`0.y.z`) versions —
pre-1.0, the maintainer just patches master and cuts a new
minor when ready.

---

## 7. Rollback

If a release is broken in production, the rollback is:

1. **Mark the GitHub Release as a pre-release** (it is still
   downloadable, but flagged).
2. **Add a `YANKED` section** to `CHANGELOG.md` with the
   reason and the recommended replacement version.
3. **Cut a hotfix** (§6) as soon as possible.
4. **Announce** in `Announcements`.

Cardscape does not delete releases or rewrite tags. Once
tagged, the artifact stays. The community can choose to
pin to the previous version.

---

## 8. Communication

| Event | Where it is announced |
|---|---|
| Major / minor release (Phase completion) | GitHub Release + Announcements Discussion + (later) blog post |
| Patch release (hotfix) | GitHub Release + Announcements Discussion |
| Pre-release tag (alpha, beta, RC) | not used until post-1.0 |
| Deprecation of a feature | `CHANGELOG.md` `Deprecated` section + Announcements Discussion, with the replacement feature and the timeline |
| Removal of a deprecated feature | the version that removes it has a `Removed` entry; Announcements Discussion recap |

---

## 9. What this document does not cover

- **Automated CI release pipelines.** Added with Phase 1.
  This document describes the manual process for the
  pre-Phase-1 era.
- **NuGet package signing.** The project does not sign
  packages today. When the community asks for it (or
  before the first 1.0), this section gets a "Package
  signing" subsection.
- **Binary reproducibility.** Out of scope for now. If a
  security audit requires reproducible builds, this section
  gets a "Reproducible builds" subsection.
- **Multi-arch Docker images.** All images today are
  `linux/amd64`. `linux/arm64` is added with Phase 1+ (the
  Raspberry Pi / Apple Silicon use case).
