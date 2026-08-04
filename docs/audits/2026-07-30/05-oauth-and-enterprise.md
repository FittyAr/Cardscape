# Audit 05 — OAuth & enterprise auth (v1.1.0 §3.11–3.12, §4.1–4.5)

**Date:** 2026-07-30
**Auditor:** general-purpose agent
**Scope:** `docs/roadmap/03-execution-plan-v1.1.0.md` §3.11 (OAuth 3rd-party apps), §3.12 (Public OpenAPI spec), §4.1 (OAuth 2.0 / OIDC login), §4.2 (SAML SSO), §4.3 (2FA / TOTP), §4.4 (SCIM provisioning), §4.5 (Data residency).
**Read-only:** source, tests, docs. Plan checkbox state inspected only.

---

## Plan checklist note

The plan file uses plain bullet points (`- …`), **not** task-list checkboxes (`- [ ]` / `- [x]`). `Select-String` for `^- \[ \]` and `^- \[x\]` returns zero matches. No checkbox updates are mechanically possible; this audit instead records the verdict per section and flags the file naming drift in §3.12. (See "Open follow-ups" at the end.)

---

## §3.11 — OAuth for third-party apps — **DONE**

### Domain
- `OAuthApp` aggregate (`src/Cardscape.Domain/Integrations/OAuthApps/OAuthApp.cs:12-42`) with `Register` factory enforcing name/clientId/secret/owner/redirect URI rules (`OAuthApp.cs:44-93`) and `Revoke` (`OAuthApp.cs:95-104`).
- Companion aggregates `OAuthAccessToken` and `OAuthAuthorizationCode` plus their ID types live in the same folder (`src/Cardscape.Domain/Integrations/OAuthApps/`).
- Errors folder present (`OAuthApps/Errors`).

### Application
- `IOAuthAppService` abstraction (`src/Cardscape.Application/Abstractions/Security/IOAuthAppService.cs`).
- `IOAuthAppRepository`, `IOAuthAccessTokenRepository`, `IOAuthAuthorizationCodeRepository` (`src/Cardscape.Application/Abstractions/Persistence/`).
- Command + query handlers: `src/Cardscape.Application/OAuth/Commands/OAuthAppCommands.cs:14-88` (`RegisterOAuthAppCommand`, `RevokeOAuthAppCommand`) and `src/Cardscape.Application/OAuth/Queries/OAuthAppQueries.cs:11-42` (`ListOAuthAppsForOwnerQuery`).

### Infrastructure
- EF Core repos: `OAuthAppRepository.cs`, `OAuthAccessTokenRepository.cs`, `OAuthAuthorizationCodeRepository.cs` (`src/Cardscape.Infrastructure/Repositories/`).

### API endpoints
Wired in `src/Cardscape.Api/Program.cs:201-202`:
- `MapOAuthAppEndpoints()` → `GET /api/oauth-apps`, `POST /api/oauth-apps`, `DELETE /api/oauth-apps/{id}` (`src/Cardscape.Api/Endpoints/OAuth/OAuthAppEndpoints.cs:22-59`).
- `MapOAuthFlowEndpoints()` → 4 protocol endpoints — `GET /oauth/authorize` (`OAuthFlowEndpoints.cs:47-108`), `POST /oauth/token` (`OAuthFlowEndpoints.cs:113-162`), `POST /oauth/revoke` (`OAuthFlowEndpoints.cs:167-187`, RFC 7009), `GET /oauth/userinfo` (`OAuthFlowEndpoints.cs:192-219`).

### Web UI
- `/settings/oauth-apps` page at `src/Cardscape.Web/Pages/SettingsOAuthApps.razor:1-218` — register form, secret reveal (one-time), list/revoke table, links to the flow doc.

### Documentation
- `docs/api/01-oauth-flow.md` (237 lines) covers registration, full handshake example, scopes (`cards.read`/`cards.write`/`boards.read`/`boards.write`/`comments.write`/`webhooks.read`/`webhooks.write`/`admin` at `01-oauth-flow.md:53-64`), error responses (`invalid_request`/`invalid_client`/`invalid_grant`/`unsupported_grant_type`/`invalid_scope` at `01-oauth-flow.md:215-221`), and what's NOT in v1.1.0 (refresh tokens, PKCE, device flow, dynamic client registration).

