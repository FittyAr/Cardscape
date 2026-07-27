# API conventions

> The public REST API exposed by `Cardscape.Api`. This document
> describes the conventions; the per-endpoint reference will be
> generated from Swashbuckle and live in `/swagger` at runtime.

## 1. Base URL

```
https://<host>/api
```

All endpoints are under `/api`. The version (if any) is part of
the route: `/api/boards`, `/api/v2/boards`. We start at v1 (no
prefix) and bump to `/api/v2/...` when we make a breaking
change.

## 2. Versioning policy

- **Non-breaking** changes (new endpoint, new optional field on
  a response, new query parameter) go out without a version
  bump.
- **Breaking** changes (renamed field, removed endpoint,
  changed semantics) require a new version. We support at most
  two versions concurrently; the older version is deprecated
  with a `Sunset` HTTP header and a six-month removal window.

## 3. Authentication

- The API uses **JWT bearer tokens** issued by
  `POST /api/auth/login`.
- The token is sent in the `Authorization: Bearer <token>`
  header on every request.
- Tokens expire after 60 minutes; refresh via
  `POST /api/auth/refresh`.
- The OpenAPI spec is annotated with the `Bearer` security
  scheme; Swashbuckle's UI surfaces the "Authorize" button.

## 4. Content negotiation

- Requests and responses are JSON. The
  `Content-Type: application/json; charset=utf-8` header is
  set on every request and expected on every request body.
- We do not currently support XML, form-urlencoded (except for
  OAuth-style endpoints, when we add them), or any other
  format.
- All timestamps are ISO-8601 UTC (`"2026-07-27T15:00:00Z"`).
- All IDs are GUIDs (UUIDv4) serialized as strings.

## 5. URL design

| Resource | Collection | Single |
|---|---|---|
| Boards | `GET /api/boards` | `GET /api/boards/{boardId}` |
| Cards | `GET /api/boards/{boardId}/cards` | `GET /api/boards/{boardId}/cards/{cardId}` |
| Lists | `GET /api/boards/{boardId}/lists` | `GET /api/boards/{boardId}/lists/{listId}` |
| Members | `GET /api/boards/{boardId}/members` | `GET /api/boards/{boardId}/members/{userId}` |
| Comments | `GET /api/cards/{cardId}/comments` | `GET /api/cards/{cardId}/comments/{commentId}` |
| Attachments | `GET /api/cards/{cardId}/attachments` | `GET /api/cards/{cardId}/attachments/{attachmentId}` |

- Collections support `?page=1&pageSize=20` for pagination.
  The response wraps the list:
  ```json
  {
    "items": [...],
    "page": 1,
    "pageSize": 20,
    "totalItems": 137,
    "totalPages": 7
  }
  ```
- Single resources are returned directly (not wrapped).
- Filtering is by query string: `?assigneeId=...&label=...`.
- Sorting is by `?sort=-createdAt,name` (prefix `-` for
  descending).

## 6. HTTP verbs

| Verb | Use | Idempotent? | Safe? |
|---|---|---|---|
| `GET` | Read a resource or collection | yes | yes |
| `POST` | Create a new resource | no | no |
| `PUT` | Replace a resource entirely | yes | no |
| `PATCH` | Apply a partial update | no | no |
| `DELETE` | Remove a resource | yes | no |

We use `PUT` for "rename" / "move" / "set property" — anything
where the entire new state is sent. We use `PATCH` (with
JSON Merge Patch, RFC 7396) for "update a subset of fields".

## 7. Status codes

| Code | When |
|---|---|
| 200 OK | Read or update succeeded; body is the resource. |
| 201 Created | Resource created; body is the resource; `Location` header points to it. |
| 204 No Content | Resource deleted or updated without a body. |
| 400 Bad Request | Request body is malformed or fails validation. |
| 401 Unauthorized | No / invalid / expired token. |
| 403 Forbidden | Authenticated but not allowed. |
| 404 Not Found | Resource doesn't exist. |
| 409 Conflict | Version conflict (optimistic concurrency) or unique-constraint violation. |
| 422 Unprocessable Entity | Body is well-formed but domain rules reject it (e.g. renaming an archived board). |
| 429 Too Many Requests | Rate limit hit. |
| 500 Internal Server Error | Unexpected server-side failure. The response includes a `traceId` for correlation. |

## 8. Error responses

Errors are returned as `application/problem+json` (RFC 7807):

