# Cardscape scripts

PowerShell 7+ helpers for the day-to-day Cardscape workflow. Every
script is self-contained, prints usage when called with `--help`, and
forwards extra args after `--` straight to the underlying tool.

## TL;DR

Two ways in:

```powershell
# Interactive menu (no need to remember anything)
pwsh run.ps1

# Or the dispatcher for direct commands
pwsh scripts/cardscape.ps1 <command> [options]
```

The dispatcher forwards `<command>` to the matching `scripts/<command>.ps1`.
You can also call any script directly:

```powershell
pwsh scripts/test.ps1 -Unit -Coverage
```

## Catalogue

| Command     | What it does                                              |
|-------------|-----------------------------------------------------------|
| `setup`     | First-time environment check (SDK, git, restore, build)  |
| `build`     | Restore + build the solution (`-Release`, `-NoRestore`)   |
| `test`      | Run the test matrix (`-Unit`, `-Integration`, `-Coverage`)|
| `run`       | Run `api` / `web` / `mcp` locally with env overrides      |
| `migrate`   | EF Core migrations: `list`, `apply`, `add`, `script`, `drop`, `remove`, `bundle` |
| `db`        | Database info, `reset`, `open` (sqlite), `tables`         |
| `docker`    | docker-compose helper: `up`, `down`, `logs`, `build`, `ps`|
| `format`    | `dotnet format` (`-Verify` for CI, `-Severity warn`)      |
| `clean`     | Remove `bin/`, `obj/`, `TestResults/`, caches             |

## Conventions

- **PowerShell 7+** (pwsh). Tested on Windows and WSL2.
- **LF line endings, UTF-8** (no BOM). The repo ships LF.
- **`-h` / `--help`** prints usage for every script.
- **Destructive actions** (`migrate drop`, `db reset`, `clean -Database`)
  prompt in interactive shells and require `-Force` in non-TTY contexts.
- **Forward extra args** with `--`:
  ```powershell
  pwsh scripts/cardscape.ps1 test -- --logger "trx;LogFileName=results.trx"
  ```
- **Environment overrides** mirror ASP.NET Core's `__` (double
  underscore) convention. For example, `run -ConnectionString` sets
  `ConnectionStrings__Default` for the child process.

## Quick recipes

```powershell
# First day on the repo:
pwsh run.ps1                                  # or pwsh scripts/cardscape.ps1 setup
pwsh scripts/cardscape.ps1 run api

# Add a new EF migration and apply it:
pwsh scripts/cardscape.ps1 migrate add IssueFooBar
pwsh scripts/cardscape.ps1 migrate apply

# Generate a SQL script for production rollout:
pwsh scripts/cardscape.ps1 migrate script -Output deploy.sql

# Reset the local Sqlite DB and reseed:
pwsh scripts/cardscape.ps1 db reset -Force

# Run only unit tests with coverage:
pwsh scripts/cardscape.ps1 test -Unit -Coverage

# Spin up the development Docker stack:
pwsh scripts/cardscape.ps1 docker up -Dev -Detached
pwsh scripts/cardscape.ps1 docker logs -Service cardscape.api
pwsh scripts/cardscape.ps1 docker down -V   # also drop volumes

# CI gate: fail if the formatter would change anything:
pwsh scripts/cardscape.ps1 format -Verify
```

## File map

```
run.ps1                       # root menu (interactive)
scripts/
├── cardscape.ps1             # dispatcher (forwards <command> to <command>.ps1)
├── _common.ps1               # shared helpers (paths, logging, prereqs)
├── setup.ps1
├── build.ps1
├── test.ps1
├── run.ps1                   # runs api / web / mcp
├── migrate.ps1
├── db.ps1
├── docker.ps1
├── format.ps1
└── clean.ps1
```

## Adding a new script

1. Create `scripts/<name>.ps1`.
2. Dot-source `_common.ps1` for paths/logging helpers.
3. Use `Write-Step` / `Write-Info` / `Write-Ok` / `Write-Warn` / `Write-Err`
   for output.
4. Accept `-h` / `--help` (or just always print the header at the top
   — the current scripts do).
5. For destructive actions, gate with `Confirm-Destructive` (which
   honours `-Force`).
6. If you add a new top-level command, update both the catalogue in
   `scripts/cardscape.ps1` and the menu in `run.ps1` so the dispatcher
   and the menu stay in sync.
