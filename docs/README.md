# Cardscape — Documentation

This is the documentation index for **Cardscape**, an open-source
kanban and project-management tool built on .NET 10 (LTS) with a
full feature surface (kanban + calendar + automation + extensions +
Inbox + AI) and a **Model Context Protocol (MCP) server** that
lets any AI-compatible client drive the boards conversationally.

Every file here is meant to be read in order the first time you
join the project, and then referenced individually as needed.

## 1. Read me first (onboarding path)

If you are a new contributor (human or AI agent), follow this order:

1. [`AGENTS.md`](AGENTS.md) — the **contract** that any agent
   (human or AI) working on the repo must follow. Covers
   stack, design philosophy, working rules, the MCP pillar,
   the SQLite-only test matrix, and the available skills.
2. [`roadmap/02-product-positioning.md`](roadmap/02-product-positioning.md)
   — the project's name, tagline, positioning pillars,
   vocabulary guide, and voice. **Read this before writing
   about the project** in any doc, blog post, or commit
   message.
3. [`roadmap/00-feature-inventory.md`](roadmap/00-feature-inventory.md)
   — the feature inventory Cardscape is building toward.
   Tells you what's in scope.
4. [`roadmap/01-implementation-plan.md`](roadmap/01-implementation-plan.md)
   — the phased delivery plan. Tells you what's next and
   what's deferred. The MCP server ships in **Phase 2**.
   The current workstream is **v1.2.0** (see
   [`05-plan-v1.2.0.md`](roadmap/05-plan-v1.2.0.md)).
5. [`architecture/00-overview.md`](architecture/00-overview.md) —
   the Clean Architecture layers, the directory layout (now
   including `Cardscape.Mcp/`), and the dependency rules.
6. [`architecture/03-mcp-server.md`](architecture/03-mcp-server.md)
   — the MCP server operational guide. Read this before
   any MCP work.
7. [`development/00-onboarding.md`](development/00-onboarding.md) —
   get the solution building on your machine in 10 minutes.
8. [`development/01-conventions.md`](development/01-conventions.md) —
   the C# style, naming, async, and EF Core rules we
   enforce.
9. [`development/02-vertical-slices.md`](development/02-vertical-slices.md)
   — recipe for adding a new feature (use case → endpoint
   → MCP tool → UI).
10. [`development/03-testing-strategy.md`](development/03-testing-strategy.md)
    — why the test matrix is SQLite-only today and how it
    grows.
11. [`api/00-conventions.md`](api/00-conventions.md) — REST
    conventions for the public API.

## 2. Architecture Decision Records (ADRs)

ADRs live in [`adr/`](adr/) and are **append-only**. Never delete
one; mark it as `Superseded by ADR NNNN` instead.

| ADR | Title | Status |
|---|---|---|
| [0001](adr/0001-multi-provider-strategy.md) | Multi-provider persistence (SQLite, PostgreSQL, MariaDB) with SQLite-only test matrix | Accepted (2026-07-27) |
| [0002](adr/0002-mcp-server.md) | Model Context Protocol (MCP) server | Accepted (2026-07-27) |
| [0009](adr/0009-radzen-only-ui.md) | Radzen-only UI: kill HTML/JS/CSS custom in `Cardscape.Web` | Accepted (2026-08-03) |
| [0010](adr/0010-client-side-culture-switcher.md) | Client-side culture switcher (Blazor WebAssembly) | Accepted (2026-08-04) |

## 3. How the docs are organized

```
docs/
├── README.md                          # you are here
├── AGENTS.md                          # contract for agents (mirror of .agents/AGENTS.md)
├── adr/                               # Architecture Decision Records
├── ai/                                # MCP server + AI features
│   ├── 01-mcp-deep-dive.md            # the "how to add a tool" recipe
│   ├── 02-prompt-library.md            # canonical MCP prompts
│   └── 03-ai-ethics.md                # what we build, what we don't
├── api/                               # public API conventions
├── architecture/                      # how the solution is shaped
│   ├── 00-overview.md                 # the layers and the directory layout
│   ├── 01-bounded-contexts.md         # vertical slices
│   ├── 02-multi-provider-persistence.md  # companion to ADR 0001
│   └── 03-mcp-server.md               # companion to ADR 0002
├── blog/                              # public-facing blog posts
│   └── 01-cardscape-rebrand.md        # the rebrand announcement
├── brand/                             # visual identity (palette, typography, logo)
│   └── 00-brand-kit.md
├── design/                            # patterns the implementation will follow
│   ├── 01-error-handling.md           # Result<T>, ProblemDetails, error codes
│   ├── 02-logging-observability.md    # Serilog + OTel, correlation IDs
│   ├── 03-auth-and-authz.md           # the auth/authz model in detail
│   ├── 04-accessibility.md            # WCAG 2.1 AA target
│   ├── 05-performance-budgets.md      # quantified performance targets
│   └── 06-feature-flags.md            # the flag lifecycle, the "no flag left behind" rule
├── development/                       # how to set up and work on the solution
│   ├── 00-onboarding.md              # 10-minute local setup
│   ├── 01-conventions.md             # C# style, async, EF Core rules
│   ├── 02-vertical-slices.md         # recipe for adding a feature
│   ├── 03-testing-strategy.md        # SQLite-only test matrix
│   └── 04-release-process.md         # versioning, tags, NuGet, Docker
├── i18n/                              # internationalization (EN + ES today)
│   ├── 01-policy.md                  # what gets translated, who translates
│   └── 02-translation-workflow.md     # the file layout, the PR process
├── operations/                        # runbooks for self-hosting
│   ├── 01-deployment.md               # the Docker Compose setup
│   ├── 02-backup-restore.md          # the backup and restore procedure
│   ├── 03-monitoring.md              # the OTel pipeline, the dashboards, the alerts
│   └── 04-incident-response.md       # the on-call playbook
├── positioning/                      # marketing and external positioning
│   └── 01-comparison.md              # the vendor-neutral feature comparison
├── roadmap/                           # where we're going (and how we present ourselves)
│   ├── 00-feature-inventory.md        # the target feature surface
│   ├── 01-implementation-plan.md     # the phased delivery plan
│   ├── 02-product-positioning.md     # name, tagline, pillars, vocabulary, voice
│   ├── 03-execution-plan-v1.1.0.md   # the closed v1.1.0 workstream (42 features + 14 audit gaps)
│   ├── 04-audit-gaps-2026-07-30.md  # the v1.1.0 per-area audit report
│   └── 05-plan-v1.2.0.md             # the current workstream (doc reconciliation + next chunk)
├── community/                         # community-facing reference docs
│   ├── CHANGELOG.md                   # Keep a Changelog format
│   ├── ROADMAP.md                     # community-readable version of the implementation plan
│   ├── GOVERNANCE.md                  # decision-making model + path to multi-maintainer
│   ├── MAINTAINERS.md                 # areas of responsibility
│   ├── CONTRIBUTORS.md                # who contributed what (auto-generated)
│   └── LAUNCH.md                      # internal marketing runbook
└── security/                          # threat model + secure-coding checklist
    ├── 01-threat-model.md             # STRIDE per bounded context
    └── 02-secure-coding-checklist.md  # the reviewer checklist
```

