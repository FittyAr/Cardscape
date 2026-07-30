# OAuth 2.0 / OIDC for third-party apps

> Cardscape exposes an OAuth 2.0 authorization-code flow so
> third-party apps can act on behalf of a Cardscape user
> without ever seeing the user's password. This page
> documents the protocol end-to-end; for the runtime API
> reference see the OpenAPI document at
> `artifacts/openapi/openapi.json`.

## Overview

A user installs a third-party app. The app wants to read
the user's boards and create cards. The user trusts the app
enough to grant it access — but doesn't trust it with their
Cardscape password.

The third-party app is **registered** by the user in
Cardscape (this step happens once, in the Cardscape Web
client under `Settings → OAuth apps`). At registration time
the user gets back a `clientId` + a one-time
`clientSecret`. The app keeps the secret.

After that, every time the app needs access it walks the
standard **authorization-code flow**:

1. The app sends the user to
   `https://app.cardscape.example/oauth/authorize?client_id=...&redirect_uri=...&scope=...&state=...`.
2. The user authenticates with Cardscape (or is already
   logged in).
3. Cardscape issues a one-shot `code` and redirects the
   user back to the app's `redirect_uri` with
   `?code=...&state=...`.
4. The app POSTs the code to `/oauth/token` along with its
   `clientId` + `clientSecret` + `redirect_uri`.
5. Cardscape returns a Bearer access token.
6. The app uses the Bearer on `Authorization: Bearer ...`
   for subsequent API calls.
7. `/oauth/userinfo` returns the user's projection for
   the access token's scopes.

## Endpoints

| Endpoint | Method | Auth | Purpose |
|---|---|---|---|
| `/api/oauth-apps` | `GET` | JWT | List the caller's registered apps |
| `/api/oauth-apps` | `POST` | JWT | Register a new app (returns the `clientSecret` exactly once) |
| `/api/oauth-apps/{id}` | `DELETE` | JWT | Revoke a registered app |
| `/oauth/authorize` | `GET` | Cardscape session | Issue a one-shot authorization code |
| `/oauth/token` | `POST` | form-encoded `client_id` + `client_secret` | Exchange a code for a Bearer |
| `/oauth/revoke` | `POST` | form-encoded `client_id` + `client_secret` | Revoke a Bearer (RFC 7009) |
| `/oauth/userinfo` | `GET` | Bearer | Get the user's projection for the token |

## Scopes

| Scope | Allows |
|---|---|
| `cards.read` | Read cards / lists / boards |
| `cards.write` | Create / update / move / archive cards |
| `boards.read` | Read boards and their members |
| `boards.write` | Create / update boards |
| `comments.write` | Add / edit comments on cards |
| `webhooks.read` | List the user's webhooks |
| `webhooks.write` | Create / delete webhooks |
| `admin` | Workspace-level administrative operations |

The MCP server's long-lived `ApiToken` is also a
grantable credential (one of the OAuth scopes is
`api_token`); see the MCP server docs for the bootstrap
story.

## Full handshake example

### 1. Register the app

```http
POST /api/oauth-apps
Authorization: Bearer <user's JWT>
Content-Type: application/json

{
  "name": "My CLI for Cardscape",
  "allowedScopes": ["cards.read", "boards.read"],
  "redirectUris": ["http://127.0.0.1:8400/callback"]
}
```

The response carries the cleartext `clientSecret` exactly
once:

```json
{
  "id": "9b1a4f1e-...",
  "clientId": "c9JZq7vW...",
  "clientSecret": "Q1w2e3r4t5y6u7i8o9p0...",
  "secretPrefix": "Q1w2e3r4"
}
```

The app MUST store the `clientSecret` (e.g. in the OS
keychain) and forget the placeholder. The server only
keeps a SHA-256 hash + the 8-char prefix for display.

### 2. Redirect the user to /oauth/authorize

```http
GET /oauth/authorize
  ?client_id=c9JZq7vW
  &redirect_uri=http://127.0.0.1:8400/callback
  &scope=cards.read+boards.read
  &state=opaque-csrf-token
```

The user sees the Cardscape consent page. On consent the
server redirects the browser to:

```http
GET http://127.0.0.1:8400/callback
  ?code=one-shot-code
  &state=opaque-csrf-token
```

The app verifies that the `state` round-tripped correctly
(CSRF protection) and extracts the `code`.

### 3. Exchange the code for a Bearer

```http
POST /oauth/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&code=one-shot-code
&client_id=c9JZq7vW
&client_secret=Q1w2e3r4t5y6u7i8o9p0...
&redirect_uri=http://127.0.0.1:8400/callback
```

The response is the standard OAuth 2.0 token endpoint
response:

```json
{
  "access_token": "long-lived-bearer-...",
  "token_type": "Bearer",
  "expires_in": 2592000,
  "scope": "cards.read boards.read"
}
```

The `code` is now consumed (one-shot). The access token
is valid for 30 days.

### 4. Call the API with the Bearer

```http
GET /api/boards
Authorization: Bearer long-lived-bearer-...
```

The server validates the Bearer via
`IOAuthAppService.ValidateAccessTokenAsync` and translates
it into the standard ASP.NET Core `ClaimsPrincipal` (with
the user's id, the app id, and the granted scopes).

### 5. Get the user projection

```http
GET /oauth/userinfo
Authorization: Bearer long-lived-bearer-...
```

```json
{
  "sub": "9b1a4f1e-...",
  "email": "alice@example.com",
  "name": "Alice"
}
```

### 6. Revoke the token

```http
POST /oauth/revoke
Content-Type: application/x-www-form-urlencoded

token=long-lived-bearer-...
&client_id=c9JZq7vW
&client_secret=Q1w2e3r4t5y6u7i8o9p0...
```

Per RFC 7009, the server returns 200 even if the token
was unknown so it doesn't leak which tokens exist.

## Token lifetimes

| Token | Lifetime | Stored as |
|---|---|---|
| `clientId` | Until the app is revoked | Cleartext |
| `clientSecret` | Until the app is revoked | SHA-256 hash on the server; cleartext returned once at issue time |
| Authorization code | 5 minutes | SHA-256 hash; one-shot |
| Access token | 30 days | SHA-256 hash; revocable |

## Error responses

OAuth error responses use the standard `error` +
`error_description` shape:

```json
{
  "error": "invalid_grant",
  "error_description": "The authorization code has already been exchanged for an access token."
}
```

| `error` | When |
|---|---|
| `invalid_request` | A required parameter is missing |
| `invalid_client` | The `client_id` / `client_secret` are wrong or the app is revoked |
| `invalid_grant` | The code is unknown / expired / already consumed |
| `unsupported_grant_type` | Only `authorization_code` is supported in v1.1.0 |
| `invalid_scope` | The scope isn't one of the registered scopes for the app |

## What's NOT in v1.1.0

- **Refresh tokens** — re-authorize when the 30-day
  access token expires. Adding refresh tokens is tracked
  separately; the response carries `refresh_token: null`
  for now.
- **PKCE** — the redirect URI list is the protection
  against code interception today. PKCE is planned once
  public clients (mobile apps, CLIs without a secret)
  become a first-class use case.
- **Device code flow** — same reason; will be added when
  needed.
- **Dynamic client registration** — apps are registered
  manually by a user under `Settings → OAuth apps`. A
  self-service registration endpoint is on the roadmap.
