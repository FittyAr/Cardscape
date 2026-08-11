# Secure coding checklist

> The checklist a reviewer runs through on every pull
> request that touches a security-sensitive area. The
> checklist is derived from
> [`01-threat-model.md`](01-threat-model.md); the threat
> model is the "why", this checklist is the "what to look
> for".
>
> The PR template's "Security" section requires the
> author to walk through this checklist before the PR is
> marked ready for review.

---

## How to use this checklist

The reviewer runs the checklist on the PR diff. A check
that does not apply is marked "N/A" with a one-line
justification. A check that fails is a **blocker**: the PR
is not merged until the failure is resolved or a maintainer
explicitly accepts the risk (with a written justification
in the PR thread).

The checklist is organized by **what the PR touches**, not
by STRIDE category. This matches the way a reviewer
actually reads a diff: "this PR adds a new endpoint", "this
PR changes a query", etc.

---

## 1. The PR touches input from the user

A user input is anything the user can control: a query
parameter, a request body field, a header, a cookie, a
file upload, a websocket message.

| Check | How to verify |
|---|---|
| **The input is validated against an allowlist, not a denylist.** | The validation uses a regex or a parser; it does not strip `<`, `>`, `&`, etc. |
| **The input is length-limited.** | The maximum length is declared in the value object or in the model; the validator enforces it. |
| **The input is type-checked at the boundary.** | The endpoint signature is `Guid id`, not `string id`; the binder returns 400 on a malformed id, not 500. |
| **The input is HTML-encoded at the render boundary, not at the input boundary.** | The Blazor component uses `@variable`, not `@Html.Raw(variable)`. The API never returns HTML; it returns JSON. |
| **The input is SQL-parameterized, not concatenated.** | The query uses `FromSqlInterpolated` or LINQ; there is no `FromSqlRaw` with `$"..."` interpolation. |
| **The input is not logged.** | The input does not appear in the log statements (see [02-logging-observability.md](../design/02-logging-observability.md)). |
| **The input is not reflected back in a URL without encoding.** | The URL builder encodes the input (`Uri.EscapeDataString` or equivalent). |
| **The file upload is scanned for malware.** | The upload endpoint calls the configured malware scanner (added in Phase 3+); the file is stored in a temporary location until the scan passes. |
| **The file upload's MIME type is verified, not trusted.** | The server reads the file's content and detects the MIME type (e.g. `MagicByteDetector`); the client's `Content-Type` is not trusted. |

---

## 2. The PR touches authentication

The PR adds or changes a login endpoint, a password
change, a token issuance, a session management, or an
auth-method change.

| Check | How to verify |
|---|---|
| **The password is hashed, not stored.** | The `User.PasswordHash` field is the only place the password is stored; the field is set by `IPasswordHasher.HashPassword`, not by a plain assignment. |
| **The password is never logged.** | The password does not appear in any log statement; the audit log stores the action, not the password. |
| **The password policy is enforced.** | The new endpoint calls the same `PasswordPolicy.Validate` as the registration endpoint; there is no per-endpoint policy. |
| **The session is invalidated on logout.** | The logout endpoint deletes the session row in the database (Phase 1) or the Redis entry (Phase 4). |
| **The session is invalidated on privilege change.** | A role change (e.g. demoting from admin to member) invalidates the user's active sessions. |
| **The session cookie has the right flags.** | The cookie is `Secure`, `HttpOnly`, `SameSite=Lax`. The `__Host-` prefix is used (Phase 4+). |
| **The JWT is signed with a strong algorithm.** | The algorithm is HS256 with a 256-bit secret (Phase 1) or RS256 with a 2048-bit key (Phase 4). The `alg` is enforced; `alg=none` is rejected. |
| **The JWT has a short lifetime.** | The access token is 1 hour; the refresh token is 7 days, rotating. |
| **The API token is hashed at rest.** | The `ApiToken.SecretHash` field is the only place the secret is stored; the field is set by the API token service, not by a plain assignment. |
| **The API token is shown to the user only once.** | The plain-text secret is returned in the response of the create endpoint; it is not in the database; it cannot be retrieved later. |

