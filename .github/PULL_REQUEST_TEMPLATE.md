---
name: Pull request
about: Open a pull request that implements a feature, fixes a bug, or improves the docs
title: ""
labels: []
assignees: []
---

## Summary

One or two sentences on **what** this PR does and **why**.

## Linked issues

Closes # (replace with the issue number this PR closes)
Relates to # (replace with related issues, if any)

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing
      functionality to change)
- [ ] Documentation only
- [ ] Refactor (no functional change)
- [ ] Test only
- [ ] Chore / maintenance

## What changed

A short list of the changes, scoped to one logical unit. A
reviewer should be able to read this list and understand the
PR without opening the diff.

- ...
- ...
- ...

## How it was tested

- [ ] Unit tests added or updated
- [ ] Integration tests added or updated
- [ ] Architecture tests still pass
- [ ] Manual smoke test: (describe what you did)
- [ ] `dotnet build` is green locally
- [ ] `dotnet test` is green locally

## Docs updated

- [ ] `README.md` updated (if user-facing change)
- [ ] `docs/AGENTS.md` updated (if working rules changed)
- [ ] `docs/roadmap/00-feature-inventory.md` updated (if the
      feature surface changed)
- [ ] `docs/roadmap/01-implementation-plan.md` updated (if a
      phase scope changed)
- [ ] `docs/roadmap/02-product-positioning.md` updated (if
      positioning changed)
- [ ] New ADR under `docs/adr/` (if a new architectural
      decision was made)
- [ ] Inline XML doc comments on new public APIs
- [ ] No doc changes needed (explain why)

## Breaking changes

If this PR introduces a breaking change, call it out clearly:

- What breaks: ...
- Who is affected: ...
- Migration path: ...

## Checklist

- [ ] I have read [`docs/AGENTS.md`](docs/AGENTS.md) and
      followed the working rules.
- [ ] I have read [CONTRIBUTING.md](CONTRIBUTING.md) and
      followed the contribution flow.
- [ ] My commit messages follow the project's commit
      convention (Conventional Commits with the project
      scopes).
- [ ] My branch is rebased on the current `master`.
- [ ] CI is green on this PR.

## Notes for the reviewer

Anything the reviewer should pay particular attention to, or
context that does not fit anywhere else.
