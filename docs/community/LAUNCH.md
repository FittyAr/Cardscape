# Launch plan

> What "launch" means for Cardscape, what the milestones
> are, and what artifacts ship at each milestone. The
> plan is for a public, community-forming project, not for
> a startup-style growth push. The bar is "credible, open,
> ready for a first external user to install and use", not
> "front page of Hacker News".
>
> This is a **planning** document. It is reviewed and
> updated at the end of every phase.

---

## 1. The principle

A launch is **not** an event. It is a sequence of
**milestones**, each of which is a credible, public
moment for someone to start using the project. The project
launches incrementally, not in a single big-bang.

The milestones are not tied to calendar dates. They are
tied to **what the project can credibly deliver to a real
user**. A milestone that is "December 15" but the project
is not ready is delayed; a milestone that is "today, the
project is ready" ships today.

---

## 2. The milestones

### Milestone 0 — Repository and docs (✅ done)

The repository exists. The README, the docs set, the
positioning, the governance, the community files, the
brand kit, the release process, the design docs, the
threat model, the i18n policy, the contributor DX (Husky,
dev container), and the website are in place. The
build is green. The history is clean of vendor references.

**The artifact**: a public repository that a developer
can clone, build, and read.

**The audience**: developers evaluating the project; the
maintainer's peers; the maintainer's future self looking
back.

**The "is it ready" check**: a developer can clone the
repo, run `dotnet build`, see 0 errors, and read the
README in 5 minutes.

---

### Milestone 1 — MVP (the first runnable build)

The Phase 1 deliverable: a single user can sign up,
create a workspace, create a board, add lists and cards,
drag cards between lists, and sign in tomorrow to see the
same state. The release is `v0.1.0-mvp`.

**The artifact**: a tagged release with a `docker-compose.yml`
that brings up the API, the web client, and a SQLite
database; a 5-minute quickstart in the README; a
screencast or screenshot of the board view.

