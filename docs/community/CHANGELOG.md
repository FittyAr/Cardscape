# Changelog

All notable changes to Cardscape are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

The project is in pre-alpha. The first tagged release is
targeted at the end of **Phase 1 — MVP** (target date: end of
August 2026).

### Added

- Solution scaffold: 6 source projects, 5 test projects, .NET 11
  preview 6 SDK, EF Core 10.0.10 LTS.
- Multi-provider persistence scaffolding (SQLite, PostgreSQL,
  MariaDB). SQLite-only test matrix for now. See
  [ADR 0001](docs/adr/0001-multi-provider-strategy.md).
- RPL-1.5 LICENSE.
- Project-local `.agents/` folder with the working contract and
  5 skills.
- `Cardscape.Mcp` project skeleton: stdio transport,
  `ApiTokenAuthenticationHandler` placeholder, `ICurrentUser`
  resolver. No tools yet. See
  [ADR 0002](docs/adr/0002-mcp-server.md).
- `docs/` set: working contract, ADRs, architecture, development
  conventions, API conventions, feature inventory, implementation
  plan, product positioning.
- Root `README.md` (the public pitch) and
  [`docs/roadmap/02-product-positioning.md`](docs/roadmap/02-product-positioning.md)
  (name, tagline, pillars, vocabulary, voice).
- Community files: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`,
  `SECURITY.md`, `SUPPORT.md`, this `CHANGELOG.md`.
- GitHub issue templates (bug, feature, question) and pull
  request template.
- GitHub Discussion categories (announcements, ideas, Q&A, show
  and tell).
- Public website on the `site` branch: single-page HTML + CSS,
  no build step. Deployable to GitHub Pages, Netlify, Cloudflare
  Pages, or Vercel.

### Changed

- Repository rebranded to vendor-neutral product positioning.
  Working tree and git history rewritten to drop every
  reference to the legacy competitor and its brand names.
  See commit `289a370`.

### Removed

- `docs/roadmap/00-trello-features-analysis.md` (renamed to
  `docs/roadmap/00-feature-inventory.md` and rewritten as a
  Cardscape-voice feature inventory).
- All references to vendor-specific brand names (Butler,
  Power-Ups, Atlassian Intelligence) in code, docs, and
  commit history.

### Security

- None yet. The first security audit ships with Phase 5 (Polish
  & scale).

---

## Versioning policy

Cardscape uses Semantic Versioning with pre-1.0 caveats:

- **0.y.z** — pre-1.0, "moving fast". Minor bumps can include
  breaking changes. The project's API and schema are not
  stable until 1.0.
- **1.0.0** — first stable release. API and schema are stable
  from here. Breaking changes bump the major version.

Until 1.0, the minor version denotes a phase completion (see
[`docs/roadmap/01-implementation-plan.md`](docs/roadmap/01-implementation-plan.md)).

| Version | Phase | Status |
|---|---|---|
| `v0.0.0` | (unreleased scaffold) | not used |
| `v0.1.0-mvp` | Phase 1 — MVP | target end of August 2026 |
| `v0.2.0-core-mcp` | Phase 2 — Core + MCP server | target end of October 2026 |
| `v0.3.0-extensions` | Phase 3 — Extensions & automation | target end of December 2026 |
| `v0.4.0-enterprise` | Phase 4 — Enterprise & AI | target end of Q1 2027 |
| `v1.0.0` | (Phase 5 — Polish & scale mature) | not yet targeted |

The release process (tags, NuGet, Docker, notes) is in
[`docs/development/04-release-process.md`](docs/development/04-release-process.md).

---

## Types of changes

- **Added** for new features.
- **Changed** for changes in existing functionality.
- **Deprecated** for soon-to-be-removed features.
- **Removed** for now-removed features.
- **Fixed** for any bug fixes.
- **Security** for vulnerability fixes and security-policy
  changes.
