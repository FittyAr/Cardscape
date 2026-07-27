# Maintainers

> Who is responsible for what in Cardscape. The project is
> solo-maintained today. This file documents the current state
> and the path to a multi-maintainer future.

---

## Current state

Cardscape is maintained by **a single person** — the project's
author. The maintainer works on this in their available time
and is the only person with merge rights on the `master` and
`site` branches.

The maintainer's responsibilities are everything, by default:
code review, release management, issue triage, community
moderation, security response, and roadmap review.

---

## Maintainer role (the contract)

A Cardscape maintainer is expected to:

1. **Review pull requests** within 7 days of the request.
2. **Triage issues** within 14 days (bug → priority, feature →
   discussion, question → answer or redirect to Discussions).
3. **Cut releases** per the
   [release process](docs/development/04-release-process.md).
4. **Respond to security reports** within 3 business days, per
   [SECURITY.md](SECURITY.md).
5. **Moderate the community** per
   [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
6. **Keep the docs current** — every merged change that affects
   public behavior updates the relevant doc in the same PR.
7. **Participate in roadmap review** at the end of every phase.
8. **Be a public face** — Discussions, Announcements, and
   (when relevant) blog posts.

There is no on-call rotation today. There is no SLA today.
This is a volunteer project.

---

## Areas of responsibility

Areas are listed with the current single owner and the path to
splitting them when more maintainers join.

| Area | Today | Future split |
|---|---|---|
| Domain + Application + Infrastructure (backend) | maintainer | `domain-maintainer` + `infra-maintainer` |
| `Cardscape.Api` (REST API) | maintainer | `api-maintainer` |
| `Cardscape.Web` (Blazor WASM client) | maintainer | `web-maintainer` |
| `Cardscape.Mcp` (MCP server) | maintainer | `mcp-maintainer` |
| EF Core migrations + multi-DB plumbing | maintainer | `db-maintainer` |
| Documentation (`docs/`, `README.md`, `site/`) | maintainer | `docs-maintainer` |
| Community (Discussions, CoC, code of conduct) | maintainer | `community-maintainer` |
| Security (vulnerability response, threat model) | maintainer | `security-maintainer` |
| Release process + NuGet + Docker | maintainer | `release-maintainer` |
| Brand + visual identity | maintainer | `design-maintainer` |

The split is aspirational and tracks the
[bounded contexts in the architecture](docs/architecture/01-bounded-contexts.md).
A maintainer can hold multiple areas.

---

## Becoming a maintainer

The project does not have a formal "application" process. The
path is:

1. **Make consistent, high-quality contributions** over time.
   The maintainer watches who shows up and how they work.
2. **Take on an area** — start as the de-facto owner of one
   bounded context or one doc area. The maintainer pulls you
   into the review queue for that area.
3. **Get invited to the maintainers team** on GitHub. From
   here on, you have merge rights on your area and are listed
   in this file.
4. **Take on more areas** as your time and context allow.

The maintainer is the only person who can grant maintainer
status. The grant is a GitHub team invitation, an entry in
this file, and (if applicable) a CODEOWNERS update.

There is no voting. The maintainer decides. The reason is
simple: when there is one person accountable for a project,
that person decides who else is accountable. When the project
outgrows one person, this section gets a real process — see
[GOVERNANCE.md](GOVERNANCE.md) for the trigger.

---

## Stepping down

A maintainer who needs to step down:

1. Tells the other maintainers (today: the maintainer) at
   least 30 days in advance if possible.
2. Hands off their areas to whoever picks them up.
3. Is moved to the **Emeritus** section below with their
   approval.

Emeritus maintainers keep their commit history and the
"thank you" in `CONTRIBUTORS.md`, but no longer have merge
rights or on-call expectations.

---

## Emeritus

_None yet._

---

## See also

- [GOVERNANCE.md](GOVERNANCE.md) — how decisions are made
  (today: solo; future: lazy consensus + voting).
- [CONTRIBUTING.md](CONTRIBUTING.md) — the contribution flow.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) — community
  standards.
- [`.github/CODEOWNERS`](.github/CODEOWNERS) — automatic
  reviewer assignment by path.
- [docs/architecture/01-bounded-contexts.md](docs/architecture/01-bounded-contexts.md)
  — the architectural units that map to the areas above.
