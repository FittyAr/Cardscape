# Cardscape — Documentation

This is the documentation index for **Cardscape**, an open-source
kanban and project-management tool built on .NET 11 with a full
feature surface (kanban + calendar + automation + extensions +
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

## 3. How the docs are organized

```
docs/
├── README.md                          # you are here
├── AGENTS.md                          # contract for agents (mirror of .agents/AGENTS.md)
├── adr/                               # Architecture Decision Records
├── architecture/                      # how the solution is shaped
│   ├── 00-overview.md                 # the layers and the directory layout
│   ├── 01-bounded-contexts.md         # vertical slices
│   ├── 02-multi-provider-persistence.md  # companion to ADR 0001
│   └── 03-mcp-server.md               # companion to ADR 0002
├── development/                       # how to set up and work on the solution
├── api/                               # public API conventions
└── roadmap/                           # where we're going (and how we present ourselves)
    ├── 00-feature-inventory.md        # the target feature surface
    ├── 01-implementation-plan.md     # the phased delivery plan
    └── 02-product-positioning.md     # name, tagline, pillars, vocabulary, voice
```

## 4. Mirror folders

Some folders are duplicated between the repository root and
`docs/`:

| Repo path | Docs path | Why |
|---|---|---|
| `.agents/AGENTS.md` | `docs/AGENTS.md` | `.agents/` is the contract for **AI agents**; `docs/` is the contract for **humans** |
| `.agents/skills/` | (pointers in `docs/AGENTS.md`) | Skills stay in `.agents/` because tools load them by path |
| `docs/adr/0001-…`, `0002-…` | (canonical) | ADRs live only in `docs/adr/` |

The two locations are kept in sync manually. The
`docs/AGENTS.md` is the "human-friendly" rendering;
`.agents/AGENTS.md` is the operational contract an agent reads
at runtime.

## 5. Conventions for contributing to these docs

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

## 6. License

All documentation in this folder is licensed under the
[Reciprocal Public License 1.5 (RPL-1.5)](../LICENSE), the same
as the code. Improvements are welcome; redistributions must
keep the same license (reciprocity clause).