### Migration
- `IssueOAuthApps` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260730111053_IssueOAuthApps.cs`).

### Tests
- Integration: `tests/Cardscape.IntegrationTests/Endpoints/OAuthFlowTests.cs`.

### Notes
- Plan §3.11 also lists the MCP `ApiToken` as a grantable scope; the flow doc at `01-oauth-flow.md:66-69` mentions `api_token` as a grantable credential — the actual scope catalog in code should be cross-checked when the MCP server gains OAuth scope support.
- Refresh tokens are returned as `null` per the "What's NOT in v1.1.0" section; the endpoint does still emit a `refresh_token` key (`OAuthFlowEndpoints.cs:160`).

---

## §3.12 — Public OpenAPI spec — **DONE** (with filename drift)

### CI artifact
- `release` job in `.github/workflows/ci.yml:189-244`. Steps:
  - `dotnet build` of API (line 209).
  - Boots the API on `http://127.0.0.1:18080` and `curl`s `/swagger/v1/swagger.json` into `artifacts/openapi/openapi.json` (lines 215-232).
  - Uploads as `openapi-${github.ref_name}` artifact (lines 233-238).
- Trigger: any `v*` tag (job `if: startsWith(github.ref, 'refs/tags/v')` at line 191).

### Documentation
- `docs/api/02-openapi-spec.md` exists (120 lines). The audit G18 follow-up renamed the file from `01-` to `02-` to match the plan §3.12 layout. Content matches: where to find the spec, schema conventions, endpoint groups (including `/api/oauth/*`, `/api/scim/v2/*`, `/saml/{slug}/*` at lines 75-77), SDK generation, versioning, local dev.
- The flow doc and OpenAPI doc are both in `docs/api/`. `00-conventions.md` and `01-oauth-flow.md` are also present.

### Notes / drift
- The plan reference §3.12 specifies `docs/api/02-openapi-spec.md`. The actual file is now `docs/api/02-openapi-spec.md` (renamed in the G18 follow-up). The neighbouring `01-oauth-flow.md` is preserved as the OAuth-specific deep dive; `00-conventions.md` keeps the conventions slot.
- No `## 4.1+` test for the OpenAPI artifact itself; release tag is the gate. The release job's `if-no-files-found: warn` (line 238) means a missing spec only warns — could be hardened to `error`.

---

## §4.1 — OAuth 2.0 / OIDC login (Google, Microsoft, Apple) — **DONE**

### Packages
- `Directory.Packages.props:55-57`:
  - `Microsoft.AspNetCore.Authentication.Google` 11.0.0-preview.6
  - `Microsoft.AspNetCore.Authentication.MicrosoftAccount` 11.0.0-preview.6
  - `Microsoft.AspNetCore.Authentication.OpenIdConnect` 11.0.0-preview.6

### Auth provider registration
`src/Cardscape.Api/Extensions/ServiceCollectionExtensions.cs`:
- Google: `AddGoogle(...)` with `email`+`profile` scope (lines 139-145), gated on `Authentication:Google:ClientId` + `ClientSecret`.
- Microsoft: `AddMicrosoftAccount(...)` (lines 153-159), same gating.
- Apple: `AddOpenIdConnect(ExternalProvider.Apple.WireName(), ...)` (lines 181-208) with per-request `client_secret` regeneration via `IAppleClientSecretGenerator` (`OnRedirectToIdentityProvider` at lines 200-207). Gated on Apple:ClientId/TeamId/KeyId/PrivateKeyPem.

### Domain
- `src/Cardscape.Domain/Authentication/ExternalLogins/`: `ExternalLogin.cs`, `ExternalLoginId.cs`, `ExternalProvider.cs` (enum + WireName mapping), `SubjectId.cs`, errors, events.

