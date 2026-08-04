# 04 — SOC 2 readiness

> The project's SOC 2 Type II readiness
> posture, organised by the five Trust
> Services Criteria (TSC): Security,
> Availability, Processing Integrity,
> Confidentiality, Privacy. The project
> targets the **Security** criterion as
> the primary; the other four are
> available as add-ons.
>
> This is **readiness**, not certification.
> Cardscape is open-source software; the
> project ships the controls. The
> deployer hires a licensed CPA firm to
> perform the Type II audit. The project
> provides the artefacts (this document,
> the threat model, the secure-coding
> checklist, the runbooks) so the
> deployer's audit window is shorter and
> cheaper than starting from scratch.

---

## 1. Scope and trust services criteria

The project targets the **Security**
criterion (Common Criteria CC1-CC9) as
the default. The other four criteria are
optional add-ons:

| TSC | In scope? | Notes |
|---|---|---|
| **Security (CC)** | **Yes (default)** | the project's primary criterion |
| **Availability (A)** | Optional add-on | the project ships the uptime primitives; the deployer certifies against their own SLO |
| **Processing Integrity (PI)** | Optional add-on | relevant for the third-party integration surface (Slack / Google / GitHub) |
| **Confidentiality (C)** | Optional add-on | relevant when the deployer is a financial-services or healthcare org |
| **Privacy (P)** | Optional add-on | the GDPR compliance work in [`03-gdpr-compliance.md`](03-gdpr-compliance.md) is the input |

A deployer that wants **all five** is
called out as such in their SOC 2
report; the project's contribution to
the audit is the same regardless (the
five TSCs share most of the controls).

## 2. Common Criteria (CC) coverage

The Common Criteria are CC1-CC9. The
project's coverage of each:

### CC1 — Control environment

| Control | Project artefact | Where it lives |
|---|---|---|
| CC1.1 — commitment to integrity and ethical values | the maintainer's Code of Conduct | [`../community/CODE_OF_CONDUCT.md`](../community/CODE_OF_CONDUCT.md) |
| CC1.2 — board of directors demonstrates independence and exercises oversight | n/a (open-source project, no board) | the deployer documents their own oversight |
| CC1.3 — establishes structures, reporting lines, and authorities | the bounded-context map | [`../architecture/01-bounded-contexts.md`](../architecture/01-bounded-contexts.md) |
| CC1.4 — demonstrates commitment to competence | the onboarding doc | [`../development/00-onboarding.md`](../development/00-onboarding.md) |
| CC1.5 — enforces accountability | the audit log | `cardscape.audit_log` table; the activity feed is the user-visible projection |

### CC2 — Communication and information

| Control | Project artefact | Where it lives |
|---|---|---|
| CC2.1 — obtains and uses relevant, quality information | the structured logging (Serilog) | every I/O operation |
| CC2.2 — internally communicates information | the activity feed | every state change has an Activity entry |
| CC2.3 — communicates with external parties | the vulnerability disclosure policy | [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md) |

### CC3 — Risk assessment

| Control | Project artefact | Where it lives |
|---|---|---|
| CC3.1 — specifies objectives | the project goals (kanban + MCP + Trello parity) | [`../roadmap/02-product-positioning.md`](../roadmap/02-product-positioning.md) |
| CC3.2 — identifies risks | the threat model | [`01-threat-model.md`](01-threat-model.md) |
| CC3.3 — considers fraud potential | the audit log + the rate limit | every mutation has an audit entry; per-API-token rate limit at 100 req/min |
| CC3.4 — identifies and analyses significant change | the ADR process | [`../adr/`](../adr/) (every change that affects a trust boundary gets an ADR) |

### CC4 — Monitoring activities

| Control | Project artefact | Where it lives |
|---|---|---|
| CC4.1 — performs ongoing and separate evaluations | the CI pipeline + the architecture tests | `.github/workflows/ci.yml`; `tests/Cardscape.ArchitectureTests/` |
| CC4.2 — evaluates and communicates deficiencies | the CI failure notifications; the GitHub Issues list | the project tracks every CI failure as a GitHub issue |

### CC5 — Control activities

| Control | Project artefact | Where it lives |
|---|---|---|
| CC5.1 — selects and develops control activities | the secure-coding checklist | [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md) |
| CC5.2 — selects and develops general control activities over technology | the infrastructure-as-code, the deployment runbook | [`../operations/02-deployment.md`](../operations/02-deployment.md) |
| CC5.3 — deploys through policies and procedures | the developer workflow | [`../development/00-onboarding.md`](../development/00-onboarding.md) |

