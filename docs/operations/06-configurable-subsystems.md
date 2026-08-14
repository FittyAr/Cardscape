# Configurable subsystems

This page is the operator-facing reference for the runtime-tunable
subsystems that were once hard-coded. Every option below is
operator-facing configuration — no code change, no recompile, no
container rebuild. The defaults are tuned for the most common
deployment (single instance, no extra infrastructure) and the
opt-ins are documented alongside the trade-off they make.

The full configuration surface lives under the `Cardscape:`
section of `appsettings.json` (or the equivalent
`Cardscape__…` environment variable). The relevant subsections are
`Api:AdminAuthorization`, `Infrastructure:RateLimiter`,
`Infrastructure:PendingTotpStore`, and
`Infrastructure:Redis`.

The Blazor WebAssembly client has its own configuration file at
`src/Cardscape.Web/wwwroot/appsettings.json` and its own
feature-toggle section called `Features:` (see
[§ Experimental features](#experimental-features-web-ui-gates)
below). The two configuration trees are intentionally separate:
the WASM client does not share configuration with the API host.

```json
{
  "Cardscape": {
    "Api": {
      "AdminAuthorization": {
        "CacheAdminClaim": true
      }
    },
    "Infrastructure": {
      "Redis": {
        "ConnectionString": "redis-prod-01:6379,abortConnect=false",
        "Database": 0
      },
      "RateLimiter": {
        "Backend": "Redis",
        "KeyPrefix": "cardscape:rl:"
      },
      "PendingTotpStore": {
        "Backend": "Redis",
        "KeyPrefix": "cardscape:totp-pending:"
      }
    }
  }
}
```

---

## Admin authorization

**Key**: `Cardscape:Api:AdminAuthorization:CacheAdminClaim`
**Default**: `true`
**Applies to**: the API host only (the MCP server uses API-token
bearer, not the `AdminOnly` policy).

The `/api/admin/*` endpoints (GDPR data-subject requests, the
admin-only telemetry page, the audit-log export, the MCP
subscription snapshot) gate on the `is_admin` claim that
`JwtTokenService` embeds in the access token at mint time. Two
operator postures are supported:

### `CacheAdminClaim = true` (default)

The handler reads the `is_admin` claim out of the JWT and trusts
it as a snapshot of the user's admin status at login. **No
database read on the hot path**. The claim is mandatory in this
mode: a missing or malformed value fails closed with 403 and is
never repaired through a database compatibility lookup. The
trade-off is that a freshly
revoked (or freshly granted) admin status does not take effect
until the affected user's access token expires — by default, 60
minutes. The user can also trigger the change by logging out and
back in.

This is the recommended posture for almost every deployment: the
DB cost on every admin check is non-trivial, and an admin
revocation that takes an hour to propagate is acceptable for
almost every real-world incident. Pair it with a short
`Jwt:AccessTokenMinutes` (e.g. 15) if you want faster
propagation without paying the per-request DB cost.
The value is validated at startup and must be between 5 and
1,440 minutes. JWT issuer and audience must be non-empty. The API
host separately requires an HMAC signing key containing at least
32 UTF-8 bytes; non-API hosts are not forced to receive that secret.

### `CacheAdminClaim = false`

The handler **always** reads `users.IsAdmin` from the database.
Admin revocations take effect on the very next request. The cost
is one indexed row seek per `/api/admin/*` request; on a small
admin surface this is invisible, on a hot path with many admins
it adds up.

Recommended for: deployments subject to a compliance regime that
requires admin revocation to be immediate (e.g. PCI-DSS, certain
FedRAMP controls), or for short-lived incident-response
configurations where the operator flips the flag during the
incident and flips it back when it's over.

---

## Rate-limiter backend

**Key**: `Cardscape:Infrastructure:RateLimiter:Backend`
**Default**: `InMemory`
**Values**: `InMemory` | `Redis`

The per-API-token rate limiter sits in front of every
authenticated API-token request. Two implementations are
available:

### `Backend = "InMemory"` (default)

A per-process token-bucket dictionary. Each API instance owns its
own buckets. The effective global rate limit is therefore
`(per-token rate) × (instance count)` — a 1000-req/hour token
routed to two API instances gets up to 2000 req/hour. This is
acceptable for the soft guard the rate limiter is meant to be:
its job is to prevent runaway clients, not to enforce a hard
quota.

**Pros**: zero dependencies, no network round-trips on the hot
path, works in a single-instance deploy with no extra config.

**Cons**: per-instance buckets, lost on process restart (a
restarted instance starts every bucket back at full burst).

### `Backend = "Redis"`

A token-bucket implementation backed by a Lua script. The
refill + consume is atomic in a single round trip, so concurrent
requests from multiple API instances see the same budget.
Configure the Redis connection once (see below) and flip the
flag; the `IConnectionMultiplexer` is shared with the
pending-2FA-token store if you also flip that one.

**Pros**: one bucket per token across the whole deployment,
survives instance restarts (as long as the bucket has not yet
refilled to full, in which case the read-through simply seeds
it), works correctly behind a load balancer.

**Cons**: every API-token request is now a Redis round trip; a
Redis outage degrades the limiter to "allow all" (fail-open
posture) so a monitoring gap can hide until a real traffic spike
hits. The limiter's behaviour on Redis failure is logged as a
warning — wire it into your alerting.

The Lua script supports runtime reconfiguration: a PATCH to the
rate limit on a token is picked up on the very next request
without an instance restart.

---

## Pending 2FA store backend

**Key**: `Cardscape:Infrastructure:PendingTotpStore:Backend`
**Default**: `InMemory`
**Values**: `InMemory` | `Redis`

The two-step 2FA login mints a one-shot `PendingTotpToken` at the
end of the password step and consumes it at the start of the
TOTP step. The token has a 5-minute lifetime.

### `Backend = "InMemory"` (default)

A per-process `ConcurrentDictionary`. The store lives in
whatever process handled the password step, so a load-balanced
deployment must either pin the password step and the TOTP step
to the same instance (sticky sessions on the login endpoints) or
accept that the TOTP submission may fail with "invalid code" if
the request lands on a different instance.

**Pros**: zero dependencies, no network round-trips, no cross-instance
state to reason about.

**Cons**: in a multi-instance deploy without sticky sessions,
TOTP logins can fail intermittently. The token is lost on an
in-place restart of the API.

### `Backend = "Redis"`

The token is stored as a Redis key with a 5-minute TTL; the TOTP
step reads + deletes it atomically with `GETDEL`, so two
concurrent TOTP submissions for the same challenge cannot both
succeed. The store is independent of which API instance handled
which step.

**Pros**: any API instance can complete any 2FA challenge,
survives an in-place restart (the TTL takes care of cleanup), a
Redis outage on `Consume` refuses the TOTP submission (fail-closed
posture — a duplicate-consumption attack is a worse failure mode
than a denied login).

**Cons**: every 2FA login now costs one Redis round trip per
step. A Redis outage during the password step raises an
exception, which the operator should see in their error
dashboard.

---

## Redis connection settings

**Key**: `Cardscape:Infrastructure:Redis:ConnectionString`
**Default**: `null`
**Required when**: at least one `Backend` is set to `Redis`.

A standard StackExchange.Redis connection string. The
`abortConnect=false` flag is recommended so the multiplexer keeps
trying to connect in the background instead of throwing at
startup. The host parses the string once at composition time and
rejects an empty connection string with a clear error if a
backend asked for Redis.

**Key**: `Cardscape:Infrastructure:Redis:Database`
**Default**: `0`

Logical key-database index. Use this to share one Redis instance
between Cardscape and other applications without colliding on
key names.

**Key prefixes**: `KeyPrefix` on `RateLimiter` and
`PendingTotpStore` default to `cardscape:rl:` and
`cardscape:totp-pending:` respectively. The prefix lets multiple
Cardscape deployments share a single Redis instance — set
distinct prefixes per deployment.

---

## Experimental features (Web UI gates)

**Key**: `Features:DataResidencyEnabled` (and siblings)
**Scope**: the Blazor WebAssembly client only
(`src/Cardscape.Web/wwwroot/appsettings.json`). These are
**client-side** toggles — they do not live under `Cardscape:`
because the WASM host loads them from its own `appsettings.json`,
not from the API's configuration tree. The override
mechanism is the same: the WASM configuration provider reads
`appsettings.json` plus environment variables (`Features__…`).

These flags are **opt-in** by design. Each one hides a slice
of the Web UI that wraps a server-side capability that is
already in place but not yet considered "done" — the
implementation is fully alive, just relegated to operators
who actively want to expose it. The default is **off** and the
corresponding UI affordances do not render at all (the page
behaves as if the feature did not exist).

### `DataResidencyEnabled = false` (default)

Data residency (§4.5 of the v1.1.0 plan) is implemented end to
end on the server side: the `Region` enum, the
`Workspace.Region` / `SetRegion` / `GuardRegion` aggregate
methods, the `IDeploymentRegion` / `ConfigurationDeploymentRegion`
abstraction, the `RegionGuardEndpointFilter` wired into the
workspace endpoint group, the migration that adds the column,
and the API endpoints to set the region on an existing
workspace are all in place. The cross-region write rejection
is the only piece of the spec that is enforced today.

The **UI** exposure of all of the above is gated by this
flag. With the default value:

- The region selector does not appear in the workspace
  creation form (`Pages/Workspaces.razor`).
- The region badge does not appear on the workspace cards.
- A workspace created through the UI is stored with
  `Region.Unspecified`, which the domain treats as
  "accept any deployment region" (see
  `Workspace.GuardRegion(Region)` — `Unspecified` short-circuits
  to success). This is the same behaviour every
  single-instance self-host has always had; the flag merely
  stops the UI from inviting the user to pick a region that
  the rest of the stack does not act on.

Set the flag to `true` to surface the dropdown and the badge
again. **No restart of the API is required for the Web to
pick the new value up** — the WASM host reads
`wwwroot/appsettings.json` at startup, and the Web reload
(on next page navigation) reads the new value. The server-side
behaviour is unaffected in both directions: toggling the flag
changes the UI surface, not the policy.

The rationale for defaulting to off is recorded in
[`docs/roadmap/07-kanban-enterprise-parity.md`](../roadmap/07-kanban-enterprise-parity.md#data-residency);
the short version is that data residency as a user-facing
concept only makes sense on a multi-region deployment with
real cross-region enforcement, and the only currently shipped
cross-region enforcement is the single check in
`RegionGuardEndpointFilter`. Until the rest of the
cross-region story (per-resource storage backend pinning,
per-region read replicas, the GDPR Article 30 narrative that
ties the deployment's region to a documented sub-processor
list) is built, the selector is a foot-gun, not a feature.

The flag is **not** a feature flag in the
`docs/design/06-feature-flags.md` sense — it is a static
configuration value read once at component construction, not
a runtime-evaluated per-user / per-workspace gate. Future
client-side flags will live in the same `Features:` section
of `wwwroot/appsettings.json` and follow the same shape:
declared, defaulted to `false`, surfaced in the Blazor markup
with an `@if` block around the gated control(s).

---

## Environment-variable override

Every option in this page is overridable by environment
variable using the standard .NET configuration key
transformation: the `:` separator becomes `__`. Examples:

```bash
# Disable the admin-claim cache (every /api/admin/* hits the DB)
export Cardscape__Api__AdminAuthorization__CacheAdminClaim=false

# Switch the rate limiter to Redis with a specific connection string
export Cardscape__Infrastructure__RateLimiter__Backend=Redis
export Cardscape__Infrastructure__Redis__ConnectionString="redis-prod-01.internal:6379,abortConnect=false"

# Switch the 2FA token store to Redis
export Cardscape__Infrastructure__PendingTotpStore__Backend=Redis
```

Environment variables take precedence over the JSON file, so the
recommended pattern is to keep the JSON file with the safe
defaults and override only what you need to change at deploy
time (via `docker-compose`, Kubernetes ConfigMap, etc.).

---

## Verifying the configuration at startup

The application validates every value above at composition
time. A misconfiguration is a loud `InvalidOperationException`,
not a silent fallback:

- `Backend = "Redis"` without a connection string → fails.
- An unrecognised `Backend` value → fails.
- A `Jwt:SigningKey` shorter than 32 bytes outside the
  `Development` environment → fails.
- A `Jwt:SigningKey` left at the dev default outside the
  `Development` environment → fails.

The application also refuses to start with an HTTP request
bound to an internal address — the webhook SSRF guard rejects
loopback, link-local, and private-range targets at the
domain layer, so a misconfigured webhook URL never reaches the
dispatcher. See `docs/security/01-threat-model.md` for the
full threat model.
