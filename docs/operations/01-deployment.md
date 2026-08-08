# Deployment

> The deployment guide for a self-hosted Cardscape
> instance. The guide covers the **happy path**: a single
> host, SQLite, the REST API, the Blazor WASM client, and
> (in Phase 2+) the MCP server. Production deployments on
> Kubernetes, Docker Swarm, or other orchestrators are
> out of scope for this document; see the
> [production deployment guide](#the-production-deployment-guide)
> for the trade-offs.
>
> This is a **runbook**. It is meant to be followed step by
> step, in order, on a fresh host.

---

## 1. The happy path

The simplest self-hostable Cardscape deployment is a
single Linux host with Docker and Docker Compose. The host
runs the API, the web client, and the database (SQLite
for solo/dev, PostgreSQL for production). The MCP server
shares the same host when Phase 2 ships.

### 1.1 Requirements

- A Linux host (Ubuntu 22.04 LTS or later, Debian 12 or
  later, or any other Linux distribution that Docker
  supports).
- 2 GB of RAM minimum, 4 GB recommended.
- 10 GB of free disk space minimum, 50 GB recommended
  (the database grows with the user's data).
- Docker Engine 24.0 or later.
- Docker Compose v2 (the `docker compose` command, not the
  legacy `docker-compose`).
- A domain name (or a subdomain) pointing at the host's
  IP. The web client requires HTTPS; the API requires
  HTTPS in production.

### 1.2 The `docker-compose.yml`

The maintainer publishes a `docker-compose.yml` at
`https://cardscape.fitty.ar/releases/v0.1.0-mvp/docker-compose.yml`
(the path is added with the first release). The file looks
like:

```yaml
version: "3.8"

services:
  api:
    image: ghcr.io/cardscape/cardscape-api:0.1.0-mvp
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=Sqlite
      - Database__ConnectionString=Data Source=/data/cardscape.db
      - Cardscape__JwtSecret=${JWT_SECRET:?JWT_SECRET is required}
      - Otel__Endpoint=http://otel-collector:4317
    volumes:
      - cardscape-data:/data
    ports:
      - "5000:8080"
    depends_on:
      otel-collector:
        condition: service_started

  web:
    image: ghcr.io/cardscape/cardscape-web:0.1.0-mvp
    restart: unless-stopped
    environment:
      - Cardscape__ApiBaseUrl=https://cardscape.example.com/api/v1
    ports:
      - "5001:8080"

  otel-collector:
    image: otel/opentelemetry-collector-contrib:0.96.0
    restart: unless-stopped
    volumes:
      - ./otel-collector-config.yaml:/etc/otelcol-contrib/config.yaml
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP

volumes:
  cardscape-data:
```

### 1.3 The steps

```bash
# 1. Create a directory for the deployment.
mkdir -p /opt/cardscape && cd /opt/cardscape

# 2. Download the docker-compose.yml (when the first release ships).
curl -O https://cardscape.fitty.ar/releases/v0.1.0-mvp/docker-compose.yml
curl -O https://cardscape.fitty.ar/releases/v0.1.0-mvp/otel-collector-config.yaml

# 3. Generate a strong JWT secret (32+ random bytes, base64).
openssl rand -base64 48

# 4. Create a .env file with the secret and any other config.
cat > .env <<EOF
JWT_SECRET=<paste the secret from step 3>
EOF

# 5. Start the stack.
docker compose up -d

# 6. Verify the stack is up.
docker compose ps
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5001/

# 7. Set up the reverse proxy (see §2).
# 8. Set up the backup (see 02-backup-restore.md).
# 9. Set up the monitoring (see 03-monitoring.md).
```

The web client is at `http://localhost:5001`. The API is
at `http://localhost:5000`. The MCP server (Phase 2+) is
at the same port as the API, on the `/mcp/` path.

---

## 2. The reverse proxy

The reverse proxy terminates TLS and forwards the request
to the API or the web client. **Caddy** is the recommended
choice (it auto-renews Let's Encrypt certificates), but
**nginx** is also supported.

### 2.1 Caddy

```caddyfile
# /etc/caddy/Caddyfile
cardscape.example.com {
    reverse_proxy /api/* api:8080
    reverse_proxy /mcp/* api:8080
    reverse_proxy /* web:8080
}
```

Caddy auto-issues a Let's Encrypt certificate on the first
request. The first start requires port 80 and 443 open to
the internet.

### 2.2 nginx

```nginx
# /etc/nginx/sites-available/cardscape
server {
    listen 443 ssl http2;
    server_name cardscape.example.com;

    ssl_certificate /etc/letsencrypt/live/cardscape.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/cardscape.example.com/privkey.pem;

    location /api/ {
        proxy_pass http://localhost:5000/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /mcp/ {
        proxy_pass http://localhost:5000/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        # SSE support
        proxy_buffering off;
        proxy_cache off;
        proxy_read_timeout 86400s;
    }

    location / {
        proxy_pass http://localhost:5001/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

The `proxy_buffering off` for `/mcp/` is required for SSE
(Server-Sent Events), which the MCP server uses for the
HTTP+SSE transport.

---

## 3. The first-run experience

When the user opens `https://cardscape.example.com` for the
first time, they see the login page. The first account
created is **automatically the workspace admin** (this is
a known sharp edge; a future PR adds a "first-run setup"
wizard).

The first account:

1. Provides an email and a password.
2. The password is validated against the policy
   ([`docs/design/03-auth-and-authz.md`](../design/03-auth-and-authz.md) §9).
3. The account is created in the `Members` context.
4. A default workspace is created (named "My workspace").
5. The user is signed in.
6. The user can create boards.

The first account's email should be the user's real email
(the maintainer's email if it is a personal deployment,
the admin's email if it is a team deployment). The
password should be generated with a password manager (the
policy requires 12+ characters; the maintainer recommends
a passphrase of 4-5 random words).

---

## 4. The configuration

The configuration is in environment variables (the
recommended approach) or in `appsettings.json` (the
fallback). The environment variables use the double-
underscore convention to represent nested configuration
sections:

| Variable | Section | Default | Notes |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | top | `Production` | `Development` for local dev |
| `Database__Provider` | `Database` | `Sqlite` | one of `Sqlite`, `PostgreSQL`, `MariaDB` |
| `Database__ConnectionString` | `Database` | `Data Source=/data/cardscape.db` | provider-specific |
| `Cardscape__JwtSecret` | `Cardscape` | (required) | 32+ random bytes, base64 |
| `Otel__Endpoint` | `Otel` | (none) | OTel collector URL, e.g. `http://otel-collector:4317` |
| `Smtp__Host` | `Smtp` | (none) | for outbound email |
| `Smtp__Port` | `Smtp` | `587` | |
| `Smtp__Username` | `Smtp` | (none) | |
| `Smtp__Password` | `Smtp` | (none) | |
| `Smtp__From` | `Smtp` | (none) | e.g. `noreply@cardscape.example.com` |

The full list is in
[`docs/architecture/00-overview.md`](../architecture/00-overview.md)
when the implementation lands.

---

## 5. The PostgreSQL deployment

For a production deployment, SQLite is replaced with
PostgreSQL. The change is configuration only:

```yaml
services:
  api:
    environment:
      - Database__Provider=PostgreSQL
      - Database__ConnectionString=Host=postgres;Port=5432;Database=cardscape;Username=cardscape;Password=${DB_PASSWORD}
    depends_on:
      postgres:
        condition: service_healthy

  postgres:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      - POSTGRES_DB=cardscape
      - POSTGRES_USER=cardscape
      - POSTGRES_PASSWORD=${DB_PASSWORD:?DB_PASSWORD is required}
    volumes:
      - cardscape-postgres:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U cardscape"]
      interval: 5s
      timeout: 3s
      retries: 5

volumes:
  cardscape-postgres:
```

The `Database__Provider` switch triggers the EF Core
configuration in
[`src/Cardscape.Infrastructure/Persistence/`](../../src/Cardscape.Infrastructure/Persistence/)
to call `UseNpgsql` instead of `UseSqlite`.

> **Known follow-up.** The current EF Core migrations were
> generated with the SQLite design-time factory, so the
> snapshot is SQLite-typed. Switching the runtime provider
> to PostgreSQL trips `PendingModelChangesWarning` at
> startup. The full root cause + one-pass fix are documented
> in
> [`12-postgresql-future-work.md`](12-postgresql-future-work.md).
> Until that pass lands, the documented self-hostable stack
> is SQLite-only.

---

## 6. The MariaDB deployment

Same as PostgreSQL, with `Database__Provider=MariaDB` and
the MariaDB connection string. The image is
`mariadb:11`.

---

## 7. The MCP server deployment (Phase 2+)

The MCP server is the same `api` container in Phase 2+ —
the MCP endpoint is exposed at `/mcp/`. The AI client
connects to `https://cardscape.example.com/mcp/`.

For the **stdio** transport, the AI client runs the MCP
server as a child process. The recommended pattern is
`docker run --rm -i ghcr.io/cardscape/cardscape-mcp:0.2.0-core-mvp`
(the image entrypoint is the MCP server, not a shell).

The Claude Desktop configuration:

```json
{
  "mcpServers": {
    "cardscape": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "-e",
        "Cardscape__ApiBaseUrl=https://cardscape.example.com",
        "-e",
        "Cardscape__ApiToken=<the user's API token>",
        "ghcr.io/cardscape/cardscape-mcp:0.2.0-core-mcp"
      ]
    }
  }
}
```

The API token is created by the user in the web UI
(Settings → API tokens). The token is shown once, at
creation time, and never again.

---

## 8. The production deployment guide

A full production deployment (Kubernetes, high availability,
disaster recovery, multi-region) is out of scope for this
document. The maintainer publishes a production
deployment guide with the first v1.0 release. The guide
covers:

- **Kubernetes manifests** (Deployment, Service, Ingress,
  ConfigMap, Secret, PersistentVolumeClaim).
- **Helm chart** (the maintainer publishes one with the
  v1.0 release).
- **PostgreSQL HA** (Patroni, pg_auto_failover, or a
  managed service like AWS RDS or Cloud SQL).
- **Multi-region** (active-passive with read replicas in
  the secondary region).
- **Disaster recovery** (cross-region backups, RTO/RPO
  targets).
- **Capacity planning** (CPU, memory, IOPS, network).

The guide is added with the v1.0 release. Until then, the
simple Docker Compose setup is the recommended path for
self-hosting.

---

## 9. The upgrade path

The upgrade from one version to the next is:

1. **Read the release notes.** The release notes list the
   breaking changes, the migration steps, and the
   rollback path.
2. **Back up the data** (see
   [`02-backup-restore.md`](02-backup-restore.md)).
3. **Pull the new images.** `docker compose pull`.
4. **Apply the database migration.** `docker compose run
   --rm api dotnet ef database update` (or the equivalent
   for the user's provider).
5. **Restart the stack.** `docker compose up -d`.
6. **Verify the upgrade.** `docker compose ps`, check the
   API health, check the web client, sign in.

The maintainer publishes step-by-step upgrade guides per
release in the
[`docs/development/04-release-process.md`](../development/04-release-process.md).

---

## 10. The rollback path

If the upgrade fails, the rollback is:

1. **Stop the new stack.** `docker compose down`.
2. **Restore the data from the pre-upgrade backup.**
3. **Pull the previous images.** `docker compose pull`
   with the previous tag.
4. **Start the previous stack.** `docker compose up -d`.
5. **Verify the rollback.** Sign in, check the data.

The pre-upgrade backup is the maintainer's responsibility;
see [`02-backup-restore.md`](02-backup-restore.md) for the
backup procedure.

---

## 11. When to revisit

This document is revisited when:

1. The deployment story changes (e.g. a move to a
   different orchestrator, a new release artifact).
2. A new environment is supported (e.g. macOS, Windows
   Server, ARM).
3. A new security requirement (e.g. FIPS compliance,
   CIS benchmarks) imposes a new configuration.
4. A first-time self-hoster reports a step that is
   unclear or wrong.

Until then, this document is the source of truth for
self-hosting Cardscape.
