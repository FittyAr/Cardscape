# 03 — GDPR compliance

> The project's GDPR posture, in the form a Data
> Protection Officer (DPO) or external auditor
> can review. Covers the lawful basis, the
> data inventory (Record of Processing
> Activities, ROPA), the data subject
> rights, the breach-notification process,
> the cross-border transfer rules, the
> Data Protection Impact Assessment (DPIA)
> for high-risk processing, and the
> privacy-by-design decisions baked into
> the code.
>
> This document is **template** quality
> today: it gives the project a runnable
> starting point. The official,
> legally-binding text is the
> institution's deployed privacy policy;
> the project's privacy notice lives at
> [`../operations/PRIVACY.md`](../operations/PRIVACY.md)
> (or whatever URL the deploying org
> publishes under their own domain).
> The maintainer ships this template
> for the self-hosted case.

---

## 1. Scope and role

Cardscape is **a self-hosted kanban tool**:
the deploying organisation is the data
controller, and Cardscape is the data
processor. The maintainer (the Cardscape
project) ships the software; the deploying
organisation is on the hook for the
controller-side obligations (privacy notice,
consent, data subject rights, DPO appointment,
cross-border-transfer analysis, breach
notification).

This document covers the **processor-side**
obligations: what Cardscape does with the
data on the controller's behalf. The
controller-side obligations are summarised
in §9 (controller checklist) so a deployer
can use it as a starting point.

## 2. Lawful basis

The lawful basis for processing personal data
in Cardscape is the **legitimate interest**
of the controller (the deploying
organisation) in providing a kanban tool to
its users (the data subjects, who are the
controller's employees, contractors, or
customers). Cardscape is a B2B-style tool;
the data subjects are typically a captive
audience the controller has a clear
relationship with (employees, contractors).

