# Authentication and authorization

> The project's model for **who a request is** (authn) and
> **what a request can do** (authz). The model is policy-
> based, with a single authorization pipeline that every
> request — web, API, MCP — runs through.
>
> This is a **design** document. The code lands in Phase 1
> (cookie + JWT) and Phase 2 (MCP API token).

---

## 1. The three principals

Cardscape has three principal types. Every authenticated
request runs as one of them.

| Principal | Where it is used | Identity claim |
|---|---|---|
| **User** | the Blazor web client, the REST API from a logged-in user | `sub` = the user id; `auth_method` = `cookie` |
| **API token** | the REST API from a third-party integration, the MCP server from an AI client | `sub` = the user id that owns the token; `auth_method` = `api_token`; `token_id` = the token's id |
| **System** | background jobs, webhooks, the email service | `sub` = `system`; `auth_method` = `system` |

The principal is resolved at the edge (the API host, the
MCP host) and propagated through the request as an
`ICurrentUser` (or `ICurrentPrincipal`) abstraction.

---

## 2. The three authn methods

The same identity (a User) can be authenticated three ways.

| Method | Transport | Lifetime | Revocation | Used by |
|---|---|---|---|---|
| **Cookie** | HTTP cookie, `Secure`, `HttpOnly`, `SameSite=Lax` | sliding (7 days idle, 30 days absolute) | the cookie is invalidated on logout; the server can revoke by invalidating the session id | the Blazor web client |
| **JWT bearer** | `Authorization: Bearer <jwt>` | 1 hour, with a refresh token (7 days, rotating) | revocation list (Redis, added in Phase 4) | third-party REST clients (advanced users) |
| **API token** | `Authorization: Bearer <secret>` (the secret is the token) | configurable per token; default 90 days, with a hard cap of 1 year | the token is deleted in the `Members` context; the secret is hashed and never stored in plaintext | the MCP server, scripted REST clients |

The cookie, the JWT, and the API token are all backed by
the same `User` entity. The auth method is an attribute of
the request, not a separate user.

### Cookie auth details

The cookie is set by the API host on successful login. The
cookie value is a session id; the session data (user id,
IP, user agent, last-seen) is stored server-side (in the
database in Phase 1, in Redis in Phase 4). The cookie is
`Secure` (HTTPS only) and `HttpOnly` (not readable by
JavaScript).

The login flow is the standard ASP.NET Identity flow:
email + password → validation → cookie set.

### JWT auth details

The JWT is signed with `HS256` in Phase 1 (a server-side
secret), with a plan to move to `RS256` (a public key, for
key rotation) in Phase 4. The token claims are:

- `sub` — the user id.
- `email` — the user's email.
- `iat` — issued at.
- `exp` — expires at.
- `scope` — space-separated list of scopes (for tokens;
  unused for user JWTs).
- `workspace` — the current workspace id (when applicable).

The refresh token is a separate, longer-lived, one-time-use
token. The server rotates the refresh token on every use.

### API token auth details

The API token is a 32-byte random secret, base64url-encoded.
The secret is presented as `Authorization: Bearer <secret>`.
The server hashes the secret with PBKDF2 (Phase 1) or
Argon2id (Phase 4) and looks up the hash in the `ApiToken`
entity.

The token entity carries:

- `id` — the token's id (the public id, used in audit logs).
- `user_id` — the owner.
- `name` — a human-readable label ("Claude Desktop",
  "My CI script").
- `scopes` — the list of scopes the token can use.
- `secret_hash` — the hashed secret.
- `created_at` — when the token was created.
- `last_used_at` — when the token was last used.
- `expires_at` — when the token expires (nullable for
  non-expiring tokens; default 90 days).
- `revoked_at` — when the token was revoked (nullable).

The plain-text secret is shown to the user **once**, at
creation time, and never again. Lost tokens cannot be
recovered; they must be revoked and re-created.

---

## 3. The authorization model

Authorization is **policy-based**, with a single
authorization pipeline that every request runs through. The
policies are declared once, in code, and looked up by name
at the call site (endpoint, MCP tool, or command handler).

### The `IAuthorizationService` interface

```csharp
public interface IAuthorizationService
{
    Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object resource,         // the resource being accessed
        string policy,           // the policy name
        CancellationToken ct = default);
}
```

The result is one of:

