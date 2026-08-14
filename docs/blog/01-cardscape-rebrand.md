---
title: "Cardscape rebrand: from clone to standalone kanban with first-class MCP"
date: 2026-07-27
author: the Cardscape maintainer
tags: [rebrand, mcp, kanban, self-hosted, open-source]
summary: >-
  Cardscape used to be positioned as a clone of a popular
  kanban product. We re-thought the positioning from
  scratch, dropped every reference to the product we were
  cloning, and re-positioned Cardscape as a standalone
  kanban and project-management tool with a differentiator
  that no other self-hostable kanban has: a first-class
  Model Context Protocol (MCP) server.
---

# Cardscape rebrand: from clone to standalone kanban with first-class MCP

Today we are rebranding Cardscape. The product itself has
not changed — the source code, the architecture, the
public API, the documentation set all stay the same. What
has changed is **how we talk about the product**: the name
"clone" is gone, the references to the product we were
cloning are gone, the brand names that belonged to that
product are gone, and the positioning is now centered on
the thing that makes Cardscape worth choosing.

This post explains what changed, why, and what it means
for the project going forward.

---

## What changed

Three things, in order of visibility.

### 1. The positioning

Cardscape is no longer "a clone of X" or "an alternative
to X". It is **a self-hostable kanban and project-
management tool with a first-class Model Context Protocol
(MCP) server**. That sentence is the hero of the new
[`README.md`](../../README.md), the new tagline, and the
new product positioning doc
([`docs/roadmap/02-product-positioning.md`](../roadmap/02-product-positioning.md)).

The shift is from "we are like X" to "we are like
nothing else, and here is why". The "why" is the MCP
server: no other self-hostable kanban has one. We are the
only project that ships a first-class integration between
your boards and any AI client that speaks MCP (Claude
Desktop, Cursor, Windsurf, Continue, JetBrains AI, custom
agents).

### 2. The vocabulary

We dropped every reference to the product we were cloning
— including the brand names of features that belonged to
that product ("Butler" for our automation engine, "Power-
Ups" for our extensions, "Atlassian Intelligence" for our
AI features). We replaced them with our own:

| Old (cloning) | New (standalone) |
|---|---|
| "Butler" automation | "Automation" engine |
| "Power-Ups" | "Extensions" |
| "Atlassian Intelligence" | "Cardscape AI" |
| "Kanban-like" / "Kanban alternative" | "self-hostable kanban" / "kanban and project-management tool" |

The cleanup was not just in the new docs. We rewrote
every file in the repository, and we rewrote the git
history (`git filter-branch`) so the previous four commits
also carry the new vocabulary. The project no longer
mentions the product we were cloning anywhere — not in
the source, not in the docs, not in the commit messages,
not in the website.

### 3. The contribution surface

We added the artifacts a community-forming project needs:
a [`CONTRIBUTING.md`](../../CONTRIBUTING.md), a
[`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md)
(Contributor Covenant v2.1), a
[`SECURITY.md`](../../SECURITY.md) with a private
reporting channel, a [`SUPPORT.md`](../../SUPPORT.md)
with a public support matrix, a [`CHANGELOG.md`](../../docs/community/CHANGELOG.md)
in Keep a Changelog format, a [`ROADMAP.md`](../../docs/community/ROADMAP.md)
for the community-readable view of the plan, a
[`MAINTAINERS.md`](../../docs/community/MAINTAINERS.md) and a
[`GOVERNANCE.md`](../../docs/community/GOVERNANCE.md) for how the project
makes decisions today and how it will make them when more
maintainers join. We also added GitHub issue templates
(bug, feature, question), a pull request template, and
four GitHub Discussion categories (announcements, ideas,
Q&A, show-and-tell).

For the project itself, we added the design docs the
implementation will follow: error handling, logging and
observability, auth and authz, accessibility, performance
budgets, feature flags. And the operations docs the first
self-hosting user will need: deployment, backup/restore,
monitoring, incident response. And the security docs:
threat model, secure-coding checklist.

---

## Why now

Two reasons.

The first is that the project is changing audience. We
started as a project for the maintainer ("I want a kanban
I can self-host and drive with my AI"). We are now
becoming a project for the maintainer plus a community
("I want a kanban I can self-host and drive with my AI,
and I want other people who want the same thing to be able
to find it, install it, contribute to it, and trust it").
The two audiences need different positioning. "A clone
of X" works for the first audience; the second audience
needs a standalone product with a clear differentiator.

The second is that the MCP integration is genuinely
unique in the self-hostable kanban space. The other self-
hostable kanban tools (Wekan, Focalboard, Planka,
Leantime, etc.) do not ship an MCP server. The hosted
kanban tools have AI features, but those features are
tied to the vendor's AI and the vendor's data. Cardscape
is the only project that combines:

- **Self-hostable** (your data, your database, your
  hardware).
- **Multi-DB** (SQLite, PostgreSQL, or MariaDB — the
  provider is configuration, not code).
- **First-class MCP** (the AI client drives the boards
  through the same `Application` layer a human does
  through the web UI).
- **Open governance** (RPL-1.5, public ADRs, public
  roadmap).

That is a real differentiator, and it is worth
positioning around.

---

## What it means for the project

Three things.

### The differentiator is the headline

The MCP server ships in **Phase 2** (target: end of
October 2026). The MVP (Phase 1, target: end of August
2026) is the smallest shippable cut — a single user, a
workspace, a board, lists, cards, drag and drop. The MCP
server is the reason someone would pick Cardscape over
another self-hostable kanban, but the MVP is what gets
the project to "runnable" first.

### The community is the multiplier

The project is solo-maintained today. The bar is "think
big and professional": ADR-grade decisions, polished
documentation, an architecture that scales. The
contribution surface (CONTRIBUTING, CoC, SECURITY, the
templates, the design docs) is the work the maintainer
has done to make the project ready for the first
external contributor. The community, when it forms, is
the multiplier that turns a solo project into a project
that lasts.

### The design is the contract

The design docs in `docs/design/` (error handling,
logging, auth/authz, accessibility, performance, feature
flags) are the contract between the maintainer and the
contributors. Every implementation lands against a
documented pattern. Every review checks the
implementation against the pattern. The pattern is the
source of truth; the code is the implementation.

---

## What is next

The next milestone is **Milestone 1: the MVP** (see
[`LAUNCH.md`](../../docs/community/LAUNCH.md) §2). The MVP is the first
runnable build: a single user can sign up, create a
workspace, create a board, add lists and cards, drag
cards between lists, and sign in tomorrow to see the same
state.

If you want to follow along:

- Watch the [GitHub repository](https://github.com/cardscape/cardscape)
  for releases and Discussions.
- Read the [roadmap](https://cardscape.fitty.ar/ROADMAP.md).
- Read the [positioning doc](../roadmap/02-product-positioning.md)
  for the full picture of what Cardscape is and is not.
- Open a Discussion if you have a question, an idea, or
  want to show what you built.

The project is open, the maintainer is reachable, and
contributions are welcome.