### CC6 — Logical and physical access

| Control | Project artefact | Where it lives |
|---|---|---|
| CC6.1 — logical access security software, infrastructure, and architectures | the JWT bearer + API token + OAuth 2.0 auth scheme | `src/Cardscape.Api/Authentication/`, `src/Cardscape.Mcp/Authentication/` |
| CC6.2 — registers and authorizes new users | the registration flow | `src/Cardscape.Api/Endpoints/Auth/`; identity verification by email confirmation |
| CC6.3 — authorizes, modifies, or removes access | the admin API + the audit log | every role change, every user creation, every session revocation has an audit entry |
| CC6.4 — restricts physical access | n/a (the deployer runs the software) | the deployer's hosting provider certifies physical access (e.g. SOC 2 of the cloud provider) |
| CC6.5 — discontinues protection of physically removed assets | n/a (the deployer runs the software) | the deployer documents their own equipment-disposal policy |
| CC6.6 — implements logical access security measures over external threats | the rate limit + the CORS policy + the CSP header | `src/Cardscape.Api/Extensions/SecurityHeaders.cs` |
| CC6.7 — restricts the transmission, movement, and removal of information | TLS 1.2+ (the deployer terminates); the OAuth 2.0 redirect URI allow-list | the deployer configures their reverse proxy / load balancer |
| CC6.8 — implements controls to prevent or detect and act on the introduction of unauthorised or malicious software | the NuGet dependency audit (Dependabot + `dotnet list package --vulnerable`) + the architecture tests | `.github/workflows/ci.yml` runs the audit on every PR |

### CC7 — System operations

| Control | Project artefact | Where it lives |
|---|---|---|
| CC7.1 — detects configuration vulnerabilities | the security headers + the CORS policy + the rate limit | every request goes through the middleware |
| CC7.2 — monitors system components for anomalies | the audit log + the rate limit alarm | the audit log is the source of truth; the deployer wires SIEM rules |
| CC7.3 — evaluates security events | the incident-response runbook | [`../operations/04-incident-response.md`](../operations/04-incident-response.md) |
| CC7.4 — responds to security incidents | the incident-response runbook | same |
| CC7.5 — recovers from identified security incidents | the backup + restore runbook | [`../operations/05-backup-restore.md`](../operations/05-backup-restore.md) (TODO) |

### CC8 — Change management

| Control | Project artefact | Where it lives |
|---|---|---|
| CC8.1 — authorises, designs, develops, acquires, configures, documents, tests, approves, and implements changes | the ADR process + the PR review + the CI pipeline | every change gets an ADR (or a PR description); every PR runs the build, the unit tests, the architecture tests, and the integration tests |

### CC9 — Risk mitigation

| Control | Project artefact | Where it lives |
|---|---|---|
| CC9.1 — identifies, selects, and develops risk mitigation activities | the secure-coding checklist + the architecture tests | the checklist is the source of truth; the architecture tests pin the rules |
| CC9.2 — assesses and manages risks associated with vendors and business partners | the third-party-integration risk register | TODO: ship a register template |
| CC9.3 — assesses and manages risks associated with the deployment of new technologies | the migration plan template | the project has shipped .NET 8 → .NET 10 migrations as templates; see git history |

## 3. Logical access (CC6) deep dive

The project's logical-access implementation
is the single most-scrutinised control in
a SOC 2 audit. The detail:

### CC6.1 — authentication schemes

Cardscape supports four authentication
schemes, in increasing order of privilege:

| Scheme | Issued by | Lifetime | Revocable? | Use case |
|---|---|---|---|---|
| **Session cookie** | the API on successful login | 30 days, sliding | yes (logout, password change) | the Web UI |
| **API token** | the user via the Web UI | 90 days, no sliding; rotates on use | yes (Web UI revoke) | scripts, CI, AI clients |
| **OAuth 2.0 access token** | the API on successful auth-code exchange | 1 hour | yes (refresh-token revocation) | third-party apps |
| **OAuth 2.0 client credentials** | the API on successful client_credentials grant | 1 hour | yes (app revocation) | service-to-service |

Every scheme mints a `principal` the
authorisation middleware reads on every
request. The principal is the input to
every authorisation check; the
authorisation logic does not care which
scheme minted it.

### CC6.2 — user lifecycle