The deployer is responsible for documenting
the legitimate-interest assessment in their
own records (a "Legitimate Interest
Assessment" or LIA is the standard artefact).
The project's contribution to the LIA is
the data inventory in §3.

## 3. Record of Processing Activities (ROPA)

Article 30 of the GDPR requires every
controller and processor to maintain a
Record of Processing Activities. The
processing Cardscape performs on the
controller's behalf:

| # | Processing activity | Data categories | Data subjects | Retention | Recipients | Cross-border? |
|---|---|---|---|---|---|---|
| 3.1 | User account management | email, display name, password hash (Argon2id), MFA secret (TOTP) | users (controller's employees) | account lifetime + 30 days soft-delete grace | none | only the controller's chosen DB region |
| 3.2 | Workspace / board / list / card CRUD | text content the user types into Cardscape | users, plus third parties the user mentions by name | workspace lifetime | workspace members | only the controller's chosen DB region |
| 3.3 | Activity feed | user id, action type, target entity id, timestamp | users | 365 days (rolling) | workspace admins | only the controller's chosen DB region |
| 3.4 | Audit log (security events) | user id, IP, user-agent, action, result | users | 730 days (rolling; aligned with SOC 2 CC7.2) | security admins | only the controller's chosen DB region |
| 3.5 | API tokens | token name, prefix, hashed secret, scopes, last used timestamp | users | until user revokes or 90 days of inactivity | users themselves | only the controller's chosen DB region |
| 3.6 | OAuth 2.0 third-party apps | app name, allowed scopes, redirect URIs, hashed client secret | users | until user revokes | users themselves | only the controller's chosen DB region |
| 3.7 | Email integration (inbound) | email headers, body, attachment metadata | external senders | until the controller deletes the address binding | workspace members | the email provider's region (SendGrid, Mailgun, etc.) |
| 3.8 | Slack / Google / GitHub / Drive integrations | OAuth tokens (encrypted at rest), channel/repo metadata, event payloads | users + the integration's external users | until the user disconnects | workspace members | the integration's region (Slack, Google, GitHub) |
| 3.9 | MCP server (AI clients) | user id, client id, tool call arguments and results, resource subscription URIs | users | account lifetime | none (MCP clients connect to the controller's MCP server, not the maintainer's) | only the controller's chosen region |
| 3.10 | Backups | full DB snapshots (encrypted with the deployer's age / KMS key) | all of the above | 30 days (rolling) | deployer's backup destination | deployer's choice |
| 3.11 | Logs (operational) | user id (when authenticated), IP, request path, response code, response time | users | 30 days (rolling) | deployer's log destination | deployer's choice |
| 3.12 | Error reporting (Serilog) | stack trace, request path, user id (when authenticated) | users | 90 days (rolling) | deployer's log destination | deployer's choice |

The retention numbers in the table are the
**project's recommended defaults**. The
deployer is free to override them via
`Cardscape:Retention:*` configuration keys
(see §7).

## 4. Data subject rights

The deployer is responsible for honouring
data subject rights; the project's
contribution is to make the rights
**implementable** in the data model and the
admin API.

### 4.1 Right of access (Art. 15)

A user can request a copy of all personal
data Cardscape holds about them. The
project ships a `GET /api/users/{id}/export`
endpoint (admin-only) that:

- serialises the user record
- serialises every card the user authored
  (with the user's display name)
- serialises every comment the user posted
- serialises every activity-feed entry
  involving the user
- serialises every audit-log entry
  involving the user
- returns the bundle as a JSON download

The right-of-access bundle is **not** a
"GDPR data export" in the full
right-of-data-portability sense (Art. 20);
see 4.4 for the portability export.

### 4.2 Right to rectification (Art. 16)

Users can change their display name and
email through the Web UI. The project does
not auto-rectify historical content (cards,
comments) because that would alter other
users' records; the user can edit their own
content through the normal edit paths.

### 4.3 Right to erasure (Art. 17)

A user can request account deletion. The
project ships a `DELETE /api/users/{id}`
endpoint (admin-only) that:

- soft-deletes the user record
  (`IsDeleted = true`, `DeletedAt = now`)
- clears the email, display name, and
  password hash
- revokes every API token
- revokes every OAuth app
- replaces the user's display name in
  authored content with the placeholder
  "Deleted user" (so the card text
  remains intact, but no longer references
  the user by name)
- clears the user's IP, user-agent, and
  session id from the audit log and the
  activity feed

The soft-delete grace period is 30 days
(configurable). At the end of the grace
period, a background job hard-deletes the
user record and the placeholder is
replaced with the literal string
"[erased]".

### 4.4 Right to data portability (Art. 20)

A user can request a portable copy of their
data. The project ships a
`GET /api/users/{id}/portability` endpoint
(admin-only) that returns a JSON document
in the format the user provided the data
in (cards as cards, comments as comments,
not a flattened bundle). The user can
import this document into another Cardscape
instance via `POST /api/imports/user-export`.

### 4.5 Right to restriction (Art. 18)

A user can request that the controller
restrict processing. The project ships a
`POST /api/users/{id}/restrict` endpoint
(admin-only) that sets `IsRestricted = true`
on the user record; restricted users can
read but not write. The deployer is
responsible for the controller-side
decision to grant restriction; the project
exposes the flag.

### 4.6 Right to object (Art. 21)

A user can object to processing for
direct-marketing purposes. Cardscape does
not perform direct marketing; the
notification surface is in-app only and
opt-in per channel. The deployer can use
the same `IsRestricted` flag from 4.5 to
honour an opt-out: set the flag, the
notification dispatcher skips the user.

### 4.7 Rights related to automated decision-making (Art. 22)

Cardscape uses an LLM for two surfaces:
the AI "generate description" action on
cards and the AI "summarize thread" action
on comment threads. Both are user-initiated
and user-confirmed; the LLM does not act
autonomously. The deployer must document
this in their privacy notice and offer the
user the right to opt out of the AI
surfaces. Cardscape does not currently ship a deployment-level AI toggle;
operators that prohibit AI must block the configured `Ai:Endpoint` and document
that policy.

## 5. Breach notification

### 5.1 Project-side (the maintainer)

The maintainer (the Cardscape project) does
not hold any data; the maintainer ships
software. The maintainer's contribution to
breach response is:

- a published **CVE process** — see
  [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md)
- a 90-day disclosure window for security
  fixes
- a security advisory template
- an opt-in **security mailing list**
  (`security@cardscape.local` for the
  maintainer's own deployments; deployers
  maintain their own equivalents)

### 5.2 Deployer-side (the controller)

The deployer is responsible for breach
notification under Art. 33 (to the
supervisory authority, within 72 hours) and
Art. 34 (to data subjects, without undue
delay). The project contributes:

- an audit log the deployer can search
  (§4.1) to scope the breach
- a `Cardscape:Breach:Simulate` config key
  the deployer can use to dry-run the
  notification path
- a template breach notification letter in
  [`templates/breach-notification.md`](../security/templates/breach-notification.md)

## 6. Cross-border data transfers

The GDPR restricts transfers of personal
data outside the European Economic Area
(EEA) unless the destination has an
**adequacy decision** or the controller
implements **Standard Contractual Clauses**
(SCCs) or another Chapter V mechanism.

The project's contribution to the
controller's transfer analysis:

- The default database is SQLite, which
  runs on the same host as the API. No
  transfer.
- The default log destination is a local
  file on the API host. No transfer.
- The optional OTel / DB sinks for
  observability send data to the
  controller-configured endpoint. The
  controller is responsible for choosing
  an endpoint in their jurisdiction or
  implementing SCCs with their vendor.
- The Slack / Google / GitHub / Drive
  integrations send data to the
  integration provider's infrastructure.
  The controller is responsible for
  documenting this in the privacy notice
  and implementing SCCs with the
  provider if the provider is outside the
  EEA.
- The email integration sends data to
  the email provider's infrastructure.
  Same analysis as the third-party
  integrations.

## 7. Data minimisation and retention

The project ships the following retention
defaults; each is configurable under
`Cardscape:Retention:*` so the deployer
can adjust to their jurisdiction.

| Data | Default retention | Configurable? | Hard-deletion trigger |
|---|---|---|---|
| Soft-deleted user record | 30 days | yes | background job |
| Activity feed entries | 365 days | yes | background job |
| Audit log entries | 730 days | yes | background job |
| API token last-used timestamp | (rolling) | yes (90 days default of "inactive") | background job |
| Operational logs | 30 days | yes | log rotation |
| Error reports (Serilog) | 90 days | yes | log rotation |
| Email integration inbound payloads | until address binding is removed | no (managed by user) | user action |
| Third-party integration tokens | until user disconnects | no (managed by user) | user action |
| Backups | 30 days | yes | backup rotation |

The retention background jobs are
implemented as scheduled `IHostedService`s
in `src/Cardscape.Infrastructure/Hosting/RetentionSweeper.cs`.
Each sweeper logs a structured event for
every batch it deletes, so the audit log
captures the deletion.

## 8. Privacy by design (decisions in the code)

- **Password storage**: Argon2id with the
  OWASP-recommended parameters (memory
  cost 19 MiB, iterations 2, parallelism 1).
  The hash is never returned through the
  API.
- **Session storage**: server-side, in
  SQLite. The session cookie is the
  opaque `session-id` (no JWT).
- **MFA**: TOTP (RFC 6238). The shared
  secret is encrypted at rest with the
  data-protection key.
- **API tokens**: hashed with SHA-256
  before storage. The plaintext token is
  shown to the user exactly once at
  creation time. The token prefix (first
  8 chars) is shown in the Web UI for
  identification.
- **OAuth 2.0 client secrets**: hashed
  with the same Argon2id parameters as
  passwords. Shown to the user exactly
  once at registration.
- **Third-party integration tokens**:
  encrypted at rest with the
  data-protection key. The encryption
  uses `Microsoft.AspNetCore.DataProtection`
  with the controller's key ring.
- **Email integration**: the email
  address is stored in plaintext (it
  has to be, for routing). The body
  and headers are stored in plaintext
  (no PII detection / redaction
  because the controller is the
  legitimate-interest decision-maker).
- **Card and comment content**: stored
  in plaintext (the controller is the
  decision-maker for content-level
  redaction).
- **AI features**: the LLM provider
  receives the card text the user
  selected, plus a system prompt. The
  LLM does not receive the user's
  history, other cards, or other
  users' data. The deployer is
  responsible for choosing an LLM
  provider in their jurisdiction and
  documenting the transfer.

## 9. Controller checklist (for the deployer)

The deployer is the data controller. The
project ships the processor-side
infrastructure; the controller-side
obligations are the deployer's
responsibility. The minimum the deployer
must do before going live:

- [ ] Appoint a Data Protection Officer
  (DPO) if required (Art. 37 — typically
  yes for public authorities, large
  organisations, or core-business
  monitoring of data subjects on a
  large scale).
- [ ] Publish a privacy notice covering
  Cardscape processing. Template in
  [`templates/privacy-notice.md`](../security/templates/privacy-notice.md).
- [ ] Maintain a Record of Processing
  Activities (Art. 30) using §3 as the
  starting point.
- [ ] Run a Legitimate Interest
  Assessment (LIA) for the kanban use
  case, using §2 and §3 as the input.
- [ ] Configure retention (§7) to match
  the controller's documented
  retention policy.
- [ ] Configure observability (§6) so
  logs and metrics do not cross borders
  unless an adequacy / SCC mechanism is
  in place.
- [ ] Document the data subject rights
  procedure (§4) and put it in the
  privacy notice.
- [ ] Set up a breach-response runbook
  (§5.2) and rehearse it at least
  annually.
- [ ] Set up a Data Protection Impact
  Assessment (DPIA) for any high-risk
  processing the controller performs
  through Cardscape (the default
  kanban use case is **not** high-risk;
  the AI features and the large-scale
  monitoring use cases **are**). DPIA
  template in
  [`templates/dpia.md`](../security/templates/dpia.md).
- [ ] Document cross-border transfers
  (§6) and implement SCCs with the
  controller's vendors as needed.
- [ ] Train staff on the privacy notice
  and the breach-response runbook.

## 10. References

- [`01-threat-model.md`](01-threat-model.md) —
  the STRIDE analysis. Privacy-related
  threats (Information Disclosure,
  Repudiation) are in the **I** and
  **R** columns.
- [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md) —
  the secure-coding rules the contributors
  follow to keep the privacy posture in
  place.
- [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md) —
  the maintainer's CVE process.
- [`../operations/PRIVACY.md`](../operations/PRIVACY.md) —
  the privacy notice template.
- [`templates/privacy-notice.md`](../security/templates/privacy-notice.md) —
  the privacy notice template (deployer
  fills in their organisation details).
- [`templates/breach-notification.md`](../security/templates/breach-notification.md) —
  the breach notification template.
- [`templates/dpia.md`](../security/templates/dpia.md) —
  the DPIA template.
- [`../adr/0007-privacy-by-default.md`](../adr/0007-privacy-by-default.md) —
  the ADR that captures the
  privacy-by-design decisions.