- `Authorized` — the request may proceed.
- `Forbidden` — the request is denied. Returns 403.
- `ResourceNotFound` — the resource does not exist (or the
  user cannot see it; the two are indistinguishable to
  avoid leaking the existence of resources). Returns 404.

### The `Forbidden` vs `ResourceNotFound` rule

If the user does not have access to a resource, the API
returns 404, not 403. The 403 would tell the user that the
resource exists and they cannot see it; the 404 tells them
nothing. The 404 is also the correct response when the
resource genuinely does not exist, so the two are
indistinguishable.

The rule is enforced in the handler, not in the policy. The
handler looks up the resource; if the lookup returns null,
the handler returns `Result.Failure(NotFound)`. The
authorization policy is only consulted when the resource
exists.

### The policy catalogue

Policies are declared in
`src/Cardscape.Application/Authorization/Policies.cs`. The
catalogue:

| Policy | Resource | Decision |
|---|---|---|
| `workspace.member` | `Workspace` | the user is a member of the workspace |
| `workspace.admin` | `Workspace` | the user is an admin of the workspace |
| `board.reader` | `Board` | the user can read the board (member, or board is public) |
| `board.editor` | `Board` | the user can write to the board (member with editor role) |
| `board.admin` | `Board` | the user is an admin of the board (workspace admin or board owner) |
| `card.editor` | `Card` | the user can write to the card's board |
| `card.assigner` | `Card` | the user is a board editor AND the user is allowed to assign cards (board-level setting) |
| `comment.author` | `Comment` | the user is the author of the comment OR the user is a board admin |
| `extension.installer` | `Board` | the user can install extensions on the board (board admin) |
| `automation.author` | `Board` | the user can author automation rules (board admin) |
| `apittoken.owner` | `ApiToken` | the user owns the token OR the user is a workspace admin |
| `mcp.tool.<tool_name>` | varies | the API token has the required scope for the MCP tool |

A new resource type or a new operation requires a new
policy. The policy is added to the catalogue and the test
in the same PR.

---

## 4. Roles vs scopes vs permissions

Three concepts, three use cases.

| Concept | Use case | Stored in | Lifetime |
|---|---|---|---|
| **Role** | a coarse label on a user (admin / member / observer) | `Member.Role` on the `Workspace` | the duration of the membership |
| **Scope** | a fine-grained label on an API token (`boards:read`, `cards:write`) | `ApiToken.Scopes` | the duration of the token |
| **Permission** | the result of evaluating a policy for a user + resource | not stored; computed per request | per request |

Roles are for **users** (human or system). Scopes are for
**API tokens**. Permissions are for **the request**, and are
computed by the authorization pipeline.

The mapping is:

- A user with the `admin` role on a workspace satisfies
  every workspace policy and every board policy in the
  workspace.
- A user with the `member` role satisfies `workspace.member`
  and `board.reader` and `card.editor`.
- A user with the `observer` role satisfies `workspace.member`
  and `board.reader`, but not `card.editor`.
- An API token with the `boards:write` scope satisfies
  every `board.editor` policy, but only on behalf of the
  user who owns the token.
- An API token without the `cards:read` scope is denied
  every `card.*` policy.

The authorization pipeline resolves a role or a scope to a
permission at request time. The lookup is cheap (a
`switch` on the role or the scope) and is the same code
path for the REST API, the Blazor client, and the MCP
server.

---

## 5. The multi-tenancy boundary

Every resource has a `workspace_id`. Every query filters by
`workspace_id`. The filter is enforced at the EF Core query
filter level (`HasQueryFilter`), so a missing filter is a
**compile error** in tests.

The multi-tenancy rule:

> No query, no command, no endpoint, no MCP tool may
> operate on a resource without first verifying that the
> resource's `workspace_id` matches the current user's
> active workspace.

The "active workspace" is a claim on the user's session /
JWT / API token. Switching workspaces is a user action
(clicking the workspace switcher in the web UI, or a
`PUT /api/v1/me/active-workspace` call). The active
workspace is the only workspace the user can act on for
the duration of the request.

---

## 6. The audit log

Every administrative action (workspace settings, member
add/remove, role change, board visibility change, API
token create/revoke, MCP server config change) is logged
to the `AuditLog` entity. The log entry carries:

- `id` — the log entry's id.
- `actor_id` — the user (or `system`) that performed the
  action.
- `actor_principal` — `user` or `api_token:<token_id>`.
- `action` — the action name (`workspace.member.add`,
  `apittoken.revoke`).