| Event | Effect | Audit entry |
|---|---|---|
| Registration | creates the user record, sends a confirmation email | `auth.user_registered` |
| Email confirmation | flips `IsEmailConfirmed` to `true` | `auth.email_confirmed` |
| Login (success) | mints a session, records IP and user-agent | `auth.login_success` |
| Login (failure) | records the attempt, increments the failure counter | `auth.login_failure` |
| Password change | invalidates every other session | `auth.password_changed` |
| MFA enrolment | stores the TOTP secret, returns the recovery codes | `auth.mfa_enrolled` |
| MFA disable | requires re-auth + audit | `auth.mfa_disabled` |
| API token creation | hashes the secret, stores the prefix | `auth.api_token_created` |
| API token revocation | removes the token from the DB | `auth.api_token_revoked` |
| OAuth app registration | hashes the client secret | `oauth.app_registered` |
| OAuth app revocation | flips `IsRevoked = true` | `oauth.app_revoked` |
| User deletion (soft) | clears PII, replaces display name in content | `user.deleted` |
| User hard-deletion | removes the user record | `user.purged` |

### CC6.3 — authorisation model

The authorisation model is **role-based
plus resource-based**:

- **Workspace role**: Owner, Admin,
  Member, Guest. The role is recorded on
  the `WorkspaceMember` table.
- **Board role**: Admin, Member,
  Observer. Recorded on the `BoardMember`
  table. A workspace Admin can override
  a board role.
- **Card permission**: derived from the
  board role plus the card's `IsPrivate`
  flag. A private card is visible only to
  its author and the board Admins.

The role checks are centralised in
`src/Cardscape.Application/Authorisation/`;
the policy scheme is registered in
`src/Cardscape.Api/Extensions/Authorisation.cs`.
The architecture tests pin the rule "no
endpoint can do an authorisation check
inline; the policy scheme is the only
allowed pattern".

### CC6.6 — rate limit

The per-API-token rate limit is
**100 requests / minute**, sliding
window. The default is in
`src/Cardscape.Api/RateLimiting/`. The
deployer can override via
`Cardscape:RateLimit:RequestsPerMinute`.
The response on rate-limit breach is
HTTP 429 with a `Retry-After` header.

### CC6.7 — transmission security

The project does not terminate TLS; the
deployer's reverse proxy (nginx, Caddy,
Traefik, ALB, etc.) does. The project's
contribution is:

- the security headers middleware
  (`src/Cardscape.Api/Extensions/SecurityHeaders.cs`):
  CSP, HSTS (1 year, preload-eligible),
  X-Frame-Options, X-Content-Type-Options,
  Referrer-Policy, Permissions-Policy
