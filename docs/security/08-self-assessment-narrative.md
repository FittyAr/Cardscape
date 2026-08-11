# 08 — Self-assessment narrative

> The auditor-facing summary. Read this in five
> minutes; it tells you which docs to open for
> which question. The project-side controls are
> described; the deployer-side controls (hosting,
> SIEM, key ring, backup, capacity) are the
> deployer's responsibility and are NOT described
> here.

## 1. What is Cardscape?

Cardscape is a self-hosted B2B-style kanban and
project-management tool. The model is the
maintainer ships the source; the deployer
(self-hosting org, or the maintainer's own hosted
service) ships the production environment. The
auditor audits the deployer's production
environment, not this repo.

The four product surfaces are:

1. **REST API** (`Cardscape.Api`) — the
   application boundary. JWT bearer, role-based
   authorization, problem-details errors.
2. **MCP server** (`Cardscape.Mcp`) — the
   Model Context Protocol surface for AI clients.
   API-token bearer.
3. **Web client** (`Cardscape.Web`) — Blazor
   WASM, talks to the API. No server-side
   session.
4. **Background workers** — an EF-backed queue claimed by
   the API's internal dispatcher and delivered through Wolverine;
   dedicated hosted services run retention and revocation sweeps.

## 2. Project-side vs. deployer-side controls

The deployer's auditor verifies **both** the
project-side and the deployer-side controls.
The split is the load-bearing contract: the
project ships what is reproducible from source;
the deployer ships what is environment-specific.

| Layer | Project ships | Deployer ships |
|---|---|---|
| Source code | ✅ | n/a |
| Default configuration (secure-by-default) | ✅ | can override via env-vars / `appsettings.Production.json` |
| Architecture tests (pin the secure-by-default rules) | ✅ | can add their own |
| EF Core migrations (the schema) | ✅ | owns the DB host, the backup, the encryption-at-rest |
| Log pipeline (Serilog, structured) | ✅ | owns the sink, the retention, the SIEM |
| Reverse proxy / TLS termination | not in repo | ✅ |
| HSTS / CSP / X-Content-Type-Options / Referrer-Policy | middleware ships | proxy enforces |
| Key ring (data protection; OAuth client secrets) | abstractions ship | owns the keys; can use their own KMS |
| Physical access / data centre | n/a | ✅ (via hosting provider's SOC 2) |
| SIEM rules / alert thresholds | defaults ship | tunes for their environment |
| Backup / DR | not in repo | ✅ |
| Capacity / autoscaling | not in repo | ✅ |
| Breach notification (Art. 33/34 GDPR) | template ships | owns the 72-hour clock |
| Privacy notice (public-facing) | template ships | fills in their org details |
| DPIA (Art. 35 GDPR) | trigger list ships | completes the DPIA for their jurisdiction |
| Third-party pen test | RFP template ships | commissions the firm; receives the report |

## 3. The five-minute auditor path

The auditor's first read is this document. The
auditor's second read is the matrix in
[`06-asvs-controls.md`](06-asvs-controls.md).
Then the SOC 2 Common Criteria mapping in
[`04-soc2-readiness.md`](04-soc2-readiness.md).
Then the GDPR posture in
[`03-gdpr-compliance.md`](03-gdpr-compliance.md)
and the Article 30 records in
[`07-gdpr-article-30.md`](07-gdpr-article-30.md).
Then the threat model in
[`01-threat-model.md`](01-threat-model.md). Then
the secure-coding checklist in
[`02-secure-coding-checklist.md`](02-secure-coding-checklist.md).
Then the templates in `templates/` for any
artefact the deployer needs to produce.

| Question the auditor is asking | Read this |
|---|---|
| "Does the project have a documented secure SDLC?" | [`01-threat-model.md`](01-threat-model.md) + [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md) |
| "What ASVS L1 controls are implemented, deferred, or operator-action?" | [`06-asvs-controls.md`](06-asvs-controls.md) |
| "What SOC 2 Common Criteria are covered?" | [`04-soc2-readiness.md`](04-soc2-readiness.md) |
| "What GDPR controls are in place?" | [`03-gdpr-compliance.md`](03-gdpr-compliance.md) + [`07-gdpr-article-30.md`](07-gdpr-article-30.md) |
| "What is the breach-notification workflow?" | [`templates/breach-notification.md`](templates/breach-notification.md) |
| "How is a vulnerability reported?" | [`05-vulnerability-disclosure.md`](05-vulnerability-disclosure.md) |
| "What does the deployer need to commission?" | [`templates/pen-test-rfp.md`](templates/pen-test-rfp.md) |
| "What's the privacy notice template?" | [`templates/privacy-notice.md`](templates/privacy-notice.md) |
| "What triggers a DPIA?" | [`templates/dpia.md`](templates/dpia.md) |
| "How is a DSR handled?" | [`templates/dsar-response.md`](templates/dsar-response.md) |

## 4. Posture summary

| Area | Status | Evidence in this repo |
|---|---|---|
| Source-code integrity | ✅ | Husky pre-commit (`dotnet format --verify-no-changes`), branch protection in the deployer's GitHub org, signed commits deferred to v3.0+ |
| Dependency hygiene | ✅ | Central Package Management (`Directory.Packages.props`), `dotnet list package --vulnerable --include-transitive` in CI |
| Authentication | ✅ | PBKDF2-SHA256 (100k iterations) password hashing; JWT (HS256, configurable 60-minute default with signed `exp`, no refresh bearer); self-service revocation (`POST /api/auth/revoke`); `JwtRevocationValidator` rejects revoked tokens on every request; `RevocationSweeper` purges expired revocations every 30 minutes |
| Authorization | ✅ | Role-based (`WorkspaceRole` Admin / Member / Observer, `BoardMember`); `IsAdmin` claim cached in JWT (no DB roundtrip on every admin request); `AdminOnlyPolicy` + `McpSubscriptionsAdminPolicy` gate the admin surface |
| Session management | ✅ | Bearer-only; no cookie session; `ClockSkew = 1 minute`; revocation endpoint exempt from the revocation validator for idempotency |
| Data protection | ✅ | Soft-delete + 30-day grace + automated `Anonymise`; `RetentionSweeper` purges; DSR export is a per-user zip (data.json + attachments); right-to-erasure is the same endpoint as admin delete |
| Cryptography | ✅ | HS256 (JWT), PBKDF2-SHA256 (passwords), ES256 (Apple client secrets), AES-256 (data-protection ring); `RandomNumberGenerator` for tokens |
| Transport security | partial | `UseHttpsRedirection` in the API; HSTS / CSP / etc. via the deployer's reverse proxy |
| Logging | ✅ | Serilog structured; `cardscape.security` logger for auth events; redaction filter for common secret patterns; no request bodies in logs |
| Monitoring / SIEM | partial | OpenTelemetry pipeline ships; sink is operator-action |
| Error handling | ✅ | `GlobalExceptionMiddleware` + ProblemDetails; no stack traces leaked |
| File upload | ✅ | `IStorageService` (S3-compatible in production); content-type allow-list + size limit + filename sanitisation; SSRF protection on outbound HTTP |
| WebSocket | ✅ | SignalR `/hubs/board` requires JWT bearer; rejects unauthenticated upgrades |
| Input validation | ✅ | FluentValidation on every command; value-objects (`EmailAddress`, `Password`, `DisplayName`) reject malformed input at the domain layer |
| API security | ✅ | All endpoints under `RequireAuthorization()`; documented in `docs/api/`; consistent status code conventions |
| Third-party code | ✅ | Central Package Management + lockfile + CI vulnerability scan; no copy-pasted third-party source |
| Memory safety | ✅ | C# / .NET 10 LTS (managed runtime); no `unsafe` code outside explicitly-marked places |
| Sub-resource integrity | partial | The repo ships the Blazor WASM assets; SRI is the deployer's CDN concern |
| WebAuthn / hardware MFA | ❌ | v3.0+; the v1.2.0 TOTP + recovery-code second factor is the entry-tier alternative |
| CAPTCHA | ❌ | v3.0+; `RateLimitMiddleware` covers the same surface for now |
| Signed commits | ❌ | v3.0+; the deployer's GitHub org controls this |
| Signed releases | ❌ | v3.0+; the SDK ships `.snupkg` symbols; full release signing is the deployer's supply-chain concern |

## 5. Known limitations the auditor should weigh

1. **Single maintainer project.** The threat
   model assumes a single maintainer
   (`Mavis`) with a small set of trusted
   contributors. The deployer's GitHub org
   controls branch protection; the project
   does not have a "many reviewers" defence
   in depth. The auditor weighs this against
   the deployer's own review process.

2. **v1.2.0 is pre-1.0.** The threat model
   assumes a hostile network and a
   semi-trusted operator. The auditor weighs
   the deployer's own hardening (firewall
   rules, network segmentation, WAF) on top.

