# Product positioning

> The design doc that defines **how Cardscape presents itself**:
> the name, the tagline, the positioning pillars, the
> vocabulary, and the voice. Every other artifact (the root
> `README.md`, the docs, the AGENTS contract, future blog
> posts, the public website) draws from this file.

This is a **design** document. It is not implemented as code.
It exists so the maintainer (and any future contributor) makes
consistent choices when writing about the project.

---

## 1. Why this document exists

A solo-maintained, public, open-source project lives or dies on
how it presents itself in three places:

1. The **GitHub repo page** — what a developer sees in the
   first 5 seconds.
2. The **README hero** — the line that decides whether they
   stay or bounce.
3. The **search result** — the snippet that decides whether
   they click at all.

Those three places are all written from the same source. This
doc is that source.

If the name changes, every doc that mentions "Cardscape" has
to be updated. If the tagline changes, the README hero changes.
If a vocabulary word becomes off-limits (e.g. a competitor's
brand name), this doc is where that decision is recorded.

---

## 2. The name — "Cardscape"

### 2.1 What it is

**Cardscape** is a portmanteau of **card** (the atomic unit of
a kanban) and **-scape** (the suffix that means "a wide view
of a thing" — soundscape, cityscape, landscape, mindscape).

It evokes:

- The card as the center of the model.
- A wide, composed view of the work — not a single board, not
  a single list, the whole landscape of the project.
- A noun, not a verb — Cardscape **is** a thing, not a thing
  you do.

### 2.2 Naming principles it satisfies

A good name for this project must:

| Principle | Cardscape |
|---|---|
| Memorable, one word | ✅ one word, six syllables, easy to say |
| Distinct, not generic | ✅ "Cardscape" is not a generic English word; it does not collide with any major product |
| Vendor-neutral | ✅ does not reference any other kanban or project-management product |
| Pronounceable in EN / ES / FR | ✅ "card-scape" reads the same way in all three |
| Easy to spell after hearing it once | ✅ phonetic, no silent letters |
| Plausible as a CLI binary | ✅ `cardscape`, `cardscape-mcp` |
| Plausible as a domain | ✅ `cardscape.dev`, `cardscape.io`, `cardscape.app` (availability TBD) |
| Plausible as a Docker image | ✅ `ghcr.io/<owner>/cardscape` |
| Plausible as a GitHub org or repo | ✅ already the repo name; works as an org too |
| Holds up as the project grows beyond kanban | ✅ "-scape" is generic enough to absorb calendar, automation, AI, etc. |

### 2.3 Alternatives considered (and why we kept "Cardscape")

For the record, the following names were considered before
the project shipped its first commit. None displaced
"Cardscape".

| Candidate | Why not |
|---|---|
| `Cardstack` | already a real product (a blockchain company); collision |
| `Carddeck` | too playful; "deck" implies cards, not boards; misleading |
| `Cardline` | too narrow — implies a single sequence, not a landscape |
| `Cardscope` | awkward to say; "scope" reads as "range" not "view" |
| `Field` | generic, hard to search, collides with many projects |
| `Boardworks` | "works" implies a service; this is a tool |
| `Stacklane` | cute, but too narrow — "lane" is a single dimension |
| `Plank` | too short; collides with hundreds of projects on GitHub |
| `Quire` | real word (a section of a book), niche, hard to search |
| `Kanvas` | misspelling of canvas; trade-marked in some jurisdictions |

Decision: **keep "Cardscape"**. It is the name. The next time
this question is asked is the next time we rename a public
project, which should be never.

### 2.4 What we do not call the project

| Do not say | Say instead | Why |
|---|---|---|
| "Cardscape — a kanban clone" | "Cardscape — a self-hostable kanban and project-management tool" | The word "clone" implies a derivative; Cardscape is a standalone product |
| "Cardscape — a Trello alternative" | "Cardscape — a self-hostable kanban and project-management tool" | We do not name competitors in our own positioning |
| "Cardscape — the X killer" | "Cardscape — the only self-hostable kanban with first-class AI integration" | "Killer" is a tired marketing trope |
| "Card" (when we mean the data model) | "card" — lowercase, in code and in prose | "Card" capitalized collides with `System.Card` namespaces in some libraries |

---

## 3. The tagline

The tagline is the **one sentence** the project leads with. It
has to do three things in under 20 words:

1. Say what the product **is** (a kanban / project-management tool).
2. Say what makes it **different** (the MCP server; self-hostable).
3. Make the reader want the **next sentence**.

### 3.1 Candidates (evaluated)

| # | Tagline | Strength | Weakness |
|---|---|---|---|
| 1 | **"The self-hostable kanban your AI can drive."** | Differentiator first; short; active voice; AI is the hook | "your AI" sounds informal; might not scan in non-English markets |
| 2 | "Kanban, calendar, automation — with a Model Context Protocol server built in." | Lists the surface; spells out the differentiator explicitly | Long; reads like a feature list, not a hook |
| 3 | "Drive your boards conversationally. Self-host the data." | Two short sentences; emphasizes both halves of the value | Does not name the product surface; could be a calendar, a CRM, anything |
| 4 | "The only self-hostable kanban with first-class AI integration." | Strong, defensible claim | "Only" is a strong word; risky if a competitor adds MCP tomorrow |
| 5 | "Kanban + project management + MCP, self-hostable, on your stack." | Honest, specific, no superlatives | Reads like a stack declaration, not a tagline |

### 3.2 Recommended primary tagline

> **The self-hostable kanban your AI can drive.**

It is the strongest hook: the differentiator (the AI) is the
verb, the reader's data sovereignty is the promise, and the
product category is named. It is short enough to fit in a GitHub
repo description, an OG image, and a tweet.

### 3.3 Recommended supporting line (used in the README hero)

> **Drive your boards conversationally from any AI client.
> Keep full ownership of your data.**

This is the second sentence of the README hero. The first
sentence is the tagline. The third sentence is the
"what-is-it" sentence:

> Cardscape is an open-source, self-hostable project-management
> tool with a complete feature surface — kanban boards,
> calendar, automation engine, extensions, Inbox, Planner, and
> AI — and a Model Context Protocol server that lets AI
> assistants read, create, and move cards on your behalf.

### 3.4 Secondary taglines (for sub-pages and docs)

| Used in | Tagline |
|---|---|
| The MCP server section | **"The first-class AI integration for your boards."** |
| The self-hostable section | **"Your boards, your database, your hardware."** |
| The architecture section | **"Clean Architecture, .NET 11, one code path for humans and AI."** |

---

## 4. Positioning pillars

The five value props Cardscape leads with, in order. Every
piece of external writing (README, blog post, social, docs
intro) draws from this list in this order.

### Pillar 1 — Self-hostable, you own the data

> One `docker compose up` and the whole thing is on your
> hardware. No vendor can read your boards, change the rules,
> or sunset the product.

The promise: **data sovereignty**, not as a footnote, as the
core pitch. The competitive set is the SaaS kanban tools
(hosted), and the only reason to self-host is to escape them.

### Pillar 2 — AI integration that is not bolted on

> Cardscape ships a first-party MCP server as a peer to the
> REST API. The same domain model, the same authorization, the
> same idempotency. An AI client drives the boards through the
> same `Application` layer a human does through the web UI.

The promise: **the AI is a first-class user, not a wrapper**.
The competitive set is the open-source kanban tools without
MCP. Cardscape is the only one with it.

### Pillar 3 — Multi-database without lock-in

> SQLite for solo and dev, PostgreSQL or MariaDB for
> production. The provider is configuration, not code.

The promise: **no database vendor lock-in**. A team can grow
from a SQLite laptop demo to a 50 GB PostgreSQL production
deployment without a re-architecture.

### Pillar 4 — A complete feature surface

> Workspaces, boards, lists, cards, members, comments,
> checklists, attachments, calendar, automation rules,
> scheduled commands, Inbox, Planner, extensions, API tokens,
> audit logs. Designed for the long run, not a demo.

The promise: **scope, not "MVP"**. The bar is "everything a
team of 50 needs in a project-management tool", not "the
smallest thing that compiles".

### Pillar 5 — Modern .NET, end to end

> ASP.NET Core 11, Blazor WebAssembly, Entity Framework Core 10
> LTS, Radzen.Blazor. Type-safe, fast, long-term support.

The promise: **a stack that has a future**, not a stack that
already aged out. The competitive set is the open-source kanban
tools in languages with shrinking ecosystems.

### 4.1 Order matters

The pillars are listed in **priority order**:

1. Self-hostable → the **why** (the reason to leave a hosted tool).
2. AI integration → the **differentiation** (the reason to choose
   this over other self-hostable kanbans).
3. Multi-database → the **defensibility** (lowers the cost of
   adoption; no "if we outgrow SQLite" worry).
4. Complete feature surface → the **scope** (the reason to stay
   once adopted).
5. Modern .NET → the **stack** (the reason a .NET team will
   look twice).

When a paragraph has room for two pillars, lead with #1 and
#2. When it has room for one, lead with #2 (the differentiator).

---

## 5. Vocabulary guide

Words and phrases to use, and words and phrases to avoid, in
every artifact that mentions Cardscape externally.

### 5.1 Use these

| Term | Use when |
|---|---|
| **kanban and project-management tool** | describing what Cardscape is |
| **self-hostable** | the deployment story |
| **first-class AI integration** | the MCP differentiator |
| **Model Context Protocol (MCP)** | the protocol name, with the acronym after the first mention |
| **feature surface** | the set of features we ship (instead of "feature set" or "feature list") |
| **bounded context** | DDD term; the architectural unit |
| **vertical slice** | a feature end-to-end (use case → endpoint → MCP tool → UI) |
| **design for three, test on one** | the multi-DB strategy |
| **.NET 11** | the runtime, always with the version |
| **RPL-1.5** | the license, always with the version |
| **ADR** (architecture decision record) | for design decisions |
| **the differentiator** | referring to the MCP server |
| **the maintainer** | referring to the solo developer (singular; not "the team") |
| **contributions are welcome** | instead of "PRs are welcome" (more inclusive) |

### 5.2 Avoid these

| Term | Why not | Use instead |
|---|---|---|
| any competitor product name (Trello, Asana, Jira, ClickUp, Linear, etc.) | we do not name competitors in our own positioning | "hosted kanban tools", "other self-hostable kanban tools" |
| "Trello clone", "Trello alternative", "Trello killer" | positions Cardscape as derivative | drop the comparison; state the value directly |
| "MVP", "demo", "just a prototype" | the user explicitly said no demo MVP | "the smallest shippable cut", "the first release" |
| "Butler" | vendor-specific brand name for the automation feature | "Automation" |
| "Power-Up(s)" | vendor-specific brand name for extensions | "Extension(s)" |
| "Trello-style board" | vendor-specific visual reference | "kanban board" |
| "AI assistant" (alone) | too generic; what model? what client? | "AI client" (Claude Desktop, Cursor, etc.) or "MCP-compatible client" |
| "the team" | the project is solo-maintained | "the maintainer" or "the project" |
| "free" (as a feature) | the project is open-source, not free-as-in-free-pizza; the value is the license, not the price | "open-source" |
| "self-hosted" (hyphenated) | inconsistent | "self-hostable" (capability) or "self-hosted" (state, only when describing a specific deployment) |
| "killer feature" | tired marketing trope | "differentiator" |
| "next-generation" | buzzword | drop it |
| "revolutionary" | buzzword | drop it |

### 5.3 Capitalization

- **Cardscape** — always capitalized, never all-caps, never
  stylized (no "CardScape" or "cardScape").
- **MCP** — all-caps, with the first use spelled out as
  "Model Context Protocol (MCP)".
- **.NET** — with the leading dot, always.
- **RPL-1.5** — all-caps, hyphen, version.
- **ADR** — all-caps when used as a term, expanded on first
  use as "architecture decision record (ADR)".
- **Blazor**, **WebAssembly**, **Entity Framework Core**,
  **Radzen** — proper-noun capitalization from the product
  owners; do not lowercase.
- **PostgreSQL**, **MariaDB**, **SQLite** — proper-noun
  capitalization from the database owners.

---

## 6. Voice and tone

Cardscape's voice in writing is:

- **Direct.** No hedging, no "we believe", no "we hope". State
  what is true and what is built.
- **Specific.** Numbers, versions, file paths, commit hashes.
  "11/11 projects, 0 errors, 0 warnings, 6.1 s" beats
  "builds fast".
- **Confident without being arrogant.** The differentiator is
  a fact (we are the only self-hostable kanban with first-class
  MCP); state it as a fact, not a boast.
- **Calm.** No exclamation points. No marketing hype. The
  project is built to last; the writing should be too.
- **First person plural, sparingly.** "We" for the project
  ("we ship", "we test"), but the maintainer is one person
  and that is honest.

What that means in practice:

- ❌ "We're so excited to announce…" → ✅ "Cardscape ships…"
- ❌ "A powerful, flexible, and beautiful way to…" → ✅ "An
  open-source, self-hostable project-management tool."
- ❌ "Game-changing" → ✅ "first-party"
- ❌ "Easy to use" → ✅ concrete claim (e.g. "single
  `docker compose up`")
- ❌ "We hope you enjoy" → ✅ "the working contract is in
  `docs/AGENTS.md`"

---

## 7. Where this positioning shows up

| Artifact | Draws from |
|---|---|
| Root `README.md` hero | §3.2 (tagline), §3.3 (supporting line), §4 (pillars) |
| Root `README.md` "Why Cardscape" | §4 (pillars, in order) |
| Root `README.md` MCP section | §3.4 (secondary taglines), §4.2 (pillar 2) |
| Root `README.md` "Status" | §1 (the honest scope) |
| `docs/README.md` intro | §4.1 (pillar order) |
| `docs/AGENTS.md` "What Cardscape is" | §4.1 (pillar 1, then 2) |
| `docs/adr/0002-mcp-server.md` "Consequences" | §4.2 (pillar 2 framing) |
| Future blog post on MCP | §3.2 (tagline), §4.2 (pillar 2) |
| Future public website | §3.2, §3.3, §4 (all) |

If a future artifact contradicts this document, this document
wins. Update the artifact, not the other way around.

---

## 8. When to revisit

This document should be revisited when **any** of the
following is true:

1. The name "Cardscape" is taken by another project of similar
   scope (a public kanban or project-management tool with the
   same name in the same market).
2. A second self-hostable kanban ships a first-class MCP
   server, in which case "the only self-hostable kanban with
   MCP" stops being defensible and the tagline needs to change.
3. The MCP protocol itself is deprecated or replaced by a
   successor; the differentiator needs a re-positioning.
4. The maintainer grows the project past solo work and the
   vocabulary around "the maintainer" / "the team" needs to
   change.

Until then, this document is the source of truth for how
Cardscape presents itself. Changes are append-only with a
"Revised YYYY-MM-DD" header.