### Application
- `src/Cardscape.Application/Authentication/ExternalLogins/ExternalLoginCommands.cs`.
- `IOAuthAppService` is OAuth-app-side; the external-login service shares Wolverine pattern via `ResolveExternalLoginCommand`.

### Infrastructure
- `src/Cardscape.Infrastructure/Repositories/ExternalLoginRepository.cs`.

### API endpoints
Wired in `src/Cardscape.Api/Program.cs:169` (`MapExternalLoginEndpoints()`):
- `GET /api/auth/external/{provider}/start` (`src/Cardscape.Api/Endpoints/Auth/ExternalLoginEndpoints.cs:55-103`) — `Results.Challenge` with the provider's `WireName`, round-trips `returnUrl` via state cookie.
- `GET /api/auth/external/{provider}/callback` (`ExternalLoginEndpoints.cs:105-186`) — pulls claims, resolves user via `ResolveExternalLoginCommand`, mints JWT, redirects to SPA with tokens in fragment.

### Web UI
- Login page buttons: `src/Cardscape.Web/Pages/Login.razor:19-33` (Google, Microsoft, Apple) using `/api/auth/external/{provider}/start?returnUrl=…`.
- Account linking: `src/Cardscape.Web/Pages/SettingsExternalLogins.razor:1-34` — links back to the same `/api/auth/external/{provider}/start` with `returnUrl=/settings/external-logins`.

### Migration
- `IssueExternalLogins` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260729205310_IssueExternalLogins.cs`).

### Notes
- Apple uses the OIDC handler with a placeholder `ClientSecret` replaced on every redirect (line 191) — matches Apple's "client_secret is a JWT signed with your private key" spec.
- Apple is gated by config — the `IsImplemented()` check on `ExternalProvider.Apple` keeps `/api/auth/external/apple/start` from the menu when unconfigured (referenced in the Auth flow code at `ExternalLoginEndpoints.cs:68-74`).

---

## §4.2 — SAML SSO — **DRIFT** (endpoints are stubs)

### Packages
- `Sustainsys.Saml2.AspNetCore` 2.10.0 in `Directory.Packages.props:82`. Good.

### Domain + Application + Infrastructure
- `src/Cardscape.Domain/Authentication/Saml/SamlConnection.cs` (aggregate).
- `src/Cardscape.Application/Saml/SamlConnectionCommands.cs` (`ConfigureSamlConnectionCommand`, `DisableSamlConnectionCommand`, `GetSamlConnectionQuery`).
- `src/Cardscape.Infrastructure/Repositories/SamlConnectionRepository.cs` — `FindByIdAsync` / `FindBySlugAsync` / `FindByWorkspaceAsync` / `AddAsync` (`SamlConnectionRepository.cs:10-23`).
- DI: `services.AddScoped<ISamlConnectionRepository, SamlConnectionRepository>()` (`src/Cardscape.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:277`).

### API endpoints
Wired in `src/Cardscape.Api/Program.cs:200` (`MapSamlEndpoints()`) — all three IdP-facing endpoints exist **as STUBS**:
- `GET /saml/{workspaceSlug}/login` (`src/Cardscape.Api/Endpoints/Saml/SamlEndpoints.cs:32-45`) — `Results.Challenge` to a `saml-{workspaceSlug}` scheme, but **no `AddSaml2(...)` call exists in the codebase** (confirmed by repo-wide grep — no hits in `src/Cardscape.Api/`).
- `GET /saml/{workspaceSlug}/login-init` (`SamlEndpoints.cs:47-64`) — returns `200 OK` with the JSON `{ info = "SAML AuthnRequest initiated", workspaceSlug, next = "POST /saml/{workspaceSlug}/acs (assertion consumer)" }`. The endpoint's own comment (line 53-57) says: *"A real implementation will wire the Sustainsys Saml2 handler in ServiceCollectionExtensions; this stub is a 200 OK that explains the wiring shape."*
- `POST /saml/{workspaceSlug}/acs` (`SamlEndpoints.cs:66-75`) — also a stub: `200 OK` with `{ info = "SAML ACS endpoint (assertion consumer service)" }`. No SAMLResponse parsing, no challenge handler. The comment (line 70-73) says: *"Sustainsys.Saml2 handles the request in the authentication pipeline; this minimal endpoint is a 200 OK that operators can hit with curl to confirm the route is mounted."*
- `GET /saml/{workspaceSlug}/metadata` (`SamlEndpoints.cs:77-96`) — returns a hand-written static `EntityDescriptor` XML (not generated by `Sustainsys.Saml2.Metadata` as the plan implies).
- Admin endpoints (authenticated, all real): `GET / POST / DELETE /api/workspaces/{workspaceId}/saml` (`SamlEndpoints.cs:99-133`) — calls `ConfigureSamlConnectionCommand`, `GetSamlConnectionQuery`, `DisableSamlConnectionCommand`.

### Web UI
- `/workspaces/{id}/saml` configuration page exists (`src/Cardscape.Web/Pages/WorkspaceSaml.razor:1-132`).

### Migration
- `IssueSamlConnections` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260730014409_IssueSamlConnections.cs`).

