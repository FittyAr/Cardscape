# Threat model

> The project's threat model, in **STRIDE** form, per
> bounded context. The model is the input to every security
> review of a change in the affected context. It is also the
> input to the secure-coding checklist in
> [`02-secure-coding-checklist.md`](02-secure-coding-checklist.md).
>
> The project is in pre-alpha; the threat model is a living
> document that grows as the architecture grows. New
> bounded contexts get a STRIDE section in the PR that
> introduces them.

---

## 1. The trust boundaries

The model has five trust boundaries. Every request crosses
at least one.

| Boundary | Description | Trust level |
|---|---|---|
| **The user's browser** | the Blazor WASM client running in the user's browser | untrusted (the user controls the browser) |
| **The network** | the path from the browser to the API host, and from the AI client to the MCP host | untrusted (an attacker can read and modify traffic; TLS protects confidentiality) |
| **The API host** | the ASP.NET Core process serving the REST API | trusted |
| **The MCP host** | the ASP.NET Core process serving the MCP server | trusted |
| **The database** | the SQLite / PostgreSQL / MariaDB instance | trusted |

The "trusted" boundaries are trusted only within the
process. A bug in the code (e.g. an injection
vulnerability) moves the boundary inward (an attacker
controls the input, so the input is untrusted).

---

## 2. STRIDE, per category

STRIDE is Microsoft's threat-modeling framework. The
categories:

| Letter | Category | Question it answers |
|---|---|---|
| **S** | **Spoofing** | can an attacker impersonate a user? |
| **T** | **Tampering** | can an attacker modify data they should not be able to? |
| **R** | **Repudiation** | can a user deny they did something they did? |
| **I** | **Information disclosure** | can an attacker read data they should not be able to? |
| **D** | **Denial of service** | can an attacker make the system unavailable? |
| **E** | **Elevation of privilege** | can an attacker gain permissions they should not have? |

Each bounded context is reviewed against the six
categories. The threats that are not in scope (out of
scope) are listed explicitly.

---

## 3. The `Members` context (authn, authz, users)

### S — Spoofing

| Threat | Mitigation |
|---|---|
| Credential stuffing (a leaked password from another service) | HIBP check at registration and at password change |
| Password spraying (a common password against many accounts) | rate limit on the login endpoint (10 attempts / 15 min per IP + per account) |
| Session hijacking (cookie theft) | `Secure`, `HttpOnly`, `SameSite=Lax`; rotation on privilege change |
| JWT forgery | signing with HS256 (Phase 1) or RS256 (Phase 4); no `alg=none` |
| API token theft | the token is a high-entropy secret; the user can revoke it; the secret is shown once and never again |

### T — Tampering

| Threat | Mitigation |
|---|---|
| Profile change by another user | authorization check (`workspace.member`, `board.editor`); the change is in a transaction with an audit log entry |
| Password change without the current password | require the current password (or a password-reset token) for a password change |

### R — Repudiation

| Threat | Mitigation |
|---|---|
| "I didn't do that" on a sensitive action | every administrative action is logged in the `AuditLog` with the actor's id, the trace id, and the before/after state |

### I — Information disclosure

| Threat | Mitigation |
|---|---|
| User enumeration via the "forgot password" endpoint | the response is the same whether the user exists or not ("if the email is registered, you will receive a reset link") |
| Password leak in the logs | passwords are never logged (see [02-logging-observability.md](../design/02-logging-observability.md)) |
| API token leak in the logs | API tokens are never logged; the audit log stores the token id, not the secret |
| Profile leak across workspaces | the user profile is global; the workspace membership is what grants access. The profile fields (email, display name) are visible only to the user themselves and to workspace admins |

### D — Denial of service

| Threat | Mitigation |
|---|---|
| Login endpoint flooding | rate limit (10 attempts / 15 min per IP + per account) |
| Password reset flooding | rate limit (3 reset requests / 24h per email) |
| API token creation flooding | rate limit (10 tokens / 24h per user) |

### E — Elevation of privilege

| Threat | Mitigation |
|---|---|
| A user assigning themselves the `admin` role | the role change is authorized by the `workspace.admin` policy; the user cannot satisfy that policy for themselves |
| An API token being used for a scope it does not have | the authorization pipeline checks the token's scopes against the required scope; the check is per-tool, not per-endpoint |
| A workspace admin accessing the system admin | the system admin role is a separate role; workspace admins cannot satisfy `system.admin` policies |

### Out of scope

- A user handing their credentials to someone else. The
  user is responsible for their credentials; we do not
  detect or prevent credential sharing.
- A user with physical access to a logged-in browser. The
  browser is the user's responsibility; we do not detect
  or prevent physical access.
- A user who chooses a weak password despite the HIBP
  check. The HIBP check is a best-effort; we cannot
  enforce strong passwords without locking out legitimate
  users.

---

## 4. The `Boards` context (workspaces, boards, lists, cards)

### S — Spoofing

| Threat | Mitigation |
|---|---|
| A user impersonating another user on a board | the board is workspace-scoped; the user must be a member of the workspace to interact with the board |

