# Trello Enterprise parity — what's worth building, what's not

> The maintainer's take on the **Trello Enterprise** feature
> surface, mapped feature-by-feature against Cardscape as it
> stands at v1.2.0-pending (and as it is likely to stand for
> the next two minor versions). The aim is to answer three
> questions, in order:
>
> 1. **What is the value of this feature for a self-hostable
>    open-source kanban that is mostly deployed by individuals,
>    small teams, and small-and-medium companies?** (Trello's
>    enterprise buyers are Fortune-500; ours are not.)
> 2. **What is the build cost?** (Solo maintainer, no full-time
>    team, no security auditors on retainer.)
> 3. **Where in the v1.x / v2.x roadmap does it actually land?**
>
> The hard call: most of the Trello Enterprise surface is
> **"checklist, not tomorrow"** for Cardscape. This doc records
> why, feature by feature, so the call is traceable and not
> relitigated every quarter.

---

## 1. The principle

Trello Enterprise is sold to **organisations that already have
a security team, a compliance officer, a SOC 2 audit, and a
budget for a vendor relationship**. The features in that tier
exist to make Trello **fit into** a regulated environment
without making the environment more annoying for the people
in it.

Cardscape's primary buyer is the **self-hoster**: a developer
or a small team that runs the binary on their own hardware
and trusts themselves. The "enterprise" axis of value is
secondary — and on that axis, the marginal buyer is the
**small company (10–100 people) that wants SSO and an audit
log** without paying Atlassian $20/user/month for the
privilege. That buyer is real. The Fortune-500 buyer is not
the target and will not be the target for the foreseeable
future.

So the doc partitions the Trello Enterprise surface into
three buckets:

- **A — Worth building.** Real value to the self-hoster and
  the small-company buyer. Maps to a roadmap item with a
  real deadline.
- **B — Worth the design, not the code yet.** Architecturally
  sound, fits the model, but the build cost is more than the
  current maintainer bandwidth can absorb. Stays as a
  known-known.
- **C — Checklist, not for tomorrow.** Looks good in a
  "we are feature-parity with Trello" table, but the
  implementation cost is high, the user benefit is marginal
  for the actual buyer, and the maintenance burden (audit
  cycles, vendor updates, third-party library churn) is
  constant. Recorded so we can point to this doc when the
  question comes up.

The categorisation is opinionated and dated. Re-evaluate at
each minor version bump.

---

## 2. The mapping

The Trello Enterprise surface is grouped by the **user
problem** it solves, not by the **Trello pricing page**,
because the pricing page bundles features in arbitrary ways
that don't match the build cost. The groups are:

1. **Authentication and identity** — getting the right
   humans in, keeping the wrong humans out.
2. **Provisioning and lifecycle** — when humans join, leave,
   or change teams.
3. **Governance and policy** — central rules that apply to
   every workspace in the org.
4. **Visibility and reporting** — knowing what is going on
   (audit, telemetry, exports).
5. **Data sovereignty** — where the bytes live and who can
   read them.

Each feature is rated **A / B / C** and given a short
rationale and a "where in the roadmap" line.

### 2.1 Authentication and identity