### Why DRIFT (not DONE)
- The DI comment at `InfrastructureServiceCollectionExtensions.cs:272-276` says: *"The Sustainsys.Saml2 handler is registered in the API layer (SamlAuthenticationHandler) when at least one workspace has a connection configured."* No such `SamlAuthenticationHandler.cs` file exists in the API (verified via `Get-ChildItem`).
- Repo-wide grep for `Sustainsys`/`Saml2` in `src/Cardscape.Api/` returns only the SamlEndpoints.cs comments.
- The `/saml/{slug}/login` call to `Results.Challenge` with scheme `saml-{workspaceSlug}` has no matching scheme registration, so the request will fail at the authentication handler layer (no-op challenge).
- Net result: the admin endpoints and metadata are functional; the **actual SAML protocol flow (AuthnRequest → IdP → ACS)** is not wired. An IdP that POSTs a signed `SAMLResponse` to `/acs` will get a `200 OK` with text and no user session.

### Required to close
1. Implement `SamlAuthenticationHandler` (challenge + sign-in handler for the `saml-{slug}` scheme) using `Sustainsys.Saml2.AspNetCore`.
2. On application startup (or on first connection), call `authBuilder.AddSaml2(...)` per-workspace with the configured IdP metadata.
3. Replace the `Results.Challenge` and `Results.Ok` stubs in `SamlEndpoints.cs:32-96` with the Sustainsys `Challenge`/`SignIn` middleware path or with explicit `Saml2AuthenticationHandler` invocations.
4. Have `/saml/{slug}/metadata` return the Sustainsys-generated metadata rather than the hand-rolled XML.

---

## §4.3 — 2FA / TOTP — **PARTIAL** (endpoints + UI done; login flow has no TOTP step)

### Packages
- `OtpNet` is in `Directory.Packages.props` (section comment at line 77; confirmed in `src/Cardscape.Infrastructure/Authentication/TotpService.cs:10` — `using OtpNet;`).

### Domain
- `src/Cardscape.Domain/Authentication/Totp/`: `TotpCredential.cs` (aggregate with `EncryptedSecret`, `RecoveryCodesHash`, `LastUsedCounter`), `TotpCredentialId.cs`, events, errors.

### Application
- `ITotpService` (`src/Cardscape.Application/Abstractions/Authentication/ITotpService.cs`).

### Infrastructure
- `TotpService.cs` uses `OtpNet`: `KeyGeneration.GenerateRandomKey(20)`, `Base32Encoding.ToString(...)`, `new Totp(...).VerifyTotp(..., out long matchedStep, VerificationWindow.RfcSpecifiedNetworkDelay)` (`src/Cardscape.Infrastructure/Authentication/TotpService.cs:40-115`). Counter replay protection at lines 117-120. Recovery codes hashed with SHA-256 at lines 233-237.
- `TotpCredentialRepository.cs` (`src/Cardscape.Infrastructure/Repositories/TotpCredentialRepository.cs`).