- the HSTS preload list submission
  (the deployer adds their domain to
  [hstspreload.org](https://hstspreload.org/))
- the OAuth redirect URI allow-list
  (the user can register a URI per OAuth
  app; wildcards are forbidden)
- the CORS policy (the deployer
  configures the allowed origins via
  `Cardscape:Cors:AllowedOrigins`)

### CC6.8 — dependency vulnerability scan

The CI pipeline runs
`dotnet list package --vulnerable` on
every PR. The output is uploaded as a
GitHub Actions artifact; the CI fails
if any package reports a known
vulnerability of severity High or
Critical. The project also enables
GitHub Dependabot on
`Directory.Packages.props` for
proactive PRs on vulnerable
dependencies.

## 4. Availability (A) coverage

The project's contribution to the
Availability criterion:

| Control | Project artefact | Notes |
|---|---|---|
| A1.1 — capacity planning | the horizontal scale-out path | the API is stateless; the DB is the bottleneck; the deployer is responsible for the capacity sizing |
| A1.2 — environmental protections | the deployer's hosting | the deployer certifies the data-centre SOC 2 |
| A1.3 — disaster recovery | the backup + restore runbook | TODO: ship the runbook in v1.3.0 |
| A2.1 — system monitoring | the Serilog → log destination path + the OTel traces | the deployer wires the alert rules |
| A2.2 — system availability | the health check endpoints | `/health/live` and `/health/ready` on the API and the MCP |
| A2.3 — system recovery | the idempotent command pattern (Wolverine) | the same command can be replayed; the handler decides whether to apply the side effect |

## 5. Processing Integrity (PI) coverage

The project's contribution to PI:

| Control | Project artefact | Notes |
|---|---|---|
| PI1.1 — obtain and use relevant data | the input validation (`FluentValidation` on the Application commands) | every command has a validator; the validator is unit-tested |
| PI1.2 — implement policies and procedures | the idempotency middleware | the same command can be replayed; the handler dedupes by the idempotency key |
| PI1.3 — implement policies for inputs | the schema validation + the OAuth redirect URI allow-list + the file-upload content-type check | the file upload rejects any content type that is not in the allow-list |
| PI1.4 — implement policies for processing | the bounded-context map + the architecture tests | the architecture tests pin the rule "Application depends on Domain, but not vice versa" |
| PI1.5 — implement policies for outputs | the `Result<T>` pattern | every command returns a `Result<T>`; the controller maps the error to the HTTP status code |
| PI2.1 — implement policies for system inputs | the input validation | every command validates its inputs |
| PI2.2 — implement policies for system processing | the Wolverine command bus | the bus is the single entry point; no endpoint can write to the DB without going through the bus |
| PI2.3 — implement policies for system outputs | the output projection (DTOs) | the DB entities are never serialised; the Application layer projects to DTOs |

## 6. Confidentiality (C) coverage

The project's contribution to the
Confidentiality criterion:

| Control | Project artefact | Notes |
|---|---|---|
| C1.1 — identify and protect confidential information | the Argon2id password hash + the SHA-256 API token hash + the data-protection encryption for the OAuth integration tokens | the encryption keys are the deployer's |
| C1.2 — dispose of confidential information | the retention sweeper | the soft-delete grace + the hard-delete background job |
| C2.1 — manage changes to confidential information | the audit log | every change to a confidential field has an audit entry |

## 7. Privacy (P) coverage

The Privacy criterion maps to
[`03-gdpr-compliance.md`](03-gdpr-compliance.md).
The P1-P8 criteria are the SOC 2
incarnation of the GDPR principles; the
project's GDPR document is the input.

## 8. SOC 2 audit artefacts (what the deployer needs)

For a Type II audit window (typically
6-12 months), the deployer needs:

- [ ] **System description** — the
  deployer's narrative of the system
  using the project's architecture docs
  as input. The project ships
  [`../architecture/`](../architecture/)
  for this.
- [ ] **Control matrix** — the
  deployer's mapping of their controls
  to the TSC. The project ships this
  document (§2-§7) for the project-side
  controls; the deployer adds their
  own controls (hosting, backups,
  physical access).
- [ ] **Auditor evidence** — the
  deployer's log of controls operating
  during the audit window. The project
  ships the audit log (§3.5) and the
  CI run history (`.github/workflows/`)
  as the input.
- [ ] **Risk assessment** — the
  deployer's annual risk assessment.
  The project ships
  [`01-threat-model.md`](01-threat-model.md)
  as the input.
- [ ] **Vendor management** — the
  deployer's list of vendors and their
  SOC 2 status. The project's third-party
  integrations are a vendor; the
  deployer adds the integration
  provider's SOC 2 letter to the file.
- [ ] **Change management evidence** —
  the PR history + the CI history +
  the ADR history. The project ships
  all three on the public repository.

## 9. The audit window (Type II)

A SOC 2 Type II audit requires a
**6-12 month audit window** during
which the controls are operating. The
project's recommendation:

1. **Month 0**: hire a CPA firm;
   share this document, the threat
   model, the secure-coding checklist,
   and the runbooks.
2. **Month 1-2**: deployer configures
   their hosting; turns on the
   audit log; wires the log to the
   SIEM.
3. **Month 3**: the audit window
   starts.
4. **Month 9-12**: the audit window
   ends; the auditor inspects the
   evidence.
5. **Month 12-15**: the auditor
   issues the Type II report.

The project ships the project-side
artefacts (this document, the threat
model, the secure-coding checklist,
the runbooks); the deployer is
responsible for the deployer-side
artefacts (system description, vendor
list, physical-access controls,
capacity planning, backup-restore
evidence).

## 10. References

- [`01-threat-model.md`](01-threat-model.md) —
  the threat model that drives the
  CC3 risk assessment.
- [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md) —
  the CC5.1 control activities.
- [`03-gdpr-compliance.md`](03-gdpr-compliance.md) —
  the Privacy (P) criterion.
- [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md) —
  the CC2.3 communication with
  external parties.
- [`../operations/04-incident-response.md`](../operations/04-incident-response.md) —
  the CC7.3 / CC7.4 controls.
- [`../architecture/`](../architecture/) —
  the system description input.
