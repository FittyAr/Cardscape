# SQLite backup and restore

This runbook covers the Cardscape SQLite database, attachment storage, configuration, and Data Protection keys. A backup is complete only after a successful restore test.

## Backup set

Preserve these items together:

- the SQLite database configured by `ConnectionStrings__Default`;
- the attachment directory configured by `Storage__LocalRoot`;
- ASP.NET Core Data Protection keys;
- deployment configuration and secrets through the operator's secret manager.

## Create a consistent database backup

Stop writes before copying the database. For the Docker deployment:

```bash
docker compose stop api
docker compose cp api:/app/Data/cardscape.db ./cardscape.db
docker compose start api
gzip -9 cardscape.db
```

Store the resulting archive off-host with encryption and retention appropriate to the deployment. Back up attachments and Data Protection keys in the same recovery point.

## Restore

1. Provision a clean Cardscape deployment at the same application version as the backup.
2. Stop the API.
3. Restore the database file, attachments, and Data Protection keys to their configured paths.
4. Restore configuration from the secret manager.
5. Start the API and verify health, authentication, attachment access, and one read/write board workflow.
6. Upgrade the application only after the restored version is healthy; EF Core then applies newer migrations.

Example database restore:

```bash
gunzip cardscape.db.gz
docker compose stop api
docker compose cp ./cardscape.db api:/app/Data/cardscape.db
docker compose start api
```

## Verification schedule

Run a restore drill at least quarterly on an isolated host. Record the recovery point, duration, application version, migration result, smoke-test result, and any missing artifact. A copied file that has never been restored is not a verified backup.

## Safety rules

- Never overwrite the active database while the API is running.
- Never store backups only on the same disk as the live instance.
- Never omit Data Protection keys: encrypted integration credentials depend on them.
- Never restore production data into a shared development environment.