### API endpoints
Wired in `src/Cardscape.Api/Program.cs:191` (`MapTotpEndpoints()`):
- `GET /api/auth/2fa/status` (`src/Cardscape.Api/Endpoints/Auth/TotpEndpoints.cs:36-48`).
- `POST /api/auth/2fa/enroll` (lines 50-71) — returns `CredentialId`, cleartext `Secret`, `QrCodeUrl` (otpauth://), and 10 recovery codes.
- `POST /api/auth/2fa/verify` (lines 73-100) — accepts TOTP code or recovery code; returns `{ valid: true }`.
- `POST /api/auth/2fa/disable` (lines 102-120) — requires a valid code.

### Web UI
- `/settings/two-factor` page (`src/Cardscape.Web/Pages/SettingsTwoFactor.razor:1-164`) — enrol button, QR/secret display, recovery-code list, verify, disable.

### Migration
- `IssueTotpCredentials` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260729211156_IssueTotpCredentials.cs`).

### Why PARTIAL
- The plan §4.3 explicitly says: *"The login flow gains a 'TOTP code' step when the user has an active credential."*
- `src/Cardscape.Api/Endpoints/Auth/AuthEndpoints.cs:27-33` is a two-arg `/api/auth/login` (email + password) returning a `Result<AuthResponse>` with the JWT. No TOTP step.
- `src/Cardscape.Application/Authentication/Queries/LoginUserQuery.cs` was grepped for `totp|2fa|HasActive` — zero matches.
- Result: 2FA can be enrolled and verified out-of-band, but the user is **not** forced through a TOTP step on login.

### Required to close
1. Extend `LoginUserQuery` (or add `LoginStep2Query`) to return a "needs_totp" response when the user has an active `TotpCredential`.
2. Update `AuthEndpoints.cs:27` to handle the "needs_totp" branch and add a `POST /api/auth/login/totp` step that verifies the code and mints the JWT.
3. (Optionally) update the login page to surface the TOTP prompt when the API returns the partial-auth state.

---

## §4.4 — SCIM provisioning — **DRIFT** (Users done; Groups missing)

### Domain + Application + Infrastructure
- `src/Cardscape.Domain/Authentication/Scim/ScimToken.cs` (token aggregate).
- `src/Cardscape.Application/Scim/ScimTokenCommands.cs`.
- `src/Cardscape.Infrastructure/Scim/ScimService.cs` (default `IScimService`).
- `IScimService` interface (`src/Cardscape.Application/Abstractions/IScimService.cs:15-34`) — **only User methods**, no Group methods:
  - `CreateUserAsync`, `ListUsersAsync`, `GetUserAsync`, `ReplaceUserAsync`, `PatchUserAsync`, `DeleteUserAsync`.
- `ScimTokenRepository.cs` (`src/Cardscape.Infrastructure/Repositories/ScimTokenRepository.cs`).
- `ScimAuthenticationHandler` (`src/Cardscape.Api/Authentication/ScimAuthenticationHandler.cs`) — registered in `ServiceCollectionExtensions.cs:217-219` as a distinct auth scheme so the JWT/API-token selector above doesn't intercept SCIM bearer tokens.
- DI: `services.AddScoped<IScimTokenRepository, ScimTokenRepository>(); services.AddScoped<IScimService, ScimService>();` (`InfrastructureServiceCollectionExtensions.cs:269-270`).

### API endpoints — Users (DONE)
Wired in `src/Cardscape.Api/Program.cs:198-199`:
- `MapScimEndpoints()` — `src/Cardscape.Api/Endpoints/Scim/ScimEndpoints.cs`:
  - `GET /scim/v2/Users` (lines 29-59) — list, paged via `startIndex`/`count`/`filter`.
  - `POST /scim/v2/Users` (lines 61-82).
  - `GET /scim/v2/Users/{userId:guid}` (lines 84-97).
  - `PUT /scim/v2/Users/{userId:guid}` (lines 99-119).
  - `PATCH /scim/v2/Users/{userId:guid}` (lines 121-139).
  - `DELETE /scim/v2/Users/{userId:guid}` (lines 141-154).
- All correctly require the SCIM scheme (workspace id resolved from `HttpContext.Items["scim.workspaceId"]` at line 164).
- `MapScimAdminEndpoints()` — `src/Cardscape.Api/Endpoints/Scim/ScimAdminEndpoints.cs`: token list/issue/delete (`/api/workspaces/{id}/scim/tokens`).

### API endpoints — Groups (MISSING)
- Plan §4.4: *"SCIM v2 endpoints (`/scim/v2/Users`, `/scim/v2/Groups`)"*.
- The class comment of `ScimEndpoints.cs:11-12` advertises `+ /Groups` but **no `/Groups` endpoints are mapped anywhere** in the file or the codebase (verified by grepping for `MapGet("/Groups"`, `MapPost("/Groups"`, etc. — zero hits).
- `IScimService` interface (`IScimService.cs:15-34`) has no Group methods.
- Result: SCIM Users is functional; SCIM Groups is **not implemented**. IdPs that require group provisioning (Okta, Azure AD group push) will fail.

### Web UI
- `/workspaces/{id}/scim` page (`src/Cardscape.Web/Pages/WorkspaceScim.razor:1-127`) — token issue form + table.

### Migration
- `IssueScimTokens` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260730011402_IssueScimTokens.cs`).

### Tests
- Integration: `tests/Cardscape.IntegrationTests/Endpoints/ScimEndpointTests.cs`.

### Required to close
1. Add `IScimService` Group methods (`ListGroupsAsync`, `CreateGroupAsync`, `GetGroupAsync`, `ReplaceGroupAsync`, `DeleteGroupAsync`).
2. Map `MapGet/MapPost/MapPut/MapDelete` for `/scim/v2/Groups` (and `/scim/v2/Groups/{id}`).
3. Map `WorkspaceMember` to a SCIM `Group` shape; consider whether groups are board-equivalents or a separate aggregate.

---

## §4.5 — Data residency — **PARTIAL** (domain + migration + UI done; gating never enforced)

### Region enum
- `src/Cardscape.Domain/Workspaces/Region.cs:11-29` — `Unspecified = 0`, `Europe = 1`, `NorthAmerica = 2`, `AsiaPacific = 3`, `SouthAmerica = 4`. Matches plan §4.5.

### Workspace aggregate
- `src/Cardscape.Domain/Workspaces/Workspace.cs:25` — `public Region Region { get; private set; } = Region.Unspecified;`.
- `SetRegion(newRegion, actingUserId, at)` (`Workspace.cs:165-180`) — emits `WorkspaceRegionChanged`. Blocks changes after first save via `CannotChangeRegion` error (`Errors/WorkspaceErrors.cs:36`).
- `GuardRegion(deploymentRegion)` (`Workspace.cs:195-211`) — returns `Result.Failure(RegionMismatch)` when workspace region != deployment region (and neither is Unspecified). The error is defined at `Errors/WorkspaceErrors.cs:29`.

### IDeploymentRegion
- `IDeploymentRegion` abstraction (`src/Cardscape.Application/Abstractions/IDeploymentRegion.cs`).
- `ConfigurationDeploymentRegion` (`src/Cardscape.Infrastructure/Configuration/ConfigurationDeploymentRegion.cs:15-30`) — reads `Cardscape:Deployment:Region` from `IConfiguration`.
- DI: `services.AddSingleton<IDeploymentRegion, ConfigurationDeploymentRegion>()` (`InfrastructureServiceCollectionExtensions.cs:255`).

### Migration
- `IssueWorkspaceRegion` (`src/Cardscape.Infrastructure/Persistence/Migrations/20260730003819_IssueWorkspaceRegion.cs:11-24`) — adds `Region` int column to `workspaces` (default `0`/`Unspecified`) and an index on `Region`.

### Web UI
- Region selector at workspace creation: `src/Cardscape.Web/Pages/Workspaces.razor:27-30` — `RadzenDropDown` of `RegionOption(Value, Label)` (lines 86-93), bound to `createModel.Region`. Sent to the API as `region` (int, nullable) at `Workspaces.razor:111-112`.
- Region badge on each workspace card: `Workspaces.razor:61-64` (`@if (ws.Region != 0)`).
- i18n: `SharedResource.resx:93-94` and `SharedResource.es.resx:93-94` have `WorkspacesRegion` + `WorkspacesRegionPlaceholder` translations.

### Why PARTIAL
- The plan §4.5: *"When the deployment is configured with a region, the API rejects cross-region writes (a workspace in `Europe` cannot accept uploads to a `NorthAmerica` storage backend). The check lives in `Workspace.GuardRegion(region)`."*
- Repo-wide grep for `GuardRegion` in `src/Cardscape.Api/` returns **zero hits**. No endpoint, middleware, or background job calls `GuardRegion`.
- `IDeploymentRegion` is registered in DI but never injected anywhere (confirmed by grepping `src/Cardscape.Api/` for `IDeploymentRegion` — no matches in endpoints/middleware).
- Net result: the Region column is set, stored, and displayed, but **no write path is gated**. Cross-region writes are silently accepted.

### Required to close
1. Inject `IDeploymentRegion` into the endpoints that mutate workspace-scoped data (board/card/list writes, attachment uploads) — or, more pragmatically, into middleware that resolves the workspace and calls `workspace.GuardRegion(deploymentRegion.Current)`.
2. Wire a failure path that returns 409 Conflict with `RegionMismatch` when the check fails.
3. Add an integration test that creates a workspace in `Europe` against a deployment configured with `Cardscape:Deployment:Region=NorthAmerica` and asserts the write is rejected.

---

## Summary table

| § | Item | Verdict | Notes |
|---|---|---|---|
| 3.11 | OAuth 3rd-party apps | **DONE** | Domain, app, infra, 4 flow endpoints + 3 admin endpoints, Web UI, doc, tests, migration all present. |
| 3.12 | Public OpenAPI spec | **DONE** | CI release job publishes the spec. Doc exists as `docs/api/02-openapi-spec.md` (filename matches plan §3.12 after the G18 follow-up). |
| 4.1 | OAuth 2.0 / OIDC login | **DONE** | Google/Microsoft/Apple handlers, ExternalLogin aggregate, admin endpoints, login + linking UI, migration. |
| 4.2 | SAML SSO | **DRIFT** | Domain/repos/admin endpoints present, but the `/saml/{slug}/login`, `/login-init`, `/acs`, `/metadata` endpoints are 200-OK stubs. `Sustainsys.Saml2` is **not** registered in the auth pipeline; no `SamlAuthenticationHandler.cs` file exists. |
| 4.3 | 2FA / TOTP | **PARTIAL** | Endpoints + Web UI + OtpNet usage all in. **`/api/auth/login` has no TOTP step** — the `LoginUserQuery` does not branch on a TOTP credential. |
| 4.4 | SCIM provisioning | **DRIFT** | Users (GET/POST/GET-id/PUT/PATCH/DELETE) + admin token endpoints in. **`/scim/v2/Groups` is missing entirely** — no IScimService method, no endpoint mapping. |
| 4.5 | Data residency | **PARTIAL** | Region enum, `Workspace.Region`/`SetRegion`/`GuardRegion`, migration, UI selector all in. **`GuardRegion` is never called from any API endpoint**; `IDeploymentRegion` is registered but unused. |

### What was checked off
- **Plan file uses plain bullets, not checkboxes.** `Select-String` for `^- \[ \]` and `^- \[x\]` in `docs/roadmap/03-execution-plan-v1.1.0.md` returns zero matches. No `- [ ]` → `- [x]` edit is mechanically possible; the audit instead records the verdict per section in this report.

### Most important gap
**§4.2 SAML SSO** is the most consequential gap. The endpoints and admin UI exist, but the protocol itself is stubbed — IdPs that POST a `SAMLResponse` to `/saml/{slug}/acs` get a JSON 200 OK and no user session. The `Sustainsys.Saml2.AspNetCore` package is installed but never called; there is no `SamlAuthenticationHandler.cs` in the API. To close, the handler needs to be implemented and the stub endpoints need to forward to the Sustainsys challenge/ACS middleware path.
