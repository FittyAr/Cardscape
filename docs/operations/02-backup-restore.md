# Backup and restore

> The backup and restore procedure for a self-hosted
> Cardscape instance. The procedure covers the three
> database providers (SQLite, PostgreSQL, MariaDB), the
> attached volumes, and the `OTel` and `Smtp`
> configuration. The procedure is meant to be run on a
> schedule (cron, systemd timer, or a managed backup
> service) and tested regularly (at least once per
> quarter).
>
> This is a **runbook**. It is meant to be followed step by
> step, in order, during a backup or a restore.

---

## 1. The principle

A backup is only as good as the **restore**. The maintainer
**tests the restore quarterly**, on a non-production host,
to confirm the backup is actually restorable. A backup
that has not been tested is not a backup; it is a hope.

The backup strategy is:

- **Daily full backup** of the database.
- **Hourly incremental backup** of the attachments volume
  (added in Phase 1+ when attachments are persisted to
  the volume; until then, the backup is the database only).
- **Weekly full backup** of the entire deployment directory
  (config, secrets, OTel config, the `docker-compose.yml`).
- **Retention**: 30 days for daily backups, 90 days for
  weekly backups, 1 year for monthly backups.
- **Storage**: off-host (S3, Backblaze B2, Azure Blob, or
  a similar object storage service). The backup is encrypted
  at rest (the storage service's server-side encryption is
  enough for most cases; client-side encryption with
  age or gpg is added for compliance-sensitive
  deployments).

---

## 2. The SQLite backup

The SQLite database is a single file (`/data/cardscape.db`
in the container, mounted to the `cardscape-data` volume
on the host). The backup is a copy of the file, taken
**with the SQLite online backup API** to avoid corruption.

The script (the user saves this as
`/opt/cardscape/scripts/backup-sqlite.sh`):

```bash
#!/usr/bin/env bash
set -euo pipefail

# Configuration
BACKUP_DIR="/opt/cardscape/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/cardscape-${TIMESTAMP}.db"

# Create the backup directory
mkdir -p "${BACKUP_DIR}"

# Use sqlite3's online backup API (VACUUM INTO is the
# safe way to copy a live SQLite database).
docker compose exec -T api \
  sqlite3 /data/cardscape.db ".backup '${BACKUP_FILE}'"

# Compress the backup
gzip "${BACKUP_FILE}"

# Upload to off-host storage (replace with the user's
# storage service CLI: aws s3 cp, rclone copy, etc.).
rclone copy "${BACKUP_FILE}.gz" \
  "remote:cardscape-backups/sqlite/"

# Prune local backups older than the retention period
find "${BACKUP_DIR}" -name "cardscape-*.db.gz" \
  -mtime +${RETENTION_DAYS} -delete

# Prune remote backups older than the retention period
rclone delete "remote:cardscape-backups/sqlite/" \
  --min-age ${RETENTION_DAYS}d
```

The script is added to the user's crontab:

```cron
# Daily SQLite backup at 03:00 UTC.
0 3 * * * /opt/cardscape/scripts/backup-sqlite.sh >> /var/log/cardscape-backup.log 2>&1
```

---

## 3. The PostgreSQL backup

The PostgreSQL backup is a logical dump via `pg_dump`. The
script (the user saves this as
`/opt/cardscape/scripts/backup-postgres.sh`):

```bash
#!/usr/bin/env bash
set -euo pipefail

# Configuration
BACKUP_DIR="/opt/cardscape/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/cardscape-${TIMESTAMP}.sql.gz"

# Create the backup directory
mkdir -p "${BACKUP_DIR}"

# Dump the database (compressed)
docker compose exec -T postgres \
  pg_dump --no-owner --no-privileges --clean --if-exists \
  -U cardscape -d cardscape \
  | gzip > "${BACKUP_FILE}"

# Upload to off-host storage
rclone copy "${BACKUP_FILE}" \
  "remote:cardscape-backups/postgres/"

# Prune local and remote backups
find "${BACKUP_DIR}" -name "cardscape-*.sql.gz" \
  -mtime +${RETENTION_DAYS} -delete
rclone delete "remote:cardscape-backups/postgres/" \
  --min-age ${RETENTION_DAYS}d
```

The script is added to the user's crontab:

```cron
# Daily PostgreSQL backup at 03:00 UTC.
0 3 * * * /opt/cardscape/scripts/backup-postgres.sh >> /var/log/cardscape-backup.log 2>&1
```

