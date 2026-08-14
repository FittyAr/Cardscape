# OpenAPI spec

> The Cardscape REST API publishes an OpenAPI 3 document at
> every release. The document is the contract every SDK
> generator, every documentation site, and every third-party
> consumer reads to integrate with Cardscape.

## 1. Where to find it

- **In-process (Development)**: `GET /openapi/v1.json` on the
  running API. The same JSON the Scalar reference UI
  (`GET /scalar`) renders.
- **At every tagged release**: the
  [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)
  `release` job boots the API, captures the spec into
  `artifacts/openapi/openapi.json`, and publishes it as a
  workflow artifact. The artifact URL is
  `https://github.com/cardscape/cardscape/releases/download/vX.Y.Z/openapi.json`
  (or the workflow run's artifact list).
- **Per-commit**: the `release` job runs on every `v*` tag
  push and every `master` push. The artifact URL pattern is
  `<run-id>`-`<sha>` in the Actions tab.

## 2. Schema conventions

The Cardscape API uses these conventions consistently:

- **Camel-case JSON property names** — every property is
  serialised camelCase (`{ "userId": "...", "createdAt": "..." }`).
  This matches the default `System.Text.Json` convention and
  what the Blazor client expects.
- **Result-shaped errors** — every error response is
  `application/problem+json` (RFC 7807) with at least
  `{ "code": "snake_case_id", "message": "Human-readable." }`.
- **Cursor-paginated lists** — list endpoints that may grow
  large (`/api/boards/{id}/activities/`,
  `/api/boards/{id}/cards/`, the search endpoint) accept an
  opaque `cursor` query parameter. The response includes
  `nextCursor` when more results are available. Pass
  `cursor=<previous-nextCursor>` to fetch the next page.
- **DateTimeOffset as ISO-8601 strings** — every timestamp is
  serialised as `2026-07-29T13:45:00.0000000+00:00`.
- **Strongly-typed IDs are opaque GUIDs at the wire** — the
  Application layer's `CardId`, `BoardId`, etc. are
  strongly-typed in C#, but they serialise as the underlying
  `Guid`. Clients do not need to know about the
  strongly-typed-id pattern.

## 3. Endpoint groups

The full surface is documented in the live OpenAPI document.
The high-level groups:

- `/api/auth/*` — register, login, refresh, external logins,
  2FA, API tokens.
- `/api/workspaces/*` — workspaces, members, invitations.
- `/api/boards/*` — boards, lists, cards, members, labels,
  automation, extensions, webhooks, custom fields, voting,
  recurring, snooze, aging, mirror.
- `/api/cards/*` — card-level operations (move, complete,
  reopen, assign, due-date, attachments, voting,
  checklists, recurrence, snooze).
- `/api/comments/*` — add, list, edit, delete, react.
- `/api/notifications/*` — list, mark-read, mark-all-read,
  unread count.
- `/api/activities/*` — per-card and per-board activity
  timelines (cursor-paginated).
- `/api/search/*` — full-text search over cards, comments,
  checklist items, labels, activity.
- `/api/imports/*` — Kanban JSON import.
- `/api/exports/*` — per-board ZIP export.
- `/api/integrations/*` — Slack, Google Drive, GitHub, Email.
- `/api/jobs/*` — background job inspection.
- `/api/security/api-tokens/*` — personal access tokens.
- `/api/internal/broadcast` — cross-process MCP→API push.
- `/api/oauth/*` — third-party OAuth flow.
- `/api/scim/v2/*` — SCIM 2.0 provisioning.
- `/saml/{slug}/*` — SAML SSO.

## 4. Generating an SDK

The OpenAPI document is the input to every code-generation
tool. The recommended pipeline for a C# consumer:

```bash
# Download the latest spec
curl -L https://github.com/cardscape/cardscape/releases/latest/download/openapi.json \
    -o cardscape-openapi.json

# Generate a typed client (Kiota is the recommended tool)
dotnet tool install --global Microsoft.OpenApi.Kiota
kiota generate -l CSharp -d cardscape-openapi.json \
    -c Cardscape.Sdk -n Cardscape.Sdk -o ./sdk
```

Cardscape also ships a hand-written
[`sdk/Cardscape.Sdk`](../sdk/) (Phase 5 polish, see the
[v1.1.0 execution plan](../roadmap/03-execution-plan-v1.1.0.md)
§5.4) for consumers who want a stable, hand-curated
contract.

## 5. Versioning

The OpenAPI document does not embed a version; the spec is
per-release. The release tag (`v1.0.0`, `v1.1.0`, etc.) is
the source of truth. Breaking changes bump the major
version; consumers should pin to a specific release tag.

For non-breaking additions (a new endpoint, a new optional
field), the spec version field stays unchanged. New
endpoints ship in minor / patch releases.

## 6. Local development

The `dev` Compose (`docker-compose.dev.yml`) starts the API
on `http://localhost:8080`. The Scalar reference UI is at
`/scalar`. The raw JSON is at `/openapi/v1.json`.
The CI's `release` job captures the same JSON at every
release; if you see a discrepancy between the local UI and
the published spec, the local API is running a preview /
uncommitted build.
