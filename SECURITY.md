# Security policy

Thank you for helping keep Cardscape and its users safe. This
document explains how to report a security vulnerability, what
to expect, and what we will (and will not) do.

---

## Supported versions

Cardscape is in **pre-alpha**. There is no production release
yet. The only version that receives security updates is:

| Version | Supported |
|---|---|
| `master` branch (the current development line) | ✅ yes |
| Tagged releases (none yet) | ❌ no — too early |

When the first tagged release ships, the supported-versions
table will be updated to include the latest two minor versions
and the latest patch release of older minors (the standard
backport policy for a small project).

---

## How to report a vulnerability

**Please do not file a public GitHub issue for security
vulnerabilities.** A public issue tells the world about the
bug before a fix is available.

Report privately by email to:

**security@fitty.ar** (this address is forwarded to the
maintainer's personal inbox and masks it from the public)

Include:

1. A clear description of the vulnerability.
2. Steps to reproduce, or a proof-of-concept.
3. The affected version (commit SHA, branch, or release tag).
4. Your assessment of the impact (low / medium / high /
   critical).
5. Whether you want public credit in the security advisory.

You can also use GitHub's private vulnerability reporting:
**Repository → Security → Advisories → "New draft security
advisory"**. This routes through GitHub's private channel and
keeps the report out of the public issue tracker.

---

## What to expect

| Step | When |
|---|---|
| Acknowledgement | within 3 business days |
| Initial triage | within 7 business days |
| Fix timeline | depends on severity: critical = 24-48 h, high = 7 days, medium = 30 days, low = next release |
| Public disclosure | after the fix ships, or 90 days after the report, whichever comes first |

For pre-alpha software, the response time may be longer than
the targets above. The maintainer is solo and works on this in
their available time. If you need a faster response, opening a
public issue with `[SECURITY-AWARE]` in the title is acceptable
for low-severity issues (e.g. documentation that exposes an
internal path).

---

## What we will do

- Acknowledge the report within 3 business days.
- Investigate and triage the report.
- Develop a fix on a private branch.
- Coordinate disclosure with the reporter.
- Credit the reporter in the security advisory (unless asked
  not to).
- Add a `SECURITY.md` entry for the fix in the changelog.

## What we will not do

- We will not sue you. This project follows
  [coordinated disclosure](https://en.wikipedia.org/wiki/Coordinated_vulnerability_disclosure);
  good-faith security research is welcome.
- We will not pay bounties. There is no bug-bounty program today
  and the project is volunteer-maintained.
- We will not ship a fix without a public advisory (unless
  the issue is so minor that disclosure adds no value).
- We will not blame the reporter for a real bug.

---

## Scope

**In scope:**

- The Cardscape source code under `src/` and `tests/`.
- The Cardscape.Mcp server.
- The Cardscape web client.
- The Cardscape REST API.
- The documentation that ships operational instructions
  (e.g. deployment guides with credentials).

**Out of scope:**

- Third-party NuGet packages. Report those upstream.
- Hypothetical vulnerabilities without a working proof of
  concept.
- Reports against infrastructure we do not control (DNS,
  GitHub itself, etc.).
- Denial-of-service against a self-hosted instance the
  reporter does not own.

---

## Security design notes

Cardscape is designed with the following baseline:

- All authentication is via the `Members` context (ASP.NET
  Identity for humans, API tokens for the MCP server and the
  REST API). No "remember me" tokens, no custom auth schemes
  without an ADR.
- All authorization rules go through a single authorization
  pipeline (policy-based, central).
- The MCP server uses the same authentication and authorization
  as the REST API. The MCP transport does not weaken the
  security model.
- Personal access tokens are hashed at rest (PBKDF2 or
  Argon2id, decided per phase). Never stored in plaintext.
- All write operations support an `Idempotency-Key` header
  / parameter, so retries from AI agents cannot double-write.
- Dependencies are pinned via Central Package Management
  (`Directory.Packages.props`).
- The multi-DB strategy is reviewed per provider for
  provider-specific security pitfalls (e.g. PostgreSQL
  RLS, MariaDB grant model).

## Full security posture

The full security posture lives in
[`docs/security/`](docs/security/):

| Topic | Document |
|---|---|
| Threat model (STRIDE) | [`docs/security/01-threat-model.md`](docs/security/01-threat-model.md) |
| Secure-coding checklist | [`docs/security/02-secure-coding-checklist.md`](docs/security/02-secure-coding-checklist.md) |
| GDPR compliance (deployer-side) | [`docs/security/03-gdpr-compliance.md`](docs/security/03-gdpr-compliance.md) |
| SOC 2 readiness (deployer-side) | [`docs/security/04-soc2-readiness.md`](docs/security/04-soc2-readiness.md) |
| Coordinated vulnerability disclosure | [`docs/security/05-vulnerability-disclosure.md`](docs/security/05-vulnerability-disclosure.md) |
| Hall of fame | [`docs/security/HALL_OF_FAME.md`](docs/security/HALL_OF_FAME.md) |
| Privacy notice template (deployer fills in) | [`docs/security/templates/privacy-notice.md`](docs/security/templates/privacy-notice.md) |
| Breach notification template | [`docs/security/templates/breach-notification.md`](docs/security/templates/breach-notification.md) |
| DPIA template | [`docs/security/templates/dpia.md`](docs/security/templates/dpia.md) |
| DSAR response template | [`docs/security/templates/dsar-response.md`](docs/security/templates/dsar-response.md) |

The `SECURITY.md` file in this root is the
short version that GitHub surfaces on the
repository's Security tab. The full posture
is in the `docs/security/` folder.
