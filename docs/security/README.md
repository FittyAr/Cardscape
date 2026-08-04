# Security

> The project's security posture. The
> documents in this folder are the
> project-side contribution to the
> security audit the deployer performs
> on their own deployment (SOC 2,
> ISO 27001, internal audit, etc.).
> The deployer is responsible for the
> deployment-side controls (hosting,
> backups, physical access, capacity).

## Contents

- [`01-threat-model.md`](01-threat-model.md) —
  the STRIDE threat model, per
  bounded context. The input to
  every security review of a change
  in the affected context.
- [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md) —
  the rules every contributor
  follows. The architecture tests
  pin a subset of the rules
  automatically.
- [`03-gdpr-compliance.md`](03-gdpr-compliance.md) —
  the GDPR posture. Covers the
  lawful basis, the data inventory
  (ROPA), the data subject rights,
  the breach-notification process,
  and the privacy-by-design
  decisions baked into the code.
- [`04-soc2-readiness.md`](04-soc2-readiness.md) —
  the SOC 2 Type II readiness
  posture, mapped to the Trust
  Services Criteria. The project
  ships the project-side controls;
  the deployer hires the CPA firm.
- [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md) —
  the coordinated vulnerability
  disclosure policy. How to report
  a security issue; the 90-day
  disclosure window; the safe
  harbour.
- [`HALL_OF_FAME.md`](HALL_OF_FAME.md) —
  the public list of reporters who
  submitted a confirmed
  vulnerability.
- [`templates/`](templates/) —
  the privacy notice, the breach
  notification, the DPIA, and the
  data subject access request
  templates. The deployer fills in
  their organisation details and
  ships.

## Mental model

Cardscape is a **self-hosted B2B-style
kanban tool**. The deploying
organisation is the **data
controller**; Cardscape is the **data
processor** for the user account, the
workspace / board / list / card
content, the activity feed, the audit
log, and the third-party integration
tokens. The maintainer (the Cardscape
project) ships the software; the
maintainer does not hold any data and
is not a controller or a processor for
the deployer's data.

The security posture has three
audiences:

1. **The contributor** — reads
   `02-secure-coding-checklist.md` and
   the threat model section for the
   context they are touching. Writes
   code that follows the rules. The
   architecture tests catch the
   automatic subset of the rules.
2. **The deployer** — reads this
   README, the GDPR doc, and the SOC 2
   doc to understand the project-side
   controls. Combines them with their
   own deployment-side controls to
   build their own audit package.
3. **The external auditor** —
   (optionally) reads the same
   documents to scope the audit. The
   project-side controls are a
   starting point; the deployer is the
   one being audited.

## Quick links

- **Found a security issue?**
  [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md)
- **Need a privacy notice?**
  [`templates/privacy-notice.md`](templates/privacy-notice.md)
- **Need to notify a supervisory
  authority of a breach?**
  [`templates/breach-notification.md`](templates/breach-notification.md)
- **Need a DPIA for a high-risk
  deployment?**
  [`templates/dpia.md`](templates/dpia.md)
- **Need a data subject access
  request response?**
  [`templates/dsar-response.md`](templates/dsar-response.md)

## Scope: what the project owns vs. what the deployer owns

| Surface | Project owns | Deployer owns |
|---|---|---|
| **The source code** | yes | n/a |
| **The default configuration** | yes (secure by default) | can override with their own configuration |
| **The architecture tests** (pin the secure-by-default rules) | yes | can add their own architecture tests for the deployment |
| **The audit log** (the database table; the API to read it) | yes | owns the database; the retention; the SIEM rules |
| **The rate limit** (the per-API-token 100 req/min default) | yes | can override the limit via `Cardscape:RateLimit:RequestsPerMinute` |
| **The security headers** (CSP, HSTS, etc.) | yes (the middleware) | owns the TLS termination; the HSTS preload submission |
| **The encryption keys** (the data-protection key ring for the OAuth integration tokens) | n/a (no key material) | yes (the deployer's key ring; the deployer can use their own KMS) |
| **The hosting** (the operating system; the reverse proxy; the backups; the monitoring) | n/a | yes |
| **The physical access** (the data centre) | n/a | yes (via the hosting provider's SOC 2) |
| **The breach notification** (the 72-hour clock) | n/a (project does not hold data) | yes (the deployer is the controller) |
| **The privacy notice** (the public-facing text) | ships a template | yes (the deployer fills in their organisation details) |

## Status

The project is **pre-alpha**. The
security posture is the project's
contribution to a future audit, not
a current certification. The deployer
ships their own certification.

| TSC | Project coverage | Audit target | Notes |
|---|---|---|---|
| **Security (CC)** | ✅ controls shipped | SOC 2 Type II in v3.0+ | the project ships the artefacts; the deployer hires the CPA firm |
| **Availability (A)** | ✅ primitives shipped | optional add-on | the deployer certifies against their own SLO |
| **Processing Integrity (PI)** | ✅ controls shipped | optional add-on | relevant for the third-party integration surface |
| **Confidentiality (C)** | ✅ controls shipped | optional add-on | relevant for financial-services or healthcare deployers |
| **Privacy (P)** | ✅ GDPR posture shipped | optional add-on | the GDPR doc is the input |

The maintainer's roadmap is to ship
**SOC 2 Type II certification** for
the maintainer's own hosted service
(if/when the maintainer runs one) in
v3.0+. The self-hosted case is
always the deployer's audit.

## Contributing to security

Security fixes follow the same PR
flow as any other change, with two
additions:

1. The PR description includes a
   `## Security` section that
   describes the threat the change
   mitigates, the STRIDE category
   (from `01-threat-model.md`), and
   the severity (from
   `05-vulnerability-disclosure.md`).
2. The PR is backported to every
   supported release branch (see
   `05-vulnerability-disclosure.md` §6
   for the supported-version policy).

The maintainer reviews security PRs
within 1 business day. Critical-severity
PRs are reviewed within 4 hours during
business hours, or 24 hours otherwise.

## References

- [`../adr/0007-privacy-by-default.md`](../adr/0007-privacy-by-default.md) —
  the privacy-by-design ADR.
- [`../operations/04-incident-response.md`](../operations/04-incident-response.md) —
  the incident-response runbook (CC7.3
  / CC7.4).
- [`../community/SECURITY.md`](../community/SECURITY.md) —
  the community-facing security policy
  (the GitHub `SECURITY.md` convention
  the maintainer ships for the
  one-click "Report a vulnerability"
  button).
