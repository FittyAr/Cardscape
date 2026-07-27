# Comparison: how Cardscape is different

> A vendor-neutral, **feature-axis** comparison of
> Cardscape against other self-hostable project-management
> tools. The comparison is by **what the project does**,
> not by **which product**. It is designed to help a
> prospective user decide whether Cardscape is the right
> tool for them, and to help a prospective contributor
> decide whether Cardscape is the right project for them.
>
> The comparison is **not** a "we are better than them"
> page. It is a "we are different from them" page, and
> the differences are explained. Some differences favor
> Cardscape; some do not. The reader decides.

---

## 1. The comparison axis

The page is organized by **what a self-hostable project-
management tool does**. Each axis is described in
vendor-neutral terms; the cells describe how Cardscape
handles it.

The axes:

1. **Data sovereignty** — who owns the data, where it
   lives, who can read it.
2. **Database** — which engines are supported, how the
   provider is selected.
3. **AI integration** — what AI the tool ships, how the
   AI is exposed, what data leaves the user's instance.
4. **Feature surface** — the set of features the tool
   ships.
5. **Extensibility** — how the tool grows beyond its
   built-in features.
6. **Automation** — how the tool automates repetitive
   work.
7. **Collaboration** — how the tool handles multiple
   users, real-time updates, and notifications.
8. **Mobile** — how the tool runs on phones and tablets.
9. **Authentication** — how the tool authenticates users.
10. **Developer-facing** — the API, the SDKs, the
    integrations for developers.
11. **License** — how the tool is licensed, what
    redistributors must do.
12. **Stack** — what the tool is built on, what
    long-term-support story it has.
13. **Governance** — how the project makes decisions, how
    contributors are added.

A reader picks the axes that matter to them and reads
those cells.

---