---

## 3. The PR touches authorization

The PR adds or changes a policy, a role, a scope, or a
resource-access check.

| Check | How to verify |
|---|---|
| **The resource is looked up before the policy is checked.** | The handler does `var board = await _boards.GetByIdAsync(id); if (board is null) return NotFound;` **before** calling the authorization service. The check is on the resource, not just on the user's role. |
| **The 403 / 404 rule is followed.** | A user without access to a resource gets 404, not 403. The handler returns `NotFound` when the lookup fails or when the policy fails; the API does not distinguish the two. |
| **The policy is declared in `Policies.cs`, not inline.** | The policy's name is in the catalogue; the handler references the name, not the implementation. |
| **The policy is tested.** | The test covers: an authorized user (the policy passes), an unauthorized user (the policy fails with the correct error code), a non-existent resource (the lookup fails before the policy is checked). |
| **The multi-tenancy filter is in place.** | The EF Core query has `HasQueryFilter(b => b.WorkspaceId == _currentUser.ActiveWorkspaceId)`. A test asserts that a query for a different workspace returns 0 rows. |
| **The audit log is written for administrative actions.** | The action is in the catalogue of audited actions; the handler writes the audit log entry in the same transaction as the action. |

---

## 4. The PR touches the database

The PR adds or changes a query, a migration, a stored
procedure, or an index.

| Check | How to verify |
|---|---|
| **The query is parameterized.** | There is no string interpolation in `FromSqlRaw`. EF Core LINQ is parameterized by default. |
| **The query has an index that matches the WHERE clause.** | The EXPLAIN plan (run in the integration test) shows an index scan, not a full table scan. |
| **The query returns a bounded result set.** | The endpoint uses pagination (`Skip` / `Take`); there is no `ToListAsync` on a query that could return 1M rows. |
| **The migration is reversible.** | The migration has a `Down` method that undoes the `Up`; the test runs `migrate down && migrate up` and asserts the schema is identical. |
| **The migration is lock-aware.** | Long-running migrations use the expand-contract pattern (see [01-conventions.md](../development/01-conventions.md)); the API stays up during the migration. |
| **No N+1 query.** | The EF Core profiler (run in the integration test) shows a single query for the operation, not 1 + N. |
| **The query respects the multi-tenancy filter.** | The query is on a `DbSet<T>` where `T` has a `HasQueryFilter` for `WorkspaceId`; a test asserts the filter is in effect. |

---

## 5. The PR touches the MCP server

The PR adds or changes an MCP tool, a resource, a prompt,
or a transport.

| Check | How to verify |
|---|---|
| **The tool's scope is declared.** | The tool has an `[McpScope("cards:read")]` attribute (or equivalent); the scope is enforced by the authorization pipeline. |
| **Write idempotency remains centralized.** | Classify the tool as `write`; the `tools/call` filter consumes `_meta.idempotencyKey`, hashes the tool plus canonical arguments and replays through the Application idempotency store. Do not add per-tool idempotency parameters. |
| **The tool's parameters are validated.** | The parameter validation is the same as the REST API's; the validator is shared, not duplicated. |
| **The tool's response does not leak the user's data to another user.** | The tool's response is scoped to the API token's user; the tool does not return data the user cannot see via the REST API. |
| **The tool's OTel span has the right attributes.** | The span has `cardscape.user_id`, `cardscape.workspace_id`, and `cardscape.mcp.tool`; the trace id is propagated to the `Application` layer. |
| **The tool does not call an external service without explicit user consent.** | An outbound HTTP call (e.g. an AI provider) is gated by a feature flag and a per-workspace configuration; the default is "off". |

---

## 6. The PR touches secrets, configuration, or environment

