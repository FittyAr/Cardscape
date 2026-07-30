# Cardscape audit — 2026-07-30

> This directory holds the per-area audit reports produced on
> 2026-07-30. The audit verifies, slice by slice, that the
> features described in
> [`../../roadmap/03-execution-plan-v1.1.0.md`](../../roadmap/03-execution-plan-v1.1.0.md)
> are actually present in the codebase on `master` at
> commit `02f9486`.
>
> Each sub-agent owns one file. The verdict for every item
> is one of:
>
> - **DONE** — code is on `master`, evidence in this report.
> - **PARTIAL** — some pieces present, list what's missing.
> - **MISSING** — no code, no test, no doc.
> - **DRIFT** — implemented but in a way that diverges from
>   the plan; record the actual shape.
>
> After every sub-agent reports, a master
> `04-audit-gaps-2026-07-30.md` is generated at the
> `docs/roadmap/` level, with a single prioritized list of
> gaps to close.

## Reports

| # | File | Scope | Plan reference |
|---|---|---|---|
| 01 | `01-hygiene.md` | CI, test projects, plan status, ADRs | §1 |
| 02 | `02-mcp.md` | MCP server completeness | §2 |
| 03 | `03-cards-and-views.md` | Card Aging, Snooze, Mirror, List Limits, Dashboards, iCal | §3.1–3.6 |
| 04 | `04-integrations.md` | Slack, Google Drive, GitHub, Email-to-board | §3.7–3.10 |
| 05 | `05-oauth-and-enterprise.md` | OAuth apps, OpenAPI, OAuth login, SAML, 2FA, SCIM, data residency | §3.11–3.12, §4.1–4.5 |
| 06 | `06-ai.md` | Google Calendar sync, IAiService, AI providers, AI features, MCP AI tools | §4.6–4.10 |
| 07 | `07-polish.md` | i18n, PWA, SDK, status page, import, export, MCP subs | §5.1–5.8 |

## Method

- Each agent reads the relevant section of the v1.1.0 plan.
- Each agent greps the codebase for the named files, classes,
  endpoints, MCP tools, and tests.
- Each agent records the verdict with file paths and line
  numbers as evidence.
- Each agent appends to its own report file; no agent edits
  another agent's file.