3. **The OpenTelemetry pipeline is the
   default; the sink is operator-action.**
   The deployer wires the SIEM. The auditor
   verifies the deployer's SIEM, not the
   repo's `Serilog` config.

4. **The reverse proxy is the deployer's.**
   The repo ships the headers; the
   enforcement is the proxy. The auditor
   verifies the proxy.

5. **Encryption-at-rest is the deployer's
   DB / volume / bucket.** The repo ships
   the application-level encryption (the
   data-protection ring); the storage-tier
   encryption is the deployer's.

6. **`DatabaseLogSink` is a no-op in v1.2.0.**
   The log sink is wired but the actual
   database write is a TODO with a follow-up
   ADR (`0011-database-log-sink.md`)
   referenced from the source. The deployer
   should not rely on the DB log sink; the
   file + OpenTelemetry sinks are the
   production paths.

7. **Sub-processors are operator-action.**
   The repo ships no third-party service
   integrations by default. The deployer
   enumerates their own sub-processors and
   signs the DPAs.

8. **The DSR deletion contract is
   soft-delete + 30-day grace + 6-hour
   sweeper, then anonymise.** Hard-delete is
   deferred. The auditor verifies the
   sweeper cadence matches the deployer's
   documented policy.

## 6. What this document is NOT

- This document is not a SOC 2
  certification. The project does not
  self-certify; the auditor certifies.
- This document is not a pen-test report.
  The pen-test report comes from the firm
  the deployer commissions. The
  `templates/pen-test-rfp.md` is the
  request-for-proposal template the
  deployer sends to firms.
- This document is not a legal opinion.
  The GDPR templates are starting points;
  the deployer's legal counsel must review
  before publication.
- This document is not a privacy notice.
  The `templates/privacy-notice.md` is the
  template; the deployer fills in their
  organisation details.

## 7. Where to start

The auditor's first task is to read
[`04-soc2-readiness.md`](04-soc2-readiness.md)
for the SOC 2 framework mapping or
[`03-gdpr-compliance.md`](03-gdpr-compliance.md)
for the GDPR framework mapping, then
[`06-asvs-controls.md`](06-asvs-controls.md) for
the ASVS L1 line-by-line. The deployer's audit
window is shorter and cheaper than starting
from scratch because the project ships the
artefacts.