### T — Tampering

| Threat | Mitigation |
|---|---|
| A user editing a card they cannot see | the authorization check (`board.editor`) is on the card's board, not on the card id alone; the handler looks up the board first |
| A user moving a card to a list on a different board | the move endpoint validates that the list is on the same board as the card |
| A user assigning a card to themselves in a workspace where they are not a member | the assignment is authorized by `card.assigner`, which requires `board.editor` |
| Optimistic concurrency bypass | every write uses an `IF_Match` header (ETag); the write fails with 409 if the version does not match |

### R — Repudiation

| Threat | Mitigation |
|---|---|
| "I didn't archive that card" | the archive action is logged in the `Activity` stream on the card; the stream is append-only |

### I — Information disclosure

| Threat | Mitigation |
|---|---|
| Listing all card ids in a workspace | the list endpoint requires `board.reader` on the specific board; an unauthorized user gets 404 |
| Reading a private board's metadata | the board lookup is workspace-scoped; the user must be a member |
| Attachment access without permission | the attachment endpoint validates the user's `board.reader` on the attachment's board, not just on the attachment id |

### D — Denial of service

| Threat | Mitigation |
|---|---|
| A user creating 1M cards in a board | rate limit on card creation (60 / min per user, 600 / min per board) |
| A user creating 1M lists in a board | rate limit on list creation (10 / min per board) |
| A user uploading a 10 GB attachment | attachment size cap (default 100 MB, configurable per workspace) |

### E — Elevation of privilege

| Threat | Mitigation |
|---|---|
| A workspace member becoming a board admin | the role change is authorized by `board.admin`; a member does not satisfy this policy |
| A user bypassing the board visibility setting | the visibility setting is enforced in the EF Core query filter (`HasQueryFilter`); a missing filter is a compile-time test failure |

### Out of scope

- A workspace admin reading the contents of a private board
  in their workspace. The admin is trusted.
- A user moving a card to a list that is archived. The
  archived state is metadata, not a security boundary; the
  user can still move to it (the UI hides it, the API
  returns 409 with `conflict.list_archived`).

---

## 5. The `MCP` context (the MCP server)

The MCP server has additional threats because the AI
client is a **new kind of principal** (not a human with a
browser, not a third-party script). The threats:

### S — Spoofing

| Threat | Mitigation |
|---|---|
| An AI client impersonating another user | the API token is a per-user secret; the token's `user_id` is the only user the client can act on |
| An AI client using a revoked token | the revoked token check is on every request; the revocation list is the `ApiToken.RevokedAt` field |

### T — Tampering

| Threat | Mitigation |
|---|---|
| An AI client calling a write tool without the required scope | the authorization pipeline checks the scope; the tool returns `mcp.scope.forbidden` |
| An AI client retrying a write and creating duplicates | the centralized `tools/call` filter applies `_meta.idempotencyKey` to every catalogued write; the same owner, tool and canonical payload replay the stored result |

### R — Repudiation

| Threat | Mitigation |
|---|---|
| "The AI didn't do that" | every MCP tool call is logged with the API token id, the user, the tool, the parameters, and the trace id |

### I — Information disclosure

| Threat | Mitigation |
|---|---|
| An AI client reading cards it should not see | the tools are authorized by the same policies as the REST API; an unauthorized tool call returns `mcp.scope.forbidden` |
| An AI client leaking the user's data in a prompt to another model | the MCP server does not make outbound calls except to the configured AI provider (if any); the data does not leave the user's self-hosted instance unless the user has configured a cloud AI provider |
| An AI client exfiltrating data through tool parameters | the tool parameters are validated against the same input-validation rules as the REST API |

### D — Denial of service

| Threat | Mitigation |
|---|---|
| An AI client calling a tool 1M times in a loop | rate limit per API token (1000 calls / hour per token, configurable) |
| An AI client triggering an expensive query (e.g. a search) | the search tool has its own rate limit (60 / hour per token) |

### E — Elevation of privilege

| Threat | Mitigation |
|---|---|
| An AI client calling a write tool with a read-only token | the tool's required scope is checked; a read-only token does not satisfy the write tool's scope |
| An AI client calling a system-level tool | system-level tools are behind a separate scope (`mcp.system`) that is not granted to user tokens by default |

### Out of scope

- An AI client producing harmful output. The MCP server
  returns the data; the AI client (Claude Desktop, Cursor,
  etc.) is responsible for how it presents the data to the
  user.
- An AI client being prompt-injected by malicious data in
  a card description. The MCP server returns the data
  faithfully; the AI client is responsible for the
  trust boundary between user data and AI actions. We
  document this in the MCP server ADR and in the
  developer guide, but we do not solve it in the MCP
  server.

---

## 6. The `Automation` context (rules, buttons, scheduled commands)

The automation engine (Phase 3) lets users write rules
that act on board events. The threats are similar to
`Boards`, with the addition of:

### T — Tampering

