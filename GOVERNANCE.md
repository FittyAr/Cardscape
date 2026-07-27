# Governance

> How decisions are made in Cardscape. The project is
> solo-maintained today; this document describes both the
> current model and the trigger that moves us to a
> multi-maintainer model.

---

## Current model: benevolent dictator

Today, **the maintainer decides**. There is one person with
merge rights on `master` and `site`, one person who cuts
releases, one person who triages issues, one person who
responds to security reports.

This is intentional and is the right model for a project at
this stage. The maintainer is accountable for:

- The technical direction of the project.
- The release cadence and quality bar.
- The community standards (Code of Conduct enforcement).
- The public communication (Announcements, blog, social).

The maintainer is also the bottleneck. This file exists to
make that explicit and to define when the model should change.

---

## What the maintainer does NOT decide alone

There are decisions that benefit from outside review even
today:

- **Security-sensitive changes** (auth, authz, MCP server,
  secrets, cryptography). These are flagged in the PR template
  and get extra review even when the maintainer is the only
  approver.
- **License changes**. The license is RPL-1.5 today. Any
  change requires an ADR and a public discussion in
  Discussions → Announcements.
- **Breaking changes to the public API or the MCP tool
  surface**. These bump the major version and ship an ADR
  explaining the migration path.
- **Additions to the "What we are explicitly NOT building"
  list** in the implementation plan. These are commitments to
  the community and are hard to reverse; they need a
  Discussion and a 7-day comment window before merging.
- **Changes to the project positioning** (name, tagline,
  pillars, vocabulary in
  [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md)).
  These affect every downstream artifact; they need a
  Discussion and the maintainer can be challenged publicly.

For everything else, the maintainer's judgment is final.

---

## How to challenge a decision

If you disagree with a decision the maintainer made:

1. **Open a Discussion** in the
   [Ideas category](https://github.com/cardscape/cardscape/discussions/categories/ideas).
   Frame the disagreement in terms of the project's stated
   goals, not in terms of the maintainer's preferences.
2. **Bring evidence** — links to docs, prior discussions,
   user research, or other projects that have made the
   opposite choice.
3. **Propose the alternative** concretely, with a migration
   path if the change is hard to reverse.
4. **Accept the outcome.** The maintainer reads every
   challenge but does not have to accept it. The maintainer's
   job is to weigh the trade-offs and decide; the
   community's job is to make the trade-offs visible.

The maintainer is not infallible. The maintainer has changed
their mind on real decisions in response to community
feedback. The challenge must be public, evidence-based, and
proportional. Personal attacks, pressure campaigns, and
demands for "democratic" votes on small questions are
counterproductive and may be removed under the Code of
Conduct.

---

## When this changes: the multi-maintainer trigger

The project moves to a **multi-maintainer model** when **all**
of the following are true:

1. There are at least 2 active, sustained contributors
   (6+ months of regular, high-quality PRs) outside the
   current maintainer.
2. The issue tracker has more open issues than the current
   maintainer can triage in a typical week.
3. The current maintainer explicitly decides it is time.

The trigger is set high on purpose. The transition has costs
(coordination overhead, slower decision-making, governance
disputes) and the maintainer only wants to pay them when the
benefits are clear.

When the trigger is met, the project adopts the
**lazy-consensus + quorum** model described below.

---

## Future model: lazy consensus + quorum

The model is described here so the transition is planned,
not improvised. The maintainer commits to following this
model when the trigger is met.

### Lazy consensus

Most decisions are made by **lazy consensus**: a maintainer
proposes a change (PR, ADR, release), waits 5 business days
for objections, and merges if no maintainer objects. This
covers:

- Bug fixes.
- Documentation changes.
- Refactors that do not change behavior.
- Dependency updates within the current major.
- Adding a new test.
- Adding a new bounded context or vertical slice that does
  not break the public API.

### Quorum decisions

A subset of decisions requires **explicit approval from at
least 2 maintainers** (quorum of 2). This covers:

- Breaking changes to the public API.
- Breaking changes to the MCP tool surface.
- License changes.
- New dependencies (a new NuGet package or external service).
- Changes to the project positioning.
- Additions to the "What we are explicitly NOT building"
  list.
- Releases.

The quorum approver is **not** the proposer. So a
single-maintainer project effectively has a higher bar for
quorum decisions (a single maintainer cannot self-approve a
quorum decision; they must wait for a second maintainer).

### Voting (only on deadlock)

If two maintainers disagree and lazy consensus + a good-faith
discussion does not resolve it, the deadlock goes to a vote:

- One maintainer, one vote.
- Majority wins; ties go to the project's stated goals
  ([`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md),
  [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md)),
  not to the longest-tenured maintainer.
- The vote is recorded in the relevant ADR or release notes.

Voting is a **last resort**, not a default. Most decisions
should resolve in the discussion.

### When the multi-maintainer model is fully mature

The fully mature model (5+ maintainers, mature codebase) is
not described here. When the project gets there, the
governance model is revisited and this file gets a §6.

---

## Process for amending this document

This document is itself subject to lazy consensus. A
maintainer proposes an edit as a PR, the comment window is
7 business days (longer than the default 5, because
governance changes deserve more time), and the change merges
on lazy consensus.

Adoption of the multi-maintainer trigger requires explicit
approval from the current maintainer (because the current
maintainer is the one who transitions to the new model).

---

## See also

- [MAINTAINERS.md](MAINTAINERS.md) — who the maintainers are
  and what they do.
- [CONTRIBUTING.md](CONTRIBUTING.md) — the contribution flow.
- [docs/roadmap/01-implementation-plan.md](docs/roadmap/01-implementation-plan.md)
  — the project's stated goals and "what we are NOT building"
  list.
- [docs/roadmap/02-product-positioning.md](docs/roadmap/02-product-positioning.md)
  — the project's stated positioning.