```json
{
  "type": "https://docs.cardscape.io/errors/board-not-found",
  "title": "Board not found",
  "status": 404,
  "detail": "No board exists with id '5f3a...'.",
  "instance": "/api/boards/5f3a...",
  "traceId": "00-abc-def-00",
  "errors": {
    "newName": ["New name is required."]
  }
}
```

- `type` is a stable URL that documents the error class. New
  error types get a new URL; existing error types never
  change.
- `errors` is present only on validation failures and
  enumerates field-level issues.
- `traceId` is the W3C trace context id. The server logs the
  same id; clients should include it in bug reports.

## 9. Validation

- `400 Bad Request` for body shape issues (missing required
  field, wrong type, malformed JSON).
- `422 Unprocessable Entity` for domain-rule violations (e.g.
  renaming a board to a name that already exists in the
  workspace).

The two are deliberately separate so the client can react
differently: a 400 means "fix the request", a 422 means "the
request is fine but the operation is not allowed right now".

## 10. Rate limiting

A sliding-window rate limit applies to all endpoints:

| Identity | Limit |
|---|---|
| Anonymous | 60 requests / minute / IP |
| Authenticated | 600 requests / minute / user |
| Authenticated, board admin | 6000 requests / minute / user |

`429 Too Many Requests` is returned with a `Retry-After`
header. The exact limits are configurable.

## 11. Caching

- `GET` endpoints that return a single resource include
  `ETag` and `Last-Modified` headers. Clients SHOULD send
  `If-None-Match` / `If-Modified-Since`; the server responds
  with `304 Not Modified` if the resource hasn't changed.
- `GET` endpoints that return a collection include
  `Cache-Control: max-age=10, must-revalidate` by default.
  Specific endpoints may override this.
- `POST`, `PUT`, `PATCH`, `DELETE` responses include
  `Cache-Control: no-store`.

## 12. Versioning of the OpenAPI document

The OpenAPI document is published at `/swagger/v1/swagger.json`
(Development only) and at a public URL once the API is
stable. Breaking changes bump to `/swagger/v2/swagger.json`
etc.

## 13. CORS

- The Api is configured with CORS in Development to accept
  requests from `https://localhost:7001` (the Blazor WASM
  dev server).
- In Production, the allowed origin is configurable via
  `Cors:AllowedOrigins`.

## 14. Examples

### Create a board

```http
POST /api/boards
Authorization: Bearer <token>
Content-Type: application/json

{
  "workspaceId": "5f3a0000-0000-0000-0000-000000000001",
  "name": "Q3 Roadmap",
  "description": "Roadmap items for Q3",
  "visibility": "private"
}
```

```http
201 Created
Location: /api/boards/5f3a0000-0000-0000-0000-0000000000ab
Content-Type: application/json

{
  "id": "5f3a0000-0000-0000-0000-0000000000ab",
  "workspaceId": "5f3a0000-0000-0000-0000-000000000001",
  "name": "Q3 Roadmap",
  "description": "Roadmap items for Q3",
  "visibility": "private",
  "createdAt": "2026-07-27T15:00:00Z",
  "createdBy": "5f3a0000-0000-0000-0000-0000000000cd",
  "version": 1
}
```

### Rename a board

```http
PUT /api/boards/5f3a0000-0000-0000-0000-0000000000ab/name
Authorization: Bearer <token>
Content-Type: application/json

{
  "newName": "Q3 Roadmap (revised)"
}
```

```http
200 OK
Content-Type: application/json

{ ...full board DTO as above, with name replaced... }
```

### List cards, filtered and paginated

```http
GET /api/boards/5f3a0000-0000-0000-0000-0000000000ab/cards?assigneeId=5f3a0000-0000-0000-0000-0000000000cd&label=urgent&page=1&pageSize=20&sort=-dueDate
Authorization: Bearer <token>
```

```http
200 OK
Content-Type: application/json

{
  "items": [ ...20 card DTOs... ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 137,
  "totalPages": 7
}
```

## 15. References

- [RFC 7807 — Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc7807)
- [RFC 7396 — JSON Merge Patch](https://www.rfc-editor.org/rfc/rfc7396)
- [Microsoft — REST API design guidelines](https://learn.microsoft.com/azure/architecture/best-practices/api-design)
- [Microsoft — Web API design best practices](https://learn.microsoft.com/azure/architecture/microservices/design/good-api-design)
