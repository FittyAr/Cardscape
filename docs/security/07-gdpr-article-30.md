# 07 — GDPR Article 30 records of processing

> Article 30 of the General Data Protection
> Regulation requires every controller (and every
> processor on behalf of a controller) to keep a
> written record of processing activities. This
> document is the **template** the deployer fills
> in at deploy time. The data here is the v1.2.0
> defaults the project ships; the deployer adjusts
> the fields marked `OPERATOR` to match their
> production deployment.
>
> The compliance evidence bundle
> (`scripts/compliance-export.ps1`) packages this
> file alongside the GDPR compliance narrative
> (`docs/security/03-gdpr-compliance.md`) and the
> ASVS L1 matrix (`docs/security/06-asvs-controls.md`)
> so the deployer hands the data-protection officer
> a complete record.

---

## 1. Controller

| Field | Value |
|---|---|
| Controller name | OPERATOR (the deployer's legal entity) |
| Controller address | OPERATOR |
| Controller contact | OPERATOR (DPO email) |
| Representative in the Union (Art. 27) | OPERATOR, if the controller is established outside the EEA |

## 2. Processor

| Field | Value |
|---|---|
| Processor name | OPERATOR (self-hosted) or the SaaS provider the deployer resells through |
| Processor address | OPERATOR |
| Sub-processors | The deployer enumerates: hosting provider, transactional email provider, log aggregator, error tracker, payment processor (if applicable). The project's own components do not call any third-party services by default. |

## 3. Data Protection Officer (Art. 37-39)

| Field | Value |
|---|---|
| DPO name | OPERATOR |
| DPO email | OPERATOR |
| DPO appointed because | OPERATOR (public-sector body / core activities require DPO) |

## 4. Processing activities

The processing activities are the four workstreams
the project ships in v1.2.0. The deployer removes
any that the production deployment disables, and
adds any custom activities introduced by their
own integrations.

### 4.1 — User account provisioning and authentication

| Field | Value |
|---|---|
| Purpose | Provide a Cardscape account, authenticate the user, manage session state. |
| Legal basis | Art. 6(1)(b) — performance of a contract (the user signs up to use the service). |
| Categories of data subjects | Authenticated users. |
| Categories of personal data | Email address, display name, password hash, TOTP secret (encrypted), last login timestamp. |
| Special categories (Art. 9) | None. |
| Recipients | The data subject themselves; the deployer's staff with admin role; sub-processors the deployer enumerates (hosting, transactional email). |
| Transfers outside the EEA | The deployer enumerates: the hosting region is configurable via `Deployment:Region` (Unspecified / Europe / NorthAmerica / AsiaPacific / SouthAmerica). The default is `Unspecified`, which means the deployer's host. |
| Retention | Account is retained while the user is active. On `SoftDelete` (GDPR Art. 17 right-to-erasure), the account is anonymised 30 days later (the grace period lets the user recover from an accidental delete). The anonymised row stays in the database for FK resolution only; the PII is replaced with placeholders. |
| Security measures | PBKDF2-SHA256 (100k iterations) for password hashing; TOTP secrets encrypted at rest via `DataProtectionSecretProtector`; `IsAdmin` claim cached in JWT, no DB roundtrip on every request. |

### 4.2 — Workspace and board content

| Field | Value |
|---|---|
| Purpose | Run the kanban: store workspaces, boards, lists, cards, comments, attachments, automations, webhooks. |
| Legal basis | Art. 6(1)(b) — performance of a contract. |
| Categories of data subjects | Authenticated users; the people named in board content (card members, comment authors, @mentions). |
| Categories of personal data | Display name, email address (for board members), content the user authors (card title + description, comment body, attachment filename + body, automation expressions). |
| Special categories (Art. 9) | **Possible** if the user authors content that includes health, political, religious, or other Art. 9 data. The deployer's acceptable-use policy must require the controller to handle Art. 9 content under a separate Art. 9 lawful basis (typically Art. 9(2)(a) explicit consent). |
| Recipients | The workspace members; sub-processors the deployer enumerates (S3-compatible storage for attachments; Slack / Google / GitHub integrations if enabled). |
| Transfers outside the EEA | Same as 4.1. |
| Retention | Content is retained while the workspace is active. On workspace delete, content is soft-deleted and hard-deleted by the deployer's retention policy (default: 90 days soft-delete grace, configurable via `RetentionSweeper`). The deployer documents the exact policy in their production runbook. |
| Security measures | Role-based access control (`WorkspaceRole` Admin / Member / Observer, `BoardMember`); `RegionGuard` rejects cross-region writes; automations restricted to board members; webhook URLs validated at create-time; outbound HTTP clients reject RFC 1918 destinations (SSRF protection). |

### 4.3 — MCP resource subscriptions (AI clients)

| Field | Value |
|---|---|
| Purpose | Allow MCP-compatible AI clients to subscribe to board changes (`board://{id}` resources) and receive `notifications/resources/updated` push events. |
| Legal basis | Art. 6(1)(b) — performance of a contract. The user opted in when they minted the MCP API token. |
| Categories of data subjects | The MCP API token holder; indirectly the board members whose work is summarised in the resource. |
| Categories of personal data | The API token's `SubjectId` (user id); the `board://{id}` URIs; the subscription event log (`McpResourceBroadcaster`) — the broadcaster records `eventKind`, `uri`, `recordedAt` for the audit trail. The broadcaster does NOT record the body of the resource (the AI client re-fetches the resource on demand). |
| Special categories (Art. 9) | None directly; the AI client must apply its own Art. 9 policy on the re-fetched body. |
| Recipients | The MCP API token holder; the deployer's staff with admin role (audit trail). |
| Transfers outside the EEA | The MCP process and the API are deployed together (same region by default); the deployer documents the region. |
| Retention | Subscription event log is retained per the deployer's `RetentionSweeper` config (default: 30 days). The event log is admin-visible via `/api/admin/mcp-subscriptions` (McpSubscriptionsAdminPolicy). |
| Security measures | The MCP API token is bearer-only; the resource URL is opaque (`board://{id}`); the broadcaster runs on the same trust boundary as the API; admin-only read access to the event log; McpSubscriptionsAdminPolicy uses a cached `is_admin` claim (no DB roundtrip on every request). |

### 4.4 — Audit, observability, and incident response

| Field | Value |
|---|---|
| Purpose | Run the service: structured logs, error tracking, security events, DSR (data subject request) fulfilment. |
| Legal basis | Art. 6(1)(f) — legitimate interest (the deployer's ability to operate the service and respond to incidents). The deployer must run a balancing test against the data subject's rights and freedoms. |
| Categories of data subjects | Anyone whose actions appear in the logs (every authenticated request logs the user id; anonymous requests log the IP). |
| Categories of personal data | User id, IP, user-agent, request path, status code, duration. Security events (Art. 5(1)(f) integrity, Art. 32 security of processing) include login attempts, failed authentications, admin actions. |
| Special categories (Art. 9) | None — the project does not log card content or comments. The deployer must NOT add request-body logging. |
| Recipients | The deployer's staff (admin role); the log sink the deployer wires (operator-action). |
| Transfers outside the EEA | The deployer enumerates: the log sink is configurable (`Serilog:Sinks:*`). The default is the local file system + OpenTelemetry. |
| Retention | Logs are retained per the deployer's policy (the project ships a 30-day default). The deployer documents the retention period in their production runbook. |
| Security measures | Serilog structured logging with the `cardscape.security` logger name for auth events; the log redaction filter strips common secret patterns (`password=`, `token=`, `Authorization: Bearer`); no request bodies in logs. |

## 5. Cross-cutting concerns

### 5.1 — International transfers (Chapter V)

The project does not initiate cross-border
transfers by default. The deployer enumerates
their sub-processors and the transfer mechanism
for each (Standard Contractual Clauses, adequacy
decision, etc.) in their own Article 30 record.

### 5.2 — Data Protection by Design and by Default (Art. 25)

| Measure | Implementation |
|---|---|
| Soft-delete + grace period | `User.SoftDelete` + 30-day grace + automated `Anonymise`. |
| Right to erasure (Art. 17) | `POST /api/admin/users/{id}` with `action=delete`; `User.Anonymise` replaces PII with placeholders. |
| Right of access (Art. 15) | `GET /api/admin/users/{id}/export` returns a JSON+attachments zip. |
| Right to rectification (Art. 16) | `User.Rename` + `User.ChangeEmail` command handlers; the user can do this through the profile UI; admins can do it through `/api/admin/users/{id}` with `action=rename`. |
| Right to data portability (Art. 20) | Same export endpoint as Art. 15; the JSON shape is the user's machine-readable dump. |
| Right to restriction (Art. 18) | `User.SetRestricted(true)` blocks writes; reads remain available. |
| Right to object (Art. 21) | The deployer's privacy policy documents the manual process; the system does not have automated profiling per Art. 22. |
| Data minimisation (Art. 5(1)(c)) | The domain rejects empty / null fields at the value-object level (`EmailAddress.Create` requires shape, `DisplayName.Create` requires length). |
| Storage limitation (Art. 5(1)(e)) | `RetentionSweeper` purges soft-deleted users + their activity past the grace period. |
| Integrity and confidentiality (Art. 5(1)(f)) | PBKDF2 password hashing; JWT signature + lifetime validation; TLS in production; DTOs do not leak `PasswordHash` or `IsAdmin` to the wire. |

### 5.3 — Security of processing (Art. 32)

See `docs/security/04-soc2-readiness.md` for the
full SOC 2 mapping; Art. 32 is the GDPR analog and
the SOC 2 controls are a superset.

## 6. Breach notification

| Step | Owner | SLA |
|---|---|---|
| Detect | The deployer's SIEM / log aggregator. | As configured by the deployer. |
| Contain | The deployer's incident response team. | As configured. |
| Assess (risk to data subjects) | The deployer's DPO. | 24 hours. |
| Notify the supervisory authority | The deployer's DPO. | 72 hours from awareness (Art. 33). |
| Notify affected data subjects | The deployer's DPO. | Without undue delay (Art. 34). |
| Document the breach | The deployer's DPO. | 30 days. |

The coordinated disclosure policy is at
`docs/security/05-vulnerability-disclosure.md`.

## 7. Data Protection Impact Assessment (DPIA)

A DPIA is required for processing likely to result
in a high risk to the rights and freedoms of
data subjects. The deployer MUST complete a DPIA
before:

- Deploying with sub-processors that process
  Art. 9 (special categories) data.
- Deploying with AI clients that perform automated
  decision-making with legal effect (Art. 22).
- Deploying with cross-border transfers outside
  the EEA without an adequacy decision.

A DPIA template is out of scope for v1.2.0; the
deployer is expected to use their own jurisdiction's
template (e.g. the CNIL's in France, the ICO's in
the UK, the AEPD's in Spain, the ANPD's in Brazil).

## 8. Data subject request workflow

| Request | Workflow |
|---|---|
| Access (Art. 15) | User exports their own data via the profile UI; admin exports any user via `/api/admin/users/{id}/export`. The export is a zip with `data.json` (machine-readable) and the user's attachments. |
| Rectification (Art. 16) | User edits their profile; admin edits via `/api/admin/users/{id}`. |
| Erasure (Art. 17) | User deletes their account; the account is soft-deleted (30-day grace), then anonymised. Admin can also delete any user (e.g. legal hold). |
| Restriction (Art. 18) | Admin sets `IsRestricted=true`; the user can read but not write. |
| Portability (Art. 20) | Same as Art. 15. |
| Object (Art. 21) | The deployer's privacy policy documents the manual process. |

## 9. Sub-processor register

| Sub-processor | Purpose | Region | DPA / SCC |
|---|---|---|---|
| OPERATOR (hosting) | Hosts the API + MCP + Web. | OPERATOR | OPERATOR |
| OPERATOR (email) | Transactional email (DSR, password reset). | OPERATOR | OPERATOR |
| OPERATOR (logs) | SIEM / log aggregator. | OPERATOR | OPERATOR |
| OPERATOR (errors) | Error tracker. | OPERATOR | OPERATOR |

The deployer fills this in.

## 10. Operator-action checklist (the deployer must do this)

- [ ] Fill in the OPERATOR fields above.
- [ ] Run a balancing test on Art. 6(1)(f) for the
  audit / observability processing.
- [ ] Enumerate sub-processors and sign DPAs with each.
- [ ] Run a DPIA if any of the three DPIA triggers
  apply to the production deployment.
- [ ] Document the breach notification SLA in the
  deployer's incident response runbook.
- [ ] Document the data subject request workflow
  in the deployer's privacy policy.
- [ ] Add this file to the compliance evidence
  bundle: `pwsh ./scripts/compliance-export.ps1`.
