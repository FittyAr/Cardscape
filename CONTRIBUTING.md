# Contributing to Cardscape

Thank you for your interest in Cardscape. This project is
**solo-maintained** today but explicitly wants a community to
form around it. The bar is "think big and professional": ADR-grade
decisions, polished documentation, an architecture that scales,
a UX that competes with hosted kanban tools, and an AI integration
no other self-hostable kanban has.

This file is the formal contribution flow. The working rules every
contributor (human or AI agent) reads first are in
[`docs/AGENTS.md`](docs/AGENTS.md). **Read that first** — it is the
contract for how work is done on this codebase.

---

## 1. Code of Conduct

By participating, you agree to the
[Contributor Covenant v2.1](CODE_OF_CONDUCT.md). Be kind. Be
technical. Disagree on the merits, not on the person. Enforcement
is by the maintainer; reports go to the address listed in the
Code of Conduct.

---

## 2. Where to start

The best first contribution is one of:

- **A failing test** for a documented behavior. Tests live under
  `tests/`. The test matrix is SQLite-only today; see
  [`docs/development/03-testing-strategy.md`](docs/development/03-testing-strategy.md)
  for the convention.
- **A documentation fix** — a typo, a broken link, a missing
  example. The docs are read often; small fixes land fast.
- **A "good first issue"** — tagged in the issue tracker. These
  are scoped, well-described, and a good way to learn the
  codebase.

If you are an AI agent picking up a task: read
[`.agents/AGENTS.md`](.agents/AGENTS.md) first (the operational
contract your tool reads at runtime), then
[`docs/AGENTS.md`](docs/AGENTS.md) for the human-friendly
rendering.

---

## 3. How to file a good issue

We use three issue templates. **Pick the right one** — a bug
filed as a feature request gets triaged slower than a bug filed
as a bug.

| Template | Use when |
|---|---|
| **Bug report** | Something is broken, behaves wrong, or crashes |
| **Feature request** | You want a new feature, or a change to an existing one |
| **Question** | You have a how-do-I question that is not a bug and not a feature request |

For general discussion, idea exploration, or "what do you think
about X", use **GitHub Discussions** instead — it is async, it
is searchable, and it does not get closed when the issue is
resolved. The categories are `Announcements`, `Ideas`,
`Q&A`, and `Show and tell`.

### What makes a great bug report

1. **What you did** (the steps, in order, that lead to the bug).
2. **What you expected** (the correct behavior).
3. **What happened** (the actual behavior, including any error
   message, log line, or screenshot).
4. **Your environment** (OS, .NET SDK version, database
   provider, browser if relevant).
5. **Reproduction rate** (always? 1 in 5? once and never again?).

The "always include the version" rule is non-negotiable. Tag
the commit SHA, the release tag, or the branch.

### What makes a great feature request

1. **The problem you are trying to solve** (not the solution you
   have in mind).
2. **The user story** ("as a X, I want to Y, so that Z").
3. **The proposed solution** (optional, but it helps the
   discussion).
4. **Alternatives you considered** (what else could solve the
   problem?).
5. **The scope** (small, medium, large; which phase; who is the
   target user).

We follow the rule: **propose the problem first, the solution
second**. A solution without a problem is just a wish.

---

## 4. How to file a good pull request

### Before you start

1. **Open or comment on an issue first** for non-trivial changes.
   A PR for a feature nobody agreed on wastes everyone's time.
2. **Read the working contract**: [`docs/AGENTS.md`](docs/AGENTS.md).
3. **Read the relevant design doc**:
   - New feature? Read [`docs/development/02-vertical-slices.md`](docs/development/02-vertical-slices.md).
   - MCP work? Read [`docs/architecture/03-mcp-server.md`](docs/architecture/03-mcp-server.md) and [ADR 0002](docs/adr/0002-mcp-server.md).
   - Persistence work? Read [ADR 0001](docs/adr/0001-multi-provider-strategy.md) and [`docs/architecture/02-multi-provider-persistence.md`](docs/architecture/02-multi-provider-persistence.md).
4. **Check the PR template** (`.github/PULL_REQUEST_TEMPLATE.md`)
   and fill it out completely.

### The PR itself

- **One logical change per PR.** A PR that fixes a typo, refactors
  a class, and adds a new feature is hard to review and hard to
  revert. Split it.
- **Small PRs land fast.** Aim for under 400 lines of diff. If
  the change is larger, break it into stacked PRs.
- **Tests included.** Every PR that changes behavior includes
  the test that proves the behavior. No code without tests.
- **Docs updated.** Every PR that changes public behavior
  updates the relevant doc. Every new feature gets a section
  in the implementation plan or the feature inventory.
- **Build green.** `dotnet build` and `dotnet test` both pass
  locally before you push.