For a hot-standby setup, the user can also use
`pg_basebackup` for a physical backup (faster for large
databases). The script is a future addition; the
`pg_dump` script is enough for most deployments.

---

## 4. The MariaDB backup

The MariaDB backup is a logical dump via `mariadb-dump`.
The script is the same shape as the PostgreSQL one:

```bash
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="/opt/cardscape/backups"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/cardscape-${TIMESTAMP}.sql.gz"

mkdir -p "${BACKUP_DIR}"

docker compose exec -T mariadb \
  mariadb-dump --single-transaction --quick --routines \
  -u cardscape -p"${DB_PASSWORD}" cardscape \
  | gzip > "${BACKUP_FILE}"

rclone copy "${BACKUP_FILE}" \
  "remote:cardscape-backups/mariadb/"

find "${BACKUP_DIR}" -name "cardscape-*.sql.gz" \
  -mtime +${RETENTION_DAYS} -delete
rclone delete "remote:cardscape-backups/mariadb/" \
  --min-age ${RETENTION_DAYS}d
```

---

## 5. The attachments volume backup

The attachments volume (`cardscape-data` or
`cardscape-attachments`, depending on the deployment) holds
the user's uploaded files. The backup is a file-level copy
of the volume, taken with `restic`, `borgbackup`, or a
similar tool that supports deduplication and encryption.

The script:

```bash
#!/usr/bin/env bash
set -euo pipefail

# Configuration
BACKUP_REPO="sftp:backup-user@backup-host:/backups/cardscape-attachments"
RETENTION_DAYS=90

# Initialize the restic repository (first run only)
# restic -r "${BACKUP_REPO}" init

# Back up the attachments volume
restic -r "${BACKUP_REPO}" backup \
  /var/lib/docker/volumes/cardscape-cardscape-attachments/_data

# Prune old snapshots
restic -r "${BACKUP_REPO}" forget \
  --keep-daily 30 --keep-weekly 12 --keep-monthly 12 \
  --prune
```

The script is added to the user's crontab:

```cron
# Daily attachments backup at 04:00 UTC (after the DB backup).
0 4 * * * /opt/cardscape/scripts/backup-attachments.sh >> /var/log/cardscape-backup.log 2>&1
```

---

## 6. The configuration backup

The configuration is the `docker-compose.yml`, the
`.env` file, the OTel collector config, the Caddy /
nginx config, and the backup scripts themselves. The
backup is a `tar` of the entire `/opt/cardscape`
directory, **excluding the database volume** (which is
backed up by the database-specific scripts).

The script:

```bash
#!/usr/bin/env bash
set -euo pipefail

BACKUP_DIR="/opt/cardscape/backups"
RETENTION_DAYS=365
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/config-${TIMESTAMP}.tar.gz"

# Back up the config (excluding the database backups themselves
# and the docker volumes, which are backed up by the other scripts)
tar --exclude='./backups' \
    --exclude='./volumes' \
    --exclude='./.git' \
    -czf "${BACKUP_FILE}" \
    -C /opt/cardscape .

# Encrypt the backup (the .env file contains secrets)
gpg --symmetric --cipher-algo AES256 \
  --output "${BACKUP_FILE}.gpg" \
  "${BACKUP_FILE}"
rm "${BACKUP_FILE}"

# Upload to off-host storage
rclone copy "${BACKUP_FILE}.gpg" \
  "remote:cardscape-backups/config/"

# Prune old backups
find "${BACKUP_DIR}" -name "config-*.tar.gz.gpg" \
  -mtime +${RETENTION_DAYS} -delete
rclone delete "remote:cardscape-backups/config/" \
  --min-age ${RETENTION_DAYS}d
```

---

## 7. The restore procedure

The restore is run when the user needs to recover from a
disaster (database corruption, host failure, accidental
deletion, ransomware). The steps:

1. **Choose the recovery point.** The user picks a backup
   timestamp (the daily backup at 03:00 UTC is the typical
   choice; for a more recent point, the hourly attachments
   backup, if enabled).
2. **Provision a fresh host** (or a fresh Docker context on
   the same host). The new host is a clean Cardscape
   deployment, with no data.
3. **Download the backup** from the off-host storage to
   the new host.
