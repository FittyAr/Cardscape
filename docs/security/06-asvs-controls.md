# 06 — OWASP ASVS v4.0.3 Level 1 control coverage

> The project's coverage of the [OWASP Application
> Security Verification Standard v4.0.3 Level 1
> requirements](https://owasp.org/www-project-application-security-verification-standard/).
> Level 1 is the entry tier — the bar every mature
> SaaS deployer should clear before a third-party
> pen test. The pen test RFP template
> (`docs/security/templates/pen-test-rfp.md`) and
> the compliance evidence bundle
> (`scripts/compliance-export.ps1`) hand the
> deployer's auditor this matrix as the starting
> point. The auditor verifies the deployer's
> production deployment, not this repo.

---

## Legend

- **Implemented** — the control is in the repo, with
  tests, and the deployer ships it as-is.
- **Operator-action** — the control is partially
  implemented but the deployer must enable /
  configure it at deploy time (e.g. enable a
  third-party email provider, point the CDN at
  the right origin). The repo ships the wiring;
  the deployer closes the loop.
- **Out of scope (v1.x)** — the control is
  documented but not implemented in v1.2.0. The
  v3.0+ pen test brings it online.

---

## V1 — Architecture

| Section | Control | Status | Evidence |
|---|---|---|---|
| V1.1 | Secure SDLC | Implemented | `docs/development/00-onboarding.md` (test policy), `docs/security/02-secure-coding-checklist.md` (pre-merge checklist), `.github/workflows/ci.yml` (build + test + coverage gates), Conventional Commits via Husky (`CONTRIBUTING.md`). |
| V1.2 | Architectural documentation | Implemented | `docs/architecture/00-overview.md`, `docs/architecture/01-bounded-contexts.md`, ADRs 0001-0010. |
| V1.3 | Dependency inventory | Implemented | `Directory.Packages.props` (Central Package Management), `dotnet list package --vulnerable --include-transitive` in CI, `scripts/compliance-export.ps1` step 3. |
| V1.4 | Source-control integrity | Implemented | Branch protection rules in the deployer's GitHub org; signed commits out of scope (v3.0+). |
| V1.5 | Memory-safe languages | Implemented | C# / .NET 10 LTS (managed runtime). |
| V1.6 | Compiler-level mitigations | Implemented | `Directory.Build.props` `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, `dotnet format --verify-no-changes` Husky pre-commit. |
| V1.7 | No unsafe defaults | Implemented | JWT signing key from env-var (not `appsettings.json`), `JwtOptions.AccessTokenMinutes = 60` (cap), no default DB credentials. |
| V1.8 | Subresource integrity | Operator-action | The Web client is served by the API; SRI is the deployer's CDN concern. |
| V1.9 | HTTP security headers | Implemented | `docs/security/02-secure-coding-checklist.md` §"Response headers" + the deployer's reverse proxy is expected to set `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy`. |
| V1.10 | Subdomain independence | Operator-action | The deployer controls DNS; the repo does not. |
| V1.11 | Principle of least privilege | Implemented | `IsAdmin` claim cached in JWT, no DB roundtrip on every admin request; role-based access checks in endpoint filters (see `src/Cardscape.Api/Endpoints/Admin/`). |

## V2 — Authentication

| Section | Control | Status | Evidence |
|---|---|---|---|
| V2.1 | Password security | Implemented | `Pbkdf2PasswordHasher` (PBKDF2-SHA256, 100k iterations), `PasswordComplexity` value object (length + character class), `IUserDataExportService` + `User.Anonymise` (Art. 17 GDPR). Breached-password list in `Cardscape.SecurityTests`. |
| V2.2 | General authenticator security | Implemented | Argon2-style PBKDF2 + per-user salt; constant-time comparison. |
| V2.3 | Authenticator lifecycle | Implemented | `User.SetRestricted` (write block) + `User.SoftDelete` + `User.Anonymise` (PII replacement); `IsDeleted` + `IsAnonymised` + `IsRestricted` flags. |
| V2.4 | Credential storage | Implemented | Hashed + salted (PBKDF2); no plaintext on disk; `Password` is a value object that never logs itself. |
| V2.5 | Credential recovery | Implemented | Self-service reset via emailed link (one-time token, 1-hour expiry) — `PasswordResetToken`. |
| V2.6 | Look-up secret verifier | Implemented | Per-user salt + timing-safe compare. |
| V2.7 | Out-of-band verifier | Implemented | Email-based reset link. |
| V2.8 | Single or multi-factor one-time verifiers | Implemented | TOTP second factor (`TotpService`, `TotpEndpoints`); recovery codes. |
| V2.9 | Cryptographic software and devices | Out of scope (v1.x) | Hardware MFA / WebAuthn is a v3.0+ workstream. |
| V2.10 | Service authentication | Implemented | API tokens (`ApiToken`), OAuth client credentials (`OAuthApp` + `OAuthAccessToken`), SCIM bearer (`ScimToken`), SAML bearer (`SamlAuthenticationHandler`). |

## V3 — Session management

| Section | Control | Status | Evidence |
|---|---|---|---|
| V3.1 | Session management foundation | Implemented | JWT access tokens use a configurable 60-minute default TTL; no fictitious refresh session is exposed. `JwtRevocationValidator` rejects revoked tokens and `RevocationSweeper` purges expired rows. |
| V3.2 | Session binding | Implemented | Bearer tokens over TLS; `Authorization: Bearer` header is the only transport. |
| V3.3 | Session termination | Implemented | `POST /api/auth/revoke` self-service; `DeleteApiToken` admin; `User.SoftDelete` kills all sessions. |
| V3.4 | Cookie-based session management | N/A | The API is bearer-only. The Web client uses Blazor WASM; no server-side session. |
| V3.5 | Token-based session management | Implemented | JWT signed with HS256, issuer + audience + lifetime + signature validated; `ClockSkew = 1 minute`. |
| V3.6 | Federated re-authentication | Implemented | External login (Google, Microsoft, Apple, SAML, SCIM) requires the IdP session to still be valid; `OnRedirectToIdentityProvider` re-issues the challenge on token expiry. |
| V3.7 | Defences against session abuse | Implemented | `RateLimitMiddleware` per-IP throttling and admin-only paths. Cached `is_admin` authorization fails closed when the mandatory claim is absent; strict mode reads the live database. |

## V4 — Access control

| Section | Control | Status | Evidence |
|---|---|---|---|
| V4.1 | General access control design | Implemented | Role-based: `WorkspaceRole` (Admin / Member / Observer), `BoardMember`, `IsAdmin`; `RequireAuthorization()` on every authenticated endpoint; ownership checks inside the Application-layer command handlers. |
| V4.2 | Operation level access control | Implemented | Every command handler reads `ICurrentUser` and refuses on `Id == null`. |
| V4.3 | Other access control considerations | Implemented | Data export (`UserDataExportService`) returns a zip of the user's own data only; admin DSR endpoints (`UserDsrAdminEndpoints`) are gated by `AdminOnlyPolicy` + `McpSubscriptionsAdminPolicy`. |

## V5 — Validation, sanitisation, encoding

| Section | Control | Status | Evidence |
|---|---|---|---|
| V5.1 | Input validation | Implemented | FluentValidation on every command (`RegisterUserCommand`, `CreateWorkspaceCommand`, `CreateCardCommand`, etc.); `IsValid()` value objects for `EmailAddress`, `Password`, `DisplayName`, etc. |
| V5.2 | Sanitisation and sandboxing | Implemented | `HtmlSanitizer` on user-supplied description / comment bodies; CSP `script-src 'self'` enforced by the deployer's reverse proxy. |
| V5.3 | Output encoding | Implemented | Minimal-API serialises to JSON via `System.Text.Json` with default web defaults; Blazor WASM uses `@()` to HTML-encode by default. |
| V5.4 | Memory, string, and untrusted code | Implemented | C# / .NET 10 — managed runtime, no unsafe code outside the explicitly-`unsafe`-marked places. |

## V6 — Stored cryptography

| Section | Control | Status | Evidence |
|---|---|---|---|
| V6.1 | Data classification | Implemented | `docs/security/03-gdpr-compliance.md` records the PII fields and their treatment. |
| V6.2 | Algorithms | Implemented | HS256 for JWT, PBKDF2-SHA256 for passwords, ES256 for Apple client secrets; AES-256 for the data-protection ring. |
| V6.3 | Random values | Implemented | `RandomNumberGenerator` for tokens; `Guid.NewGuid()` for ids. |
| V6.4 | Secret management | Implemented | `IConfiguration["Jwt:SigningKey"]` (env-var in production); `DataProtectionSecretProtector` for TOTP secrets; ASP.NET Core Data Protection ring for app secrets. |

## V7 — Error handling and logging

| Section | Control | Status | Evidence |
|---|---|---|---|
| V7.1 | Log content | Implemented | Serilog structured logging; `cardscape.security` logger name for auth events; `ILogger.LogInformation` / `LogWarning` / `LogError` only — no `LogTrace` in production. |
| V7.2 | Log processing | Operator-action | The repo ships the log pipeline; the deployer ships the log sink. SOC 2 requires the deployer ship a SIEM / log aggregator with retention. |
| V7.3 | Log protection | Operator-action | The repo redacts common secret patterns in `docs/security/02-secure-coding-checklist.md`; the deployer controls the storage tier. |
| V7.4 | Error handling | Implemented | `GlobalExceptionMiddleware`; ProblemDetails responses; `CardscapeApiException` on the SDK; no stack traces leaked. |

## V8 — Data protection

| Section | Control | Status | Evidence |
|---|---|---|---|
| V8.1 | General data protection | Implemented | DSR endpoints (`UserDsrAdminEndpoints`): export, soft-delete, anonymise. Retention sweeper (`RetentionSweeper`) purges soft-deleted users past 30-day grace + 6-hour sweep cadence. |
| V8.2 | Client-side data protection | Implemented | Blazor WASM; no PII at rest on the browser beyond the access token; explicit `localStorage` opt-in for the culture picker only. |
| V8.3 | Sensitive private information | Implemented | `User.Anonymise` replaces PII with placeholders; TOTP secrets encrypted at rest; webhook delivery payloads hashed + stored. |

## V9 — Communication

| Section | Control | Status | Evidence |
|---|---|---|---|
| V9.1 | Client-server communication security | Implemented | HTTPS-only via `UseHttpsRedirection`; HSTS via the deployer's reverse proxy; TLS 1.2+ (enforced by the deployer's proxy). |
| V9.2 | Server communication security | Implemented | API-to-MCP, API-to-Slack, API-to-Google, API-to-webhook channels are all over HTTPS with bearer / OAuth credentials. |

## V10 — Malicious code

| Section | Control | Status | Evidence |
|---|---|---|---|
| V10.1 | Code integrity | Implemented | Branch protection + required reviews + CI gates; signed releases out of scope (v3.0+). |
| V10.2 | Malicious code search | Implemented | `dotnet format --verify-no-changes` Husky pre-commit; CI runs `dotnet list package --vulnerable --include-transitive`. |
| V10.3 | Application integrity | Implemented | The repo is reproducible from source + tag; the deployer runs `git rev-parse HEAD` in the deploy log. |
| V10.4 | Third-party code integrity | Implemented | Central Package Management + lockfile + `dotnet list package --vulnerable` in CI. |

## V11 — Business logic

| Section | Control | Status | Evidence |
|---|---|---|---|
| V11.1 | Business logic security | Implemented | Domain invariants enforced by aggregate factories (`Card.Create` rejects empty title, `EmailAddress.Create` rejects malformed addresses, `RegionGuard` rejects cross-region writes); 525+ unit tests + 147 integration tests. |
| V11.2 | Anti-automation | Implemented | `RateLimitMiddleware` (per-IP), `RateLimitOptions` (per-API-token), CAPTCHA out of scope (v3.0+). |

## V12 — Files and resources

| Section | Control | Status | Evidence |
|---|---|---|---|
| V12.1 | File upload | Implemented | Attachment uploads via `IStorageService` (local filesystem in dev; the deployer wires S3-compatible); `IsAllowedContentType` + size limit + filename sanitisation. |
| V12.2 | File integrity | Operator-action | The repo ships hash-on-write; the deployer wires immutable storage. |
| V12.3 | File execution prevention | Implemented | `IStorageService` writes under a per-tenant prefix with no execute permission; CSP blocks `script-src` outside `'self'`. |
| V12.4 | File storage | Implemented | The local storage implementation lives in `src/Cardscape.Infrastructure/Storage/LocalFileStorageService.cs`; the deployer swaps the implementation for S3 / Azure Blob / GCS at deploy time via the same `IStorageService` interface. |
| V12.5 | File download | Implemented | `IExportService` produces a per-board zip; the URL is the API + bearer; no public hotlink. |
| V12.6 | SSRF protection | Implemented | Outbound HTTP clients validate the resolved host is not RFC 1918 / link-local; webhook URLs are validated at create-time. |

## V13 — API and web service

| Section | Control | Status | Evidence |
|---|---|---|---|
| V13.1 | Generic web service security | Implemented | All REST endpoints are documented in `docs/api/`; CSRF protection via `RequireAuthorization()` + bearer token; no cookie-based session. |
| V13.2 | RESTful web service | Implemented | Minimal API endpoints follow REST conventions; the matrix in `docs/api/00-conventions.md` lists status code conventions. |
| V13.3 | SOAP web service | N/A | The project is REST-only. |
| V13.4 | GraphQL | N/A | The project is REST-only. |
| V13.5 | WebSocket | Implemented | SignalR `/hubs/board` requires JWT bearer (same as REST); `BoardHub` rejects unauthenticated upgrades. |

## V14 — Configuration

| Section | Control | Status | Evidence |
|---|---|---|---|
| V14.1 | Build and deploy | Implemented | `Directory.Build.props` + `Directory.Packages.props` (Central Package Management); `dotnet format --verify-no-changes` Husky pre-commit; CI release job on tag. |
| V14.2 | Dependency | Implemented | `Directory.Packages.props` pins every package; `dotnet list package --vulnerable --include-transitive` in CI. |
| V14.3 | Unintended security disclosure | Implemented | ProblemDetails responses; stack traces never leaked; `GlobalExceptionMiddleware` redacts. |
| V14.4 | HTTP security header verification | Operator-action | The repo ships a CSP / HSTS / etc. checklist; the deployer's reverse proxy enforces it. |
| V14.5 | Validate HTTP request header | Implemented | `JwtBearerEvents` validates issuer + audience + lifetime + signature; `UseAuthentication` runs before any user code. |

---

## Operator-action recap (deployer must enable / configure)

| # | Control | Deployer task |
|---|---|---|
| V1.8 | SRI | Configure CDN to set `integrity` for the Blazor WASM assets. |
| V1.10 | DNS subdomains | Provision subdomains; never share a wildcard cert. |
| V4.x | Reverse proxy | Set `Strict-Transport-Security`, `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy`, `X-Frame-Options`. |
| V7.2 | Log sink | Provision a SIEM / log aggregator with the retention the auditor's policy requires. |
| V7.3 | Log storage tier | Choose a storage tier with at-rest encryption. |
| V8.x | Storage | Wire `IStorageService` to S3 / Azure Blob / GCS with bucket-level encryption. |
| V12.2 | File integrity | Enable immutable storage for the deployment artefacts. |
| V14.4 | Header verification | Run the deployer-side compliance scanner; the auditor will re-run it. |

## Out of scope for v1.2.0 (the pen test brings these online)

| # | Control | Why deferred |
|---|---|---|
| V1.4 | Signed commits | v3.0+; the deployer's GitHub org controls branch protection. |
| V2.9 | WebAuthn / hardware MFA | v3.0+; out of the v1.2.0 scope. |
| V11.2 | CAPTCHA | v3.0+; rate-limit middleware covers the same surface. |
| V14.x | Signed releases | v3.0+; the deployer's supply-chain team owns this. |

---

## What the auditor verifies

The auditor does NOT verify this repo. The
auditor verifies the deployer's production
deployment. The deployer hands the auditor:

1. The compliance evidence bundle produced by
   `scripts/compliance-export.ps1`.
2. The deployer-specific overrides documented in
   `docs/operations/03-monitoring.md` and
   `docs/operations/04-compliance.md` (the
   operator-action recap above).
3. Access to a staging environment that mirrors
   production.

This document is the contract: every "Implemented"
row above corresponds to a piece of evidence the
deployer can show the auditor.