| Threat | Mitigation |
|---|---|
| A user writing a rule that archives every card | the rule engine validates the action against the user's permissions at **rule execution time**, not at rule authoring time. A user cannot write a rule that does something they could not do manually. |
| A user writing a rule that sends an email to an arbitrary address | the `email.send` action validates the recipient against the workspace's allowed email domains (default: any, configurable) |
| A user writing a rule that calls a webhook to an internal IP | the `webhook.call` action validates the URL against a blocklist of private IP ranges (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8) — SSRF prevention |

### D — Denial of service

| Threat | Mitigation |
|---|---|
| A rule that runs in an infinite loop | the rule engine has a per-execution timeout (5 seconds) and a per-user quota (250 runs / month, configurable) |
| A scheduled command that creates 1M cards | the action's per-execution rate limit is enforced (e.g. `card.create` is limited to 60 / min per rule) |

### E — Elevation of privilege

| Threat | Mitigation |
|---|---|
| A user writing a rule that uses an API token they do not have | the rule runs as the user who wrote it, with the user's permissions. The rule cannot use a different user's API token. |

---

## 7. The `Integrations` context (webhooks, OAuth, third-party)

The integrations (Phase 3) add external services. The
threats are about the trust we extend to those services.

### S — Spoofing

| Threat | Mitigation |
|---|---|
| A webhook receiver impersonating the sender | webhook payloads are signed with HMAC-SHA256; the receiver verifies the signature with the shared secret |
| A third-party OAuth app impersonating the user | OAuth 2.0 with PKCE; the redirect URI is exact-match; the access token is scoped to the application's requested scopes |

### T — Tampering

| Threat | Mitigation |
|---|---|
| A webhook payload being modified in transit | TLS for the webhook delivery; HMAC signature for the payload |
| A third-party OAuth app exceeding its scope | the access token's scopes are enforced on every API call; an app cannot access a resource outside its scope |

### I — Information disclosure

| Threat | Mitigation |
|---|---|
| A webhook payload leaking data to an unintended receiver | the webhook's URL is set by the workspace admin; the URL is validated at configuration time; the secret is rotated on every URL change |
| A third-party OAuth app reading data outside its scope | the scopes are enforced; an app requesting `boards:read` cannot read cards |

### E — Elevation of privilege

| Threat | Mitigation |
|---|---|
| A third-party OAuth app gaining workspace admin | the app's scopes are user-approved; the user can revoke the app at any time; the app does not get the `workspace.admin` scope by default |

---

## 8. The `Infrastructure` cross-cutting concerns

The cross-cutting concerns (configuration, secrets, the
build pipeline) are not bounded contexts but have their
own threats.

| Threat | Mitigation |
|---|---|
| A secret leaking via `appsettings.json` | secrets are in environment variables, not in `appsettings.json`; `appsettings.json` is committed to git with placeholders only |
| A secret leaking via the logs | the log redaction filter strips common secret patterns (`password=`, `token=`, `Authorization: Bearer`) |
| A CI pipeline being compromised | the CI runs in a clean container per build; the build's secrets are mounted as environment variables, not stored in the pipeline file |
| A dependency being compromised | the dependency review action (added in Phase 5) flags new dependencies and known-vulnerable versions; `dotnet list package --vulnerable` is in the CI |

---

## 9. What we are explicitly NOT protecting against

The project makes deliberate trade-offs. The following
threats are **accepted** and documented as such.

- **Nation-state attackers** with physical access to the
  data center. The threat model assumes a well-run
  self-hosting environment, not a hostile one.
- **Compromise of the user's browser**. The user is
  responsible for the security of their browser; we do not
  defend against a browser that is already compromised
  (e.g. a malicious extension reading the WASM payload).
- **Compromise of the AI client**. The AI client
  (Claude Desktop, Cursor, etc.) is a third-party
  application. We trust the client; we do not defend
  against a client that is already compromised.
- **Compromise of the user's email**. Password reset uses
  email; if the email is compromised, the password reset
  is also compromised. We do not defend against this
  beyond the standard token expiration.
- **DoS against a self-hosted instance the reporter does
  not own**. The reporter is responsible for the
  availability of their own instance.

---

## 10. The review process

Every PR that touches a security-sensitive area (auth,
authz, MCP, secrets, cryptography, dependency upgrades) is
reviewed against this document. The PR template's
"Security" section requires the author to:

1. List the bounded contexts affected.
2. For each context, list the STRIDE categories affected.
3. For each affected category, list the threats considered
   and the mitigations applied.
4. Reference the specific section of this document.

A PR that does not complete the security section is
rejected in review.

---

## 11. When to revisit

This document is revisited when:

1. A new bounded context is added (the new context gets a
   STRIDE section in the same PR).
2. A new auth method is added (e.g. SAML SSO in Phase 4).
3. A new transport is added to the MCP server (e.g. gRPC
   in Phase 5+).
4. A new compliance requirement (SOC 2, GDPR, HIPAA)
   imposes a new threat or control.
5. A real incident reveals a gap in the model (the model
   is updated in the post-mortem PR).

Until then, this document is the source of truth for the
project's threat model.