- **Commit messages follow the convention** (see §5 below).
- **No provider-specific code paths** without a comment
  explaining why the abstraction failed, per the working rules.

### Branching and rebasing

- Branch off `master` with a descriptive name:
  - `feat/card-snooze` for a new feature.
  - `fix/board-filter-overflow` for a bug fix.
  - `docs/adr-0003-event-sourcing` for documentation.
  - `chore/upgrade-ef-10.0.11` for maintenance.
- Rebase on `master` before requesting review. A clean
  linear history is much easier to review than a tangled one.
- Do not mix rebase and merge in the same PR.

---

## 5. Commit message convention

We use **Conventional Commits** with the project-specific
scopes from the working contract.

Format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

Types:

| Type | Use for |
|---|---|
| `feat` | a new feature (user-facing or API-facing) |
| `fix` | a bug fix |
| `docs` | documentation only |
| `refactor` | code change that neither fixes a bug nor adds a feature |
| `test` | adding or correcting tests |
| `chore` | tooling, dependencies, maintenance |
| `perf` | performance improvement |
| `build` | build system, CI, packaging |
| `ci` | CI configuration |

Scopes used in this project: `domain`, `application`,
`infrastructure`, `api`, `web`, `mcp`, `docs`, `release`,
`infra`, `db`.

Subject: imperative mood, lowercase, no period, max 72 chars.
Body: explain **what** and **why**, not **how**. Reference the
issue, ADR, or design doc that motivates the change.

Examples:

```
feat(mcp): add cards_move_by_label tool

Adds a tool that moves every card with a given label to a
target list in one call. Used by the weekly-review prompt
to clean up the urgent queue.

Refs: ADR 0002, #142
```

```
fix(web): board view drag-and-drop drops on touch devices

The drag handle was below the 44px touch target threshold.
Resize the handle and add a touchstart listener to start
the drag.

Fixes: #98
```

---

## 6. Review process

1. **Open the PR.** Fill out the template. Link the issue.
2. **CI runs.** Build, test, lint, architecture tests. The PR
   cannot be merged with a red CI.
3. **Maintainer review.** First pass within 3 business days.
   Expect a request for changes; that is the norm, not the
   exception.
4. **Iterate.** Push new commits to the same branch; the PR
   updates automatically.
5. **Approval.** Once CI is green and at least one maintainer
   approves, the PR is squash-merged.
6. **Post-merge.** The branch is deleted. The issue is closed
   with a reference to the merged commit.

Reviews are technical, not personal. Comments are about the
code, not the author. The maintainer reserves the right to
reject a PR that is out of scope, conflicts with the
architecture, or does not have a passing test suite.

---

## 7. Release process

The release process is documented in
[`docs/development/04-release-process.md`](docs/development/04-release-process.md).
The short version:

- Versioning: Semantic Versioning (`MAJOR.MINOR.PATCH`).
- Tags: `v0.1.0-mvp`, `v0.2.0-core-mcp`, etc.
- Artifacts: source tarball, NuGet packages, Docker images.
- Cadence: roughly aligned with phase completion; the bar is
  "ready to ship", not "calendar says so".

---

## 8. Security

If you find a security vulnerability, **do not file a public
issue**. Read [`SECURITY.md`](SECURITY.md) for the private
reporting channel.

---

## 9. License

Cardscape is licensed under the
[Reciprocal Public License 1.5 (RPL-1.5)](LICENSE). By
submitting a contribution, you agree that your contribution is
licensed under RPL-1.5 and is compatible with the project's
open-source posture.

The maintainer may, in exceptional cases and with attribution,
include third-party code under a different license. Each
inclusion is documented in `THIRD_PARTY_NOTICES.md` (added
with the first such inclusion).

---

## 10. Community

| Channel | Use for |
|---|---|
| **GitHub Issues** | bugs, feature requests, scoped questions |
| **GitHub Discussions** | general Q&A, ideas, show-and-tell, announcements |
| **PR comments** | review and design discussion on a specific change |
| **Email** | security disclosures only (see `SECURITY.md`) |

There is no Discord, no Slack, no Matrix, no forum today. The
project is small enough that GitHub is the channel. When (if)
the community outgrows GitHub Discussions, the maintainer will
open a dedicated forum and announce it in the Discussions.

---

## 11. Recognition

Contributors are listed in `CONTRIBUTORS.md` (added with the
first external contribution). Significant contributions are
called out in the release notes of the version that ships them.

The maintainer is grateful for every issue filed, every PR
opened, every docs fix, every bug report, every thoughtful
question in Discussions. Solo-maintained does not mean
"contributions are not welcome" — it means the maintainer
triages them, and the project's pace is bounded by the
maintainer's available time. Be patient. Be kind. Be useful.