| Check | How to verify |
|---|---|
| **No secret is in the diff.** | The diff does not contain any string that looks like a secret (API key, password, connection string, OAuth client secret). The CI greps for common patterns. |
| **The secret is read from an environment variable, not from `appsettings.json`.** | The configuration is `Environment.GetEnvironmentVariable("Cardscape__JwtSecret")` or `builder.Configuration["JwtSecret"]` (which reads from env vars in production). |
| **The secret has a placeholder in `appsettings.json`.** | The committed `appsettings.json` has `null` or a placeholder for the secret; the real value is in `appsettings.Development.json` (gitignored) or in the environment. |
| **The secret is rotated.** | The new secret is documented in the secrets-rotation runbook (`docs/operations/`); the rotation procedure is tested. |
| **The new dependency does not introduce a known vulnerability.** | `dotnet list package --vulnerable --include-transitive` is clean; the dependency review action (added in Phase 5) does not flag the change. |

---

## 7. The PR touches a client-side surface (Blazor, JS interop)

| Check | How to verify |
|---|---|
| **No `innerHTML` or `dangerouslySetInnerHTML`-equivalent.** | The Blazor component uses `@variable`, not `@Html.Raw`. |
| **No `eval`, no `new Function(...)` in JS interop.** | The JS interop calls a named function; the function is bundled with the app, not loaded from a user-controlled string. |
| **No `window.open` with a user-controlled URL.** | The URL is validated against an allowlist; `noopener` and `noreferrer` are set. |
| **No `localStorage` or `sessionStorage` for sensitive data.** | Sensitive data (tokens, PII) is not in the browser's storage; if it must be, it is encrypted. |
| **The form's CSRF protection is in place.** | The Blazor form uses the anti-forgery token; the API has the corresponding anti-forgery middleware. |
| **The WebSocket or SSE connection is authenticated.** | The upgrade request carries the cookie or the bearer token; the server rejects an unauthenticated upgrade. |

---

## 8. The PR touches the build, CI, or release pipeline

| Check | How to verify |
|---|---|
| **The CI step does not print secrets.** | The CI script does not `echo $SECRET`; secrets are referenced by name, not by value. |
| **The CI step runs in a clean container.** | The CI image is rebuilt per build; there is no shared mutable state. |
| **The release artifact does not contain secrets.** | The release tarball is scanned for the same patterns the PR diff is scanned for. |
| **The release is signed.** | The git tag is signed (`git tag -s`); the NuGet package is signed (added in Phase 4+); the Docker image is signed (cosign, added in Phase 4+). |

---

## 9. The PR's author

| Check | How to verify |
|---|---|
| **The author is not the only reviewer.** | The CODEOWNERS file assigns a reviewer; the maintainer is not the only approver on a security-sensitive PR (the maintainer can override this for solo work, with a written justification). |
| **The author has signed off on the checklist.** | The PR description's "Security" section is complete; the author has answered every check. |

---

## 10. What to do when a check fails

A failing check is a blocker, but it is also a learning
opportunity. The reviewer:

1. **Marks the check as failed** in the PR review (line
   comment, not a generic "needs changes").
2. **Explains why** the check failed (what the code does
   that the check disallows).
3. **Suggests a fix** (a code change, a doc update, a
   test addition).
4. **Does not approve the PR** until the check passes.

The author:

1. **Fixes the code** (or justifies why the risk is
   accepted).
2. **Re-runs the checklist** in the PR description.
3. **Re-requests review**.

A maintainer can override a failed check with a written
justification in the PR thread. The justification is
recorded in the PR's merge commit message and in the
audit log. The override is **not** a precedent for future
PRs.

---

## 11. When to revisit

This checklist is revisited when:

1. A new STRIDE category is added to the threat model.
2. A new attack vector is discovered (in the project's own
   code, in a dependency, or in the industry).
3. A new compliance requirement (SOC 2, GDPR, HIPAA) adds
   a new control.
4. The author of the checklist changes (the maintainer, in
   practice).

Until then, this checklist is the source of truth for
secure-coding reviews in Cardscape.