4. **Restore the database** from the backup file. For
   SQLite:
   ```bash
   gunzip cardscape-20260727-030000.db.gz
   docker compose cp cardscape-20260727-030000.db \
     api:/data/cardscape.db
   docker compose restart api
   ```
   For PostgreSQL:
   ```bash
   gunzip cardscape-20260727-030000.sql.gz
   cat cardscape-20260727-030000.sql \
     | docker compose exec -T postgres \
       psql -U cardscape -d cardscape
   ```
5. **Restore the attachments** from the restic snapshot.
   ```bash
   restic -r "${BACKUP_REPO}" restore latest \
     --target /var/lib/docker/volumes/cardscape-cardscape-attachments/_data
   ```
6. **Restore the configuration** from the encrypted
   config backup.
   ```bash
   gpg --decrypt config-20260727-030000.tar.gz.gpg \
     | tar -xzf - -C /opt/cardscape
   ```
7. **Start the stack** and verify.

The restore is logged in the `AuditLog` (Phase 4+) with
the actor (the user performing the restore), the
timestamp, the backup timestamp used, and the success /
failure result.

---

## 8. The quarterly restore test

The maintainer recommends testing the restore **at least
once per quarter**. The test is on a **non-production**
host; it does not affect the production deployment. The
steps:

1. **Provision a non-production host** (a cloud VM, a local
   VM, or a Docker context on a laptop).
2. **Download the most recent backup** from the off-host
   storage.
3. **Restore the database, the attachments, and the
   configuration** following the procedure in §7.
4. **Start the stack** and sign in.
5. **Verify the data**: the boards, the cards, the
   attachments, the user accounts.
6. **Compare the data with the production** (a sample
   board, a sample card, a sample attachment) and confirm
   the data is identical.
7. **Document the test**: the date, the backup used, the
   time-to-restore, any issues encountered.
8. **File issues** for any gaps in the restore procedure.

The test confirms the backup is restorable. A backup that
has not been tested is not a backup; it is a hope.

---

## 9. The encryption key management

The config backup is encrypted with GPG symmetric
encryption. The passphrase is stored in the user's
password manager (1Password, Bitwarden, KeePass) under
"Cardscape backups". The passphrase is **not** in the
configuration backup itself.

For deployments with multiple operators, the GPG key is
asymmetric: the key is generated on a secure host, the
public key is used to encrypt, the private key is held by
each operator. The private key is rotated annually.

---

## 10. The compliance considerations

For deployments that must comply with SOC 2, GDPR, HIPAA,
or similar regulations:

- **The backup is encrypted at rest** (the storage
  service's server-side encryption is enough for SOC 2
  and GDPR; HIPAA requires client-side encryption with
  the key held by the user).
- **The backup is encrypted in transit** (TLS for the
  storage service's API).
- **The backup retention** is configurable per the
  regulation. GDPR's "right to erasure" requires that
  the backup is purged when a user is deleted; the
  prune script in §2 / §3 is run after the user
  deletion.
- **The backup access is logged** (the storage service's
  access log is enough for SOC 2; HIPAA requires the
  access log to be retained for 6 years).
- **The restore is tested** (the quarterly test in §8).

The maintainer does not provide a compliance certification;
the user is responsible for their own compliance posture.

---

## 11. Anti-patterns (do not do this)

- **A backup that has not been tested** — the maintainer
  is not the only one who has learned this the hard way.
  Test the restore quarterly.
- **A backup on the same disk as the data** — if the
  disk dies, both the data and the backup are gone. The
  off-host storage is not optional.
- **A backup without encryption** — the database contains
  user data; the attachments volume contains user files.
  The backup is encrypted at rest.
- **A backup without retention** — the backup grows
  forever. The retention policy in §1 is the default; the
  user can configure it.
- **A backup without monitoring** — a backup that silently
  fails is worse than no backup. The cron output is logged
  to a file; the user is alerted on failure (e.g. with
  Healthchecks.io).
- **A backup without a documented procedure** — the
  restore procedure is in this document. The user follows
  it; the maintainer maintains it.

---

## 12. When to revisit

This document is revisited when:

1. A new database provider is added (the script for the
   new provider is added to §2 / §3 / §4).
2. A new compliance requirement is added (SOC 2, GDPR,
   HIPAA, etc.).
3. A new backup storage service is added (the script
   changes).
4. A real restore reveals a gap in the procedure.

Until then, this document is the source of truth for
backup and restore in Cardscape.