## 4. Mirror folders

Some folders are duplicated between the repository root and
`docs/`:

| Repo path | Docs path | Why |
|---|---|---|
| `.agents/AGENTS.md` | `docs/AGENTS.md` | `.agents/` is the contract for **AI agents**; `docs/` is the contract for **humans** |
| `.agents/skills/` | (pointers in `docs/AGENTS.md`) | Skills stay in `.agents/` because tools load them by path |
| `docs/adr/0001-…`, `0002-…` | (canonical) | ADRs live only in `docs/adr/` |
| `site/` (on the `site` branch) | (canonical) | The public website lives on its own orphan branch |

The two locations are kept in sync manually. The
`docs/AGENTS.md` is the "human-friendly" rendering;
`.agents/AGENTS.md` is the operational contract an agent reads
at runtime.

## 5. Reference docs by role

Different roles read different docs. Use this table to find
the right starting point.

| If you are a… | Start with |
|---|---|
| New contributor (human or AI agent) | [`AGENTS.md`](AGENTS.md) + the onboarding path in §1 above |
| Writer (docs, blog, social) | [`roadmap/02-product-positioning.md`](roadmap/02-product-positioning.md) — name, tagline, pillars, vocabulary, voice |
| Designer (UI, marketing, social) | [`brand/00-brand-kit.md`](brand/00-brand-kit.md) — palette, typography, logo concept |
| Release manager | [`development/04-release-process.md`](development/04-release-process.md) — versioning, tags, NuGet, Docker |
| Self-hoster (deploying Cardscape) | [`operations/01-deployment.md`](operations/01-deployment.md) — the Docker Compose setup, then [`operations/02-backup-restore.md`](operations/02-backup-restore.md) for backups |
| Maintainer doing the phased plan review | [`roadmap/01-implementation-plan.md`](roadmap/01-implementation-plan.md) — the canonical plan |
| Implementer working on the MCP server | [`ai/01-mcp-deep-dive.md`](ai/01-mcp-deep-dive.md) — the "how to add a tool" recipe |
| Implementer working on errors, logging, auth, accessibility, performance, or feature flags | the matching file in [`design/`](design/) — the pattern the implementation will follow |
| Security reviewer | [`security/01-threat-model.md`](security/01-threat-model.md) — STRIDE per bounded context, then [`security/02-secure-coding-checklist.md`](security/02-secure-coding-checklist.md) — the reviewer checklist |
| Translator | [`i18n/01-policy.md`](i18n/01-policy.md) — what gets translated, then [`i18n/02-translation-workflow.md`](i18n/02-translation-workflow.md) — the file layout and the PR process |
| Prospective user evaluating Cardscape | [`positioning/01-comparison.md`](positioning/01-comparison.md) — the vendor-neutral feature comparison |
| On-call responder | [`operations/04-incident-response.md`](operations/04-incident-response.md) — the playbook |

## 6. Conventions for contributing to these docs

- **Markdown only.** No Word, no PDF, no Notion exports.
- **One H1 per file** (the file's title). The index in
  `README.md` is the only place with multiple H1s.
- **Relative links** between docs. Never hardcode absolute paths.
- **Code samples** in fenced blocks with a language tag.
- **Diagrams** go in `architecture/diagrams/` and are embedded
  with relative image links. We prefer Mermaid in `.md` files
  for any new architecture diagram — it renders on GitHub,
  GitLab, and most Markdown viewers.
- **New bounded contexts** go in the catalog table and the
  Mermaid diagram of [`architecture/01-bounded-contexts.md`](architecture/01-bounded-contexts.md).
- **New architectural decisions** go in a new ADR under
  [`adr/`](adr/). Never delete or edit an existing ADR;
  supersede it with a new one.

## 7. License

All documentation in this folder is licensed under the
[Reciprocal Public License 1.5 (RPL-1.5)](../LICENSE), the same
as the code. Improvements are welcome; redistributions must
keep the same license (reciprocity clause).