## 2. Data sovereignty

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Where the data lives | the user's hardware | the user's hardware | the vendor's cloud |
| Who can read the data | the user (and the user's chosen admins) | the user | the vendor + subpoenas + the vendor's employees |
| Vendor lock-in | none (the schema is open; the data is portable) | none | high (export is partial, switching cost is high) |
| Backup is the user's responsibility | yes | yes | no (the vendor backs up) |
| Disaster recovery is the user's responsibility | yes | yes | no |

Cardscape's position: **you own the data**. The trade-off
is that you also own the backup, the disaster recovery,
and the security of the database. We give you the tools
([`docs/operations/02-backup-restore.md`](../operations/02-backup-restore.md));
you run them.

---

## 3. Database

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| SQLite (single file, zero config) | ✅ yes | often ✅ | n/a (the vendor runs the database) |
| PostgreSQL | ✅ yes | sometimes | the vendor's choice |
| MariaDB / MySQL | ✅ yes | sometimes | the vendor's choice |
| Other (MongoDB, DynamoDB, Cosmos) | ❌ no | sometimes | the vendor's choice |
| Provider is configuration, not code | ✅ yes | often no (the project is hard-coded to one provider) | n/a |
| Multi-DB test matrix | SQLite-only today (the multi-DB plumbing is in place; the test matrix grows as the providers stabilize) | usually one provider tested | n/a |

Cardscape's position: **design for three, test on one**.
The runtime supports three relational database engines; the
test matrix is SQLite-only today. The other providers
gain tests as their EF Core providers stabilize (see
[ADR 0001](../adr/0001-multi-provider-strategy.md)).

---

## 4. AI integration

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| AI drives the boards | ✅ yes (MCP server) | ❌ no (or limited to a UI suggestion) | ⚠️ sometimes (the vendor's AI, on the vendor's data) |
| AI is a first-class principal | ✅ yes (API tokens, scopes, audit) | ❌ no | ⚠️ sometimes |
| AI uses the same auth as humans | ✅ yes (the same `Application` layer, the same policies) | ❌ no | ⚠️ sometimes (the AI uses a separate "AI" key) |
| AI is open protocol (MCP) | ✅ yes (the .NET MCP SDK) | ❌ no | ⚠️ rarely (the vendor's protocol, not MCP) |
| AI data stays in the user's instance by default | ✅ yes | n/a | ❌ no (the data goes to the vendor's AI) |
| Bring-your-own AI provider (BYOK) | ✅ yes (planned, Phase 4) | n/a | ❌ no (the vendor's AI only) |

Cardscape's position: **the AI is a first-class user, not
a wrapper**. The MCP server is the reason Cardscape is
worth choosing over every other self-hostable kanban. See
[ADR 0002](../adr/0002-mcp-server.md) and
[`docs/ai/01-mcp-deep-dive.md`](../ai/01-mcp-deep-dive.md).

---

## 5. Feature surface

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Workspaces | ✅ yes | often ✅ | ✅ yes |
| Boards | ✅ yes | ✅ yes | ✅ yes |
| Lists | ✅ yes | ✅ yes | ✅ yes |
| Cards | ✅ yes | ✅ yes | ✅ yes |
| Members | ✅ yes | ✅ yes | ✅ yes |
| Comments | ✅ yes | ✅ yes | ✅ yes |
| Attachments | ✅ yes | ✅ yes | ✅ yes |
| Checklists | ✅ yes | ✅ yes | ✅ yes |
| Custom fields | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Inbox | ✅ yes (Phase 4) | sometimes | ✅ yes |
| Planner | ✅ yes (Phase 4) | rarely | ✅ yes |
| Multiple views (Board, Calendar, Table, Timeline, Dashboard) | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Mobile (responsive + PWA) | ✅ yes (Phase 2-3) | sometimes | ✅ yes |
| Mobile native apps | ❌ no (planned PWA-only) | sometimes | ✅ yes |

The full feature inventory is in
[`docs/roadmap/00-feature-inventory.md`](../roadmap/00-feature-inventory.md).
Cardscape's position: **a complete feature surface, not a
demo**. The bar is "everything a team of 50 needs in a
project-management tool", not "the smallest thing that
compiles".

---

## 6. Extensibility

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Extension framework | ✅ yes (`IExtension`, Phase 3) | sometimes (plugin / hook / module) | ✅ yes (app marketplace) |
| First-party extensions | ✅ yes (Calendar, Table, Timeline, Dashboard, …) | varies | ✅ yes |
| Third-party extensions | ❌ no marketplace; the framework is public so self-hosters can build their own | varies | ✅ yes (the vendor's marketplace) |
| Webhooks | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Public REST API | ✅ yes | sometimes | ✅ yes |
| MCP server | ✅ yes (the differentiator) | ❌ no | ⚠️ rarely |

Cardscape's position: **extensions are first-party only,
but the framework is public**. Self-hosters can build
their own; the maintainer does not curate or distribute
third-party extensions. This is a deliberate choice (no
marketplace, no curation overhead, no security review
process for unknown code). See
[`docs/roadmap/01-implementation-plan.md`](../roadmap/01-implementation-plan.md)
§7 "What we are explicitly NOT building".

---

## 7. Automation

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Rules (trigger → action) | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Custom buttons | ✅ yes (Phase 3) | rarely | ✅ yes |
| Scheduled commands (cron-like) | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Built-in actions (move, archive, label, assign, comment, due date) | ✅ yes (Phase 3) | varies | ✅ yes |
| Quotas (per-user, per-month) | ✅ yes (configurable; default 250 / month) | rarely | varies |
| User-owned scripting | ❌ no (closed rule system) | varies | ⚠️ rarely |

Cardscape's position: **a closed rule system, not a
scripting language**. The rule system is enough for the
90% case; the 10% case is a code change to the
automation engine itself, not a user-written script. This
trade-off favors security (no arbitrary code execution
per user) over flexibility (no "if the user can dream it,
they can automate it").

---

## 8. Collaboration

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Multiple users per workspace | ✅ yes (Phase 1) | ✅ yes | ✅ yes |
| Roles (admin / member / observer) | ✅ yes (Phase 1) | ✅ yes | ✅ yes |
| @mentions | ✅ yes (Phase 2) | ✅ yes | ✅ yes |
| Reactions on comments | ✅ yes (Phase 2) | ✅ yes | ✅ yes |
| Watch / un-watch | ✅ yes (Phase 2) | sometimes | ✅ yes |
| Real-time (SignalR) | ✅ yes (Phase 2) | sometimes | ✅ yes |
| Email notifications | ✅ yes (Phase 2) | sometimes | ✅ yes |
| Presence indicators | ✅ yes (Phase 2) | rarely | ✅ yes |
| Typing indicators | ✅ yes (Phase 2) | rarely | ✅ yes |

---

## 9. Mobile

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Responsive web | ✅ yes (Phase 2) | often ✅ | ✅ yes |
| PWA (installable, offline shell) | ✅ yes (Phase 2) | sometimes | ✅ yes |
| Native iOS app | ❌ no (planned PWA-only for Phase 5) | sometimes | ✅ yes |
| Native Android app | ❌ no (planned PWA-only for Phase 5) | sometimes | ✅ yes |

Cardscape's position: **PWA is enough for the 80% case**.
Native apps are planned as a future option if the
community asks for them. The PWA path is the
cost-effective choice for a solo-maintained project.

---

## 10. Authentication

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Email + password | ✅ yes (Phase 1) | ✅ yes | ✅ yes |
| OAuth (Google, Microsoft, Apple) | ✅ yes (Phase 4) | sometimes | ✅ yes |
| SAML SSO | ✅ yes (Phase 4) | rarely | ✅ yes |
| Two-factor authentication (TOTP) | ✅ yes (Phase 4) | rarely | ✅ yes |
| API tokens (personal access) | ✅ yes (Phase 2) | sometimes | ✅ yes |
| Audit logs | ✅ yes (Phase 4) | rarely | ✅ yes |

See [`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md)
for the full design.

---

## 11. Developer-facing

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Public REST API | ✅ yes | sometimes | ✅ yes |
| OpenAPI spec | ✅ yes (Phase 3) | sometimes | ✅ yes |
| Webhooks | ✅ yes (Phase 3) | sometimes | ✅ yes |
| OAuth for third-party apps | ✅ yes (Phase 3) | rarely | ✅ yes |
| C# API client SDK | ✅ yes (Phase 5) | rarely | varies |
| MCP server (AI clients) | ✅ yes (Phase 2) | ❌ no | ⚠️ rarely |

---

## 12. License

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| License | RPL-1.5 (Reciprocal Public License 1.5) | varies (MIT, Apache 2.0, AGPL) | proprietary |
| Can be used commercially | ✅ yes | ✅ yes | n/a (the user is a customer) |
| Can be modified | ✅ yes | ✅ yes | ❌ no |
| Modifications must be open | ✅ yes (the reciprocal clause) | varies | n/a |
| Can be sold as a hosted service | ⚠️ yes, with the reciprocity clause (the hosted service must publish the modifications) | varies | n/a |

Cardscape's position: **RPL-1.5, deliberately**. The
reciprocity clause prevents the "fork the code and close
it" move. The project is built in the open; improvements
stay in the open. RPL-1.5 is OSI-approved and is the
right license for a project that wants to stay open while
preventing proprietary forks.

---

## 13. Stack

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Runtime | .NET 11 | varies (Node, Python, Go, Java, PHP) | varies |
| UI framework | Blazor WebAssembly + Radzen.Blazor | varies (React, Vue, Svelte) | varies |
| ORM | Entity Framework Core 10 LTS | varies (Django ORM, Prisma, etc.) | varies |
| Long-term support story | strong (.NET LTS, EF Core LTS, Radzen has a stable release cadence) | varies | n/a |

Cardscape's position: **modern .NET, end to end**. The
choice of .NET is deliberate — the .NET LTS story means
the project will build on a supported runtime for years.
The other self-hostable kanban tools are mostly in
Node/Python/Go/PHP, which have different support
cadences.

---

## 14. Governance

| Axis | Cardscape | Other self-hostable kanban (typical) | Hosted kanban (typical) |
|---|---|---|---|
| Decision model | solo maintainer today; lazy consensus + quorum when the project outgrows one person (see [GOVERNANCE.md](../../GOVERNANCE.md)) | varies (BDFL, foundation, vendor) | the vendor |
| Public roadmap | ✅ yes ([`ROADMAP.md`](../../ROADMAP.md)) | varies | rarely (the vendor's roadmap) |
| Public ADRs | ✅ yes ([`docs/adr/`](../adr/)) | rarely | rarely |
| Contribution flow documented | ✅ yes ([`CONTRIBUTING.md`](../../CONTRIBUTING.md)) | varies | n/a |
| Code of Conduct | ✅ yes (Contributor Covenant v2.1) | varies | n/a |
| Security disclosure channel | ✅ yes ([`SECURITY.md`](../../SECURITY.md)) | varies | varies |
| Private reporting email | `security@fitty.ar` | varies | varies |

---

## 15. The one-paragraph summary

> Cardscape is a self-hostable kanban and project-management
> tool, built on .NET 11, with a first-class Model Context
> Protocol server. It is the only self-hostable kanban
> that lets an AI client (Claude Desktop, Cursor, etc.)
> drive the boards through the same `Application` layer a
> human does through the web UI. It is licensed under
> RPL-1.5, governed by a solo maintainer with a published
> plan to become a multi-maintainer project, and positioned
> for users who want a complete feature surface, a credible
> AI integration, and full data ownership — without the
> vendor lock-in of a hosted kanban.

If that paragraph is what you are looking for, Cardscape
is for you. If not, one of the other self-hostable kanban
tools is probably a better fit — and that is a fine
outcome.