**The audience**: the maintainer (as a real user); a
small set of alpha testers (the maintainer's peers,
recruited via the maintainer's network).

**The "is it ready" check**:
- The maintainer uses the MVP for a week for a real
  project (the maintainer's own work, not a demo).
- A peer can install via `docker compose up` in 5 minutes
  and use the board view without help.
- The first runnable build has zero P0 bugs.

**The announcement**: a Discussion in the `Announcements`
category. No blog post, no social media push, no
"ProductHunt". The audience is small; the project is
honest about it.

---

### Milestone 2 — MCP server (the differentiator)

The Phase 2 deliverable: the MCP server is wired end-to-end,
the AI client (Claude Desktop, Cursor) can drive the
boards, the first set of MCP tools, resources, and prompts
ship, the idempotency story is in place, the OTel
instrumentation is complete. The release is
`v0.2.0-core-mcp`.

**The artifact**: the same as Milestone 1, plus an
"AI-driven" demo: a screencast of an AI client asking
"show me all the cards assigned to me that are due this
week" and the AI client producing the answer. A
configuration guide for Claude Desktop and Cursor.

**The audience**: the maintainer; the alpha testers; the
"self-hostable kanban" community (the people who maintain
or use Wekan, Focalboard, Planka, Leantime, etc.); the
"Anthropic / MCP" community (the people who follow the
Model Context Protocol).

**The "is it ready" check**:
- The MCP server passes the smoke test in
  [`docs/ai/02-prompt-library.md`](docs/ai/02-prompt-library.md).
- An alpha tester can configure Claude Desktop to talk
  to their self-hosted Cardscape and run a prompt
  successfully.
- The first end-to-end demo (AI client → MCP → Cardscape
  → DB) is on the public website.

**The announcement**: a blog post titled "Cardscape ships
a Model Context Protocol server. No other self-hostable
kanban does this." The blog post links to the demo, the
docs, the release, and the configuration guide. The
announcement is shared on:
- The project's GitHub Discussions.
- The relevant subreddits
  (`r/selfhosted`, `r/kanban`, `r/dotnet`).
- Hacker News (`Show HN: Cardscape – a self-hostable
  kanban with a first-class MCP server`), with a
  well-prepared "what is this" comment from the
  maintainer.
- The maintainer's Twitter / Mastodon / LinkedIn.

This is the first milestone with a public-announcement
budget (a few hours of the maintainer's time, no money).

---

### Milestone 3 — Extensions and automation

The Phase 3 deliverable: the extension framework, the
first-party extensions (Calendar, Table, Timeline,
Dashboard, Custom Fields, Card Aging/Snooze/Repeater,
Voting, List Limits, Dashcards), the automation engine
(rules, buttons, scheduled commands), the first-party
integrations (Webhooks, iCalendar, Slack, Google Drive,
GitHub, email-to-board). The release is
`v0.3.0-extensions`.

**The artifact**: the same as Milestone 2, plus a
"showcase" page on the website (screenshots of each
extension in use), the OpenAPI spec, the personal
access tokens, the OAuth flow for third-party apps.

**The audience**: the maintainer; the alpha testers; the
broader "self-hosted productivity" community; the
"automation" community (the people who use n8n, Huginn,
Home Assistant, etc.).

**The "is it ready" check**:
- Each first-party extension has at least one alpha
  tester who uses it weekly.
- The automation engine has a "starter rules" library
  (10+ rules, the most common ones: "when a card is moved
  to Done, post a comment", "every Monday at 9am, archive
  cards not touched in 30 days", etc.).
- The OpenAPI spec is published to a public URL.

**The announcement**: a blog post titled "Cardscape
ships an extension framework, a full automation engine,
and a Calendar extension that closes the gap with hosted
kanban tools." The blog post links to the showcase, the
extension docs, and the release. The audience is the same
as Milestone 2, plus:
- Indie Hackers (for the "solo developer ships a
  full-featured product" angle).
- The maintainer's blog, if the maintainer has one.

---

### Milestone 4 — Enterprise and AI features

The Phase 4 deliverable: OAuth/OIDC/SSO, 2FA, audit logs,
SCIM provisioning, data residency, Inbox + Planner, Google
Calendar sync, Cardscape AI features. The release is
`v0.4.0-enterprise`.

**The artifact**: the same as Milestone 3, plus a
"security" page on the website (the security baseline, the
audit log, the compliance posture), the
Cardscape AI features (card description generation,
comment summary, auto-checklists), the MCP tools for AI
queries.

**The audience**: the maintainer; the alpha testers; small
and mid-size teams looking for a self-hostable kanban;
the "enterprise" community (the people who would never
adopt a hosted kanban for compliance reasons).

**The "is it ready" check**:
- A 50-person team can install, configure SSO, set up
  audit log forwarding, and use the AI features without
  the maintainer's help.
- A security review (third-party, if the budget allows)
  finds no P0 or P1 issues.

**The announcement**: a blog post titled "Cardscape for
teams: SSO, audit logs, and the first wave of Cardscape AI
features." The audience is the same as Milestone 3, plus:
- The "enterprise open source" community (the people
  who follow the CNCF, the Linux Foundation, the OSI).
- The relevant podcasts (Changelog, .NET Rocks, etc., if
  the maintainer has the time and the relationship).

---

### Milestone 5 — Polish, scale, and the long tail

The Phase 5 deliverable: i18n, theming, performance,
background jobs, MCP subscriptions, import/export, the C#
API client SDK, public status page, security audit, pen
test, SOC 2 / GDPR compliance docs.

This milestone is **ongoing**. The artifact is the same
project, but more polished, more documented, more
performant, and more compliant. There is no single
"launch" moment. There is a continuous improvement loop.

The first **stable** release is `v1.0.0`, which marks
the point at which the public API and the schema are
stable. The trigger for `v1.0.0` is:

- The public API has been stable for one minor version
  (e.g. `v0.4.x` is the last "moving fast" minor).
- The schema migrations are reversible.
- The deprecation policy is in place.
- The security audit is clean.
- The maintainer commits to backward compatibility from
  `v1.0.0` forward.

**The announcement**: a blog post titled "Cardscape
1.0: a stable, self-hostable kanban with first-class
MCP support." The blog post is the public commitment to
the project's API stability. The audience is the same as
Milestone 4, plus:
- The .NET community at large (a stable, polished
  open-source product on .NET is a rare thing).
- The general "open source" community (a 1.0 release
  is a milestone regardless of the project).

---

## 3. The communication channels

| Milestone | GitHub Discussions | Blog | Subreddits | HN | Twitter / Mastodon |
|---|---|---|---|---|---|
| 0 | no | no | no | no | no |
| 1 | yes | no | no | no | optional |
| 2 | yes | yes | yes | yes | yes |
| 3 | yes | yes | yes | optional | yes |
| 4 | yes | yes | yes | optional | yes |
| 5 (1.0) | yes | yes | yes | yes | yes |

The "yes" is the maintainer's responsibility. The
"optional" is "if the maintainer has the time and the
audience". The "no" is "the audience is too small; the
post would be noise".

---

## 4. The risks

| Risk | Mitigation |
|---|---|
| The project ships a milestone but the alpha tester finds a P0 bug the maintainer did not catch | the alpha tester runs the milestone for a week before the announcement; the announcement is delayed if the bug is found |
| The maintainer runs out of time and the project stalls | the project is solo; the maintainer is honest about the pace; the milestones are aspirational, not promised |
| The community does not form | the maintainer does not depend on the community; the project is useful to the maintainer regardless |
| A competitor ships a similar MCP integration | the differentiator is the combination of self-hostable + multi-DB + complete feature surface, not the MCP integration alone; the maintainer adapts the positioning in [docs/roadmap/02-product-positioning.md](docs/roadmap/02-product-positioning.md) if the landscape changes |
| A security vulnerability is found in a published release | the [release process](docs/development/04-release-process.md) §6 covers the hotfix flow; the [security policy](SECURITY.md) covers the private reporting channel |

---

## 5. When to revisit

This document is revisited:

- At the end of every phase (the next milestone is set).
- When a milestone is achieved (the milestone is marked
  done, the next one is set).
- When the project's audience changes materially (e.g.
  the project is mentioned in a major publication; the
  audience grows from 10 to 1000).
- When a competitor or a complementary project ships a
  feature that changes the positioning.

Until then, this document is the source of truth for the
launch plan in Cardscape.