- `target_type` — the resource type.
- `target_id` — the resource id.
- `metadata` — the before/after state, as a JSON diff.
- `at` — the timestamp.
- `trace_id` — the W3C `traceparent` of the request.

The audit log is append-only. It is never updated or
deleted. The retention is 7 years (the default for SOC 2
compliance; the user can configure a shorter retention for
non-regulated deployments).

---

## 7. The MCP server

The MCP server authenticates with API tokens only (no
cookies, no JWTs — the AI client does not have a browser).
The auth handler is `ApiTokenAuthenticationHandler`, which
extracts the `Authorization: Bearer <secret>` header, hashes
the secret, and looks up the `ApiToken` entity.

The auth handler is registered in the MCP host's DI
container. The handler sets the `ClaimsPrincipal` on the
request, with the user id and the token's scopes as claims.
The rest of the authorization pipeline is the same as the
REST API.

The MCP server's tools declare their required scope with
the `McpScopeAttribute`. The scope is enforced by the
authorization pipeline; a tool called without the required
scope is denied with `mcp.scope.forbidden` (the same error
code as the REST API).

---

## 8. The `ICurrentUser` abstraction

The `Application` layer does not see `ClaimsPrincipal`. It
sees `ICurrentUser`:

```csharp
public interface ICurrentUser
{
    UserId Id { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Scopes { get; }
    WorkspaceId? ActiveWorkspaceId { get; }
    bool IsSystem { get; }
}
```

The implementation is set at the edge (the API host, the
MCP host, the test host) and propagated through the
MediatR pipeline. The `Application` layer does not know
about cookies, JWTs, or API tokens; it only knows about
`ICurrentUser`.

This is the abstraction that makes the MCP server and the
REST API share the `Application` layer.

---

## 9. Password policy

The password policy is enforced at registration and at
password change:

- Minimum 12 characters.
- No maximum (long passphrases are good).
- Must contain at least one character from three of the
  four classes: lowercase, uppercase, digit, symbol.
- Cannot be the email, the display name, or the username.
- Cannot be on the Have I Been Pwned (HIBP) top 100k
  passwords list. The check is done locally (k-anonymity
  API) to avoid sending the password to a third party.

The password is hashed with **PBKDF2-SHA256** with 100k
iterations and a 16-byte salt in Phase 1, and with
**Argon2id** in Phase 4. The hash is stored in the
`User.PasswordHash` field; the salt is stored alongside it
(combined in the standard `pbkdf2$...$...` format).

We do not enforce password rotation. Forced rotation
encourages weak passwords ("Spring2026!", every 90 days).
We do enforce a "must change on next login" flag for
compromise scenarios.

---

## 10. The 2FA and SSO paths

Two-factor authentication (TOTP) and SAML/OIDC SSO are
**out of scope for Phase 1**. They land in Phase 4 (see
[the implementation plan](../roadmap/01-implementation-plan.md)).

When they land, the same `ClaimsPrincipal` carries the 2FA
and SSO claims. The authorization pipeline is unchanged.

---

## 11. Anti-patterns (do not do this)

- **`[Authorize]` only on the controller** — the policy
  must be checked against the **resource**, not just the
  user's role. A user can be a workspace member but not a
  board editor; the `[Authorize]` attribute does not see
  the difference.
- **403 when the resource does not exist** — return 404.
  The 403 leaks the existence of the resource.
- **Logging the JWT, the cookie, or the API token secret**
  — even partially redacted. See
  [02-logging-observability.md](02-logging-observability.md).
- **Storing the password in plaintext "for debugging"** —
  the password is hashed before the value leaves the
  registration endpoint.
- **Using a single global admin role** — a workspace
  admin is not a system admin. The two are separate.
- **Checking authorization in the UI only** — the UI
  hides controls the user cannot use, but the server is
  the source of truth. A user with curl can call any
  endpoint.

---

## 12. When to revisit

This document is revisited when:

1. A new resource type is added (e.g. "views" in Phase 3)
   and needs a new policy.
2. A new auth method is added (e.g. SAML SSO in Phase 4).
3. The multi-tenancy model changes (e.g. a user is in
   multiple workspaces and the active workspace is per-tab,
   not per-session).
4. A new compliance requirement (SOC 2, GDPR, HIPAA) imposes
   a new auth or audit rule.

Until then, this document is the source of truth for
authentication and authorization in Cardscape.