| Feature | Verdict | Where | Rationale |
|---|---|---|---|
| **SAML SSO** (Sustainsys.Saml2) | **A** | v1.2.0 → v1.3.0 | Real demand from the small-company buyer. Domain (`/saml/{slug}/*`) and the Sustainsys wiring are mostly in place; the four protocol endpoints are 200-OK stubs today (audit `docs/audits/2026-07-30/05-oauth-and-enterprise.md` §4.2). Close the gap: implement the SAML handler, hook the `/saml/{slug}/acs` and `/saml/{slug}/login-init` paths through Sustainsys, add a real integration test against a fixture IdP (e.g. simplesamlphp in docker-compose). |
| **OIDC SSO** (Google, Microsoft, Apple — already shipped as login, **not** as IdP) | **A** | v1.2.0 | The current OIDC login handlers do **external** IdP login (Google as the IdP, Cardscape as the SP). The enterprise need is the inverse: Cardscape as the IdP, a third-party SaaS as the SP. Real demand is lower than SAML (most enterprise stacks use SAML), so the SAML stub → real path is the priority; OIDC-as-IdP is a follow-up if any user asks for it. |
| **2FA enforcement** (admin can require 2FA) | **A** | v1.2.0 | TOTP is already in (`02-` audit §4.3) but enforcement is missing — there is no `RequireTwoFactor` policy on the workspace or org level. The policy itself is a few lines on top of `LoginUserQuery`; the audit log entry ("user X enabled 2FA" / "admin Y enforced 2FA on workspace Z") is the harder half. Worth the small cost. |
| **Domain capture / auto-claim** | **C** | — | Trello uses this to lock an organisation to a domain (anyone with `@acme.com` on Trello auto-joins Acme's enterprise). Useful for **multi-tenant SaaS**, useless for a self-hoster (the self-hoster IS the only tenant). A single-user OSS deployment does not have multiple companies to keep separate. Skip. |
| **Passwordless / passkey login** | **B** | v1.3.0+ | Real demand (developers love passkeys; small companies adopt them fast). .NET 10 has first-class WebAuthn support. The implementation is non-trivial — credential storage, attestation, backup flows, device sync — but it lands cleanly on the existing `IUserCredentialService` surface. Track for a v1.3.0 minor. |
| **Atlassian Access-style policy aggregation** | **C** | — | Bundling multiple identity providers under a single "Atlassian Access" control plane is a **vendor** feature, not a product feature. The open-source equivalent is "run a Keycloak / Authentik in front", which is what self-hosters already do. Building it is duplicating the work of the IdP ecosystem. Skip. |

### 2.2 Provisioning and lifecycle

| Feature | Verdict | Where | Rationale |
|---|---|---|---|
| **SCIM Users (CRUD)** | **A** | v1.2.0 | The endpoints are in (`05-` audit §4.4) and the test coverage is solid. Real demand: every IdP that can sync users wants this, and Cardscape's small-company buyer already has an IdP. Ship the user endpoints (already done) and add **groups** next. |
| **SCIM Groups** | **A** | v1.2.0 | **MISSING** today — `05-` audit §4.4 lists the gap explicitly. Mapping `WorkspaceMember` to a SCIM `Group` is the right shape; the design call is whether groups are workspace-equivalents (one per workspace) or a separate aggregate (a workspace can be in many groups). Recommendation: **separate aggregate**, with `WorkspaceMemberGroupMembership` as a join. Allows the same pattern that Okta/Entra users expect. |
| **Just-in-time provisioning** (auto-create a user on first SSO login) | **A** | v1.2.0 | This is a 20-line check in the SAML/OIDC callback handler: "if the subject is unknown, create a user from the claims". Should ship together with the SAML closure and the SCIM Groups work. Without JIT, the SAML path forces the operator to pre-create every user — which is a non-starter for a 50-person company. |
| **De-provisioning on SCIM delete / SAML logout propagation** | **B** | v1.3.0+ | SCIM has a `DELETE /Users/{id}` path; the proper handling is **disable** (not delete) the local user, keep their authored content readable, mark the account as orphaned, and let a retention job anonymise after 30 days (consistent with the GDPR soft-delete behaviour in `01-` audit §2). The pattern is clear but the retention-job plumbing is the costly part. |
| **Multi-org / multi-workspace consolidation** | **C** | — | Trello lets an enterprise roll up 20+ workspaces into a single billing/admin view. The self-hoster equivalent is "I have one Cardscape instance, why do I need a multi-org layer?" The self-hosted version of the problem is solved by `Workspace` already. Skip. |

### 2.3 Governance and policy

| Feature | Verdict | Where | Rationale |
|---|---|---|---|
| **Workspace-level roles** (Admin / Member / Observer) | **A (done)** | shipped | `WorkspaceRole` enum (`Domain/Workspaces/WorkspaceRole.cs`), enforced in handlers, surfaced in the Web UI. |
| **Board-level role overrides** | **B** | v1.3.0+ | Trello has `Admin` / `Editor` / `Observer` per board. Cardscape inherits role from the workspace today. The override is a small DTO and a handler change, but the UI surface is non-trivial (every member-list in every board needs a per-row role chip). Track for v1.3.0. |
| **Org-wide security policies** (2FA enforcement — see 2.1) | **A** | v1.2.0 | Same item as 2FA enforcement above. Listed here for completeness. |
| **Default board visibility** (org default is "private") | **A** | v1.2.0 | Tiny config knob, real value (a misclick on "public" leaks the board; setting the default to private makes the safe option the default). 5 lines in the board-create handler. |
| **Attachment restrictions** (block executables, etc.) | **B** | v1.3.0+ | A real ask in regulated environments. The infrastructure is in place (`IStorageService` + the upload endpoint), so the filter is a `MimeType` blacklist in the handler. The cost is mostly maintaining the blacklist (new vectors, new formats). Defer until someone asks. |
| **Idle session timeout** | **A** | v1.2.0 | The auth surface already has JWT revocation (`01-` audit §S1). An "expire after 24h of inactivity" knob is a 10-line addition on the request pipeline. Real value for shared workstations in small offices. |
| **Email restrictions** (block personal email signups) | **B** | v1.3.0+ | An admin wants to forbid `gmail.com` / `yahoo.com` for the org. Implementation is a regex / denylist check at register time. Cheap to build, but the UX is thorny (what's the rejection copy? who reviews the denylist?). Defer. |
| **Custom branding** (org logo, color, login screen) | **C** | — | Real demand from resellers / agencies; zero demand from a self-hoster who IS the org. Trello has it because the Enterprise buyer has a brand team. Cardscape's branding is the project branding, not per-deployment. Skip. |
| **Power-Up / extension administration** (org-level allowlist) | **C** | — | Cardscape extensions are a v0.x-shape concept; the org-level allowlist is a thing that becomes real when extensions become a thing. Two releases too early. |
| **Sandbox / preview org** (try changes in a sandbox) | **C** | — | Operationally heavy, and the self-hoster's equivalent is "make a `docker compose up` on a different port". Not a product feature for this audience. |

### 2.4 Visibility and reporting

| Feature | Verdict | Where | Rationale |
|---|---|---|---|
| **Audit log** | **A (done)** | shipped | Every state-changing command emits a domain event; the activity feed and the per-card activity log are the user-facing surface; the admin-export endpoint is the compliance officer's surface. Lives in `01-` audit §3 and §4. |
| **Audit log export** (CSV / JSON) | **A** | v1.2.0 | Endpoint exists, the format is a TODO. Real demand: the compliance officer wants the log in a format they can hand to a regulator. JSON is enough. |
| **Central telemetry** (org-wide board counts, active users, etc.) | **B** | v1.3.0+ | A nice-to-have dashboard. The data is in the database; the surface is an aggregation query and a page. The value is real but the urgency is low (the maintainer is the only operator today). |
| **Status page** (Trello-style "all systems operational") | **A (done)** | shipped | Live at the deployment's `/status` route. Per `docs/operations/05-status-page-deploy.md`. |
| **Anomaly detection / "unusual admin action" alerts** | **C** | — | A SOC feature. The self-hoster can grep the audit log; the small-company buyer can ask the maintainer. Building ML-style alerting is a different product. Skip. |

### 2.5 Data sovereignty

| Feature | Verdict | Where | Rationale |
|---|---|---|---|
| **Data residency (region selector)** | **A (UI gated)** | shipped behind `Features:DataResidencyEnabled` | The domain, migration, and `RegionGuardEndpointFilter` are all in place. The UI selector is **opt-in** today — the rationale is recorded in [`docs/operations/06-configurable-subsystems.md`](../operations/06-configurable-subsystems.md#experimental-features-web-ui-gates). Closing the loop requires: (a) per-resource storage backend pinning (an attachment uploaded to a `Europe` workspace must land in EU S3 / EU filesystem), (b) per-region read replicas for the read path, (c) the GDPR Article 30 narrative that ties the deployment's region to a documented sub-processor list. Until those three exist, the selector is misleading and the default-off posture is the right call. |
| **Customer-managed encryption keys** (BYOK / HYOK) | **C** | — | Real enterprise demand, real cost (KMS integration, key rotation, per-tenant key isolation). The self-hoster's equivalent is "use full-disk encryption on the host" — they already do. The small-company buyer who needs BYOK has already left for a SaaS. Skip for the foreseeable future. |
| **Right-to-erasure / DSR** | **A (done)** | shipped | The soft-delete + anonymise-after-30-days pattern is in `01-` audit §S1 and §S2. The admin DSR export endpoint is in. |
| **Data export** (full user data in a portable format) | **A** | v1.2.0 | A `GET /api/users/me/export` returning a JSON zip of every row the user has touched. The schema is open (it has to be, per `01-` positioning §2.4 — vendor-lock-in is "none"). Build it. |
| **In-place data deletion** (not just soft-delete) | **C** | — | A "delete everything in this workspace, right now, no grace period" path is occasionally asked for. The 30-day grace period exists for a reason (accidental delete recovery). The exception path (admin override with audit trail) is fine. The "skip the grace period" path is not worth the build. |
| **Bring-your-own database** (Cosmos, DynamoDB, Mongo) | **C** | — | `01-` positioning §3 lists the matrix: SQLite + PostgreSQL + MariaDB. The non-relational providers are a different model — eventual consistency, no JOINs, no transactions — and the rewrite cost is enormous. If a user needs Mongo they should use Wekan. |

---

## 3. What this means for the roadmap

The buckets translate into the next three minor versions as
follows. "Shipped" items are baseline; they exist today and
do not consume roadmap budget.

### 3.1 v1.2.0 — close the enterprise gap

The aim of v1.2.0 is to take the **PARTIAL** items from the
`05-` audit and bring them to **DONE**. The deliverables:

- **SAML SSO** — implement the four protocol endpoints, add
  the `SamlAuthenticationHandler`, integration test against
  simplesamlphp.
- **2FA enforcement** — add the admin policy, log the
  enforcement event, surface the toggle in the workspace
  settings page.
- **SCIM Groups** — design the `WorkspaceMemberGroup`
  aggregate, add the SCIM `Group` CRUD endpoints, integration
  test against a fixture.
- **JIT provisioning** — add the auto-create path in the
  SAML / OIDC callback handler. Couples with the SAML
  closure.
- **Default board visibility** — org-wide setting; default
  `private`.
- **Idle session timeout** — JWT inactivity expiry.
- **Audit log export** — JSON endpoint under
  `/api/admin/audit-log/export`.
- **Data export** — `GET /api/users/me/export` with a
  portable JSON zip.

That's roughly **8 items, each 1–3 days of work** = **2–4
weeks of focused effort**, well inside the v1.2.0 envelope.

### 3.2 v1.3.0 — the next tier

These are the **B** items that survive the v1.2.0 cut.
None of them are blockers; each is a "real ask" but not a
"no-go" for a buyer evaluating Cardscape today.

- Board-level role overrides.
- De-provisioning on SCIM delete (with the 30-day retention
  sweep).
- Passkey / WebAuthn login.
- Attachment restrictions (MIME denylist).
- Email restrictions.
- Central telemetry dashboard.

### 3.3 v2.0+ — the things that need a different shape

- **Customer-managed encryption keys.** The build cost is
  large (KMS integration, per-tenant key isolation, rotation
  audit trail); the buyer that needs it is a Fortune-500 that
  will not self-host a single-tenant OSS instance. Track for
  a future commercial / multi-tenant offering if one ever
  exists.
- **Org-wide policy aggregation** (Atlassian-Access-style).
  Solved by an IdP front-end, not by Cardscape.
- **Sandbox / preview org.** The self-hoster's "sandbox" is
  `docker compose up` on a different port.

### 3.4 The never-list

These come up often and are **explicitly out**:

- **Custom branding per deployment.** Project branding is
  project branding.
- **Multi-org / multi-workspace consolidation for the
  enterprise buyer.** The self-hoster IS the only tenant.
- **Domain capture / auto-claim.** Multi-tenant SaaS
  semantics; not applicable to a self-hosted instance.
- **Anomaly detection / "unusual admin action" alerts.** A
  SOC feature. Grep the audit log.

When any of these comes up in a feature request, link this
doc, not the audit, not the positioning page. The doc is the
single source of truth for the "is this on the roadmap" call.

---

## 4. Why the data-residency default is off

The data-residency toggle is the only one in the codebase
that ships **off by default and invisible in the UI** — the
maintainer's posture for the other A-rated items is
"shipped, opt-in to the policy, opt-out of the implementation".
Data residency is special because the **only currently
enforced part** of the spec is the single write-rejection
check in `RegionGuardEndpointFilter`. Everything else
(per-resource storage pinning, per-region read replicas, the
sub-processor list) is not yet built.

A user who picks "Europe" in the UI today gets:

- The dropdown selection is stored in the database.
- A badge saying "Europe" is rendered on the workspace card.
- The next write to that workspace is **allowed** unless the
  deployment is also pinned to Europe via
  `Cardscape:Deployment:Region` (and even then, only on the
  one endpoint path that the `RegionGuardEndpointFilter`
  actually wraps).
- Attachments uploaded to a "Europe" workspace **land on
  whatever filesystem the storage root points at**, regardless
  of region.

That is **misleading** in a way that the other PARTIAL items
are not. A 2FA toggle that doesn't enforce is annoying; a
data-residency selector that doesn't move the data is a
compliance hazard. The default-off UI posture is the call
that prevents the second case.

The toggle stays in the code. The data-residency column
stays in the schema. The migration stays. The guard stays.
Flipping the flag back on requires zero other changes — the
moment the per-resource storage pinning lands (the next big
chunk of work on this axis), the UI selector comes back on
by default and the migration is invisible.

See [`docs/operations/06-configurable-subsystems.md`](../operations/06-configurable-subsystems.md#experimental-features-web-ui-gates)
for the operator-facing documentation of the toggle.

---

## 5. Re-evaluation cadence

This doc is revisited at every minor version bump. The
trigger questions:

1. Did any of the **C** items change in a way that makes
   them **B**? (A new buyer persona, a new compliance
   regime, a new open-source dependency that absorbs the
   build cost.)
2. Did any of the **B** items become **A** because someone
   asked for them twice? (A single ask is not a roadmap
   item; a recurring ask is.)
3. Did any of the **A** items become **shipped**? (Move
   them to the "shipped" baseline; they no longer consume
   roadmap budget.)
4. Did the **maintainer bandwidth** change? (A new
   maintainer moves more items from B to A. A maintainer
   who takes a sabbatical freezes the whole list.)

The doc lives at `docs/roadmap/07-trello-enterprise-parity.md`
and is referenced from `docs/roadmap/01-implementation-plan.md`
as the source of truth for the enterprise-tier roadmap.
