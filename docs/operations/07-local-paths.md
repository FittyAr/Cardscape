# Local paths and the FittyAr convention

Audit (October 2026): Cardscape installs nothing on the user's machine
and writes no user-level configuration outside the repository or Docker
volumes. The audited surface was `src/`, `sdk/` (a NuGet library),
`scripts/` (PowerShell helpers that write only inside the checkout),
`infra/` (a Dockerfile) and the compose files.

## Convention

| Purpose | Windows | Linux | macOS |
|---|---|---|---|
| Installed apps | `%LOCALAPPDATA%\Programs\FittyAr\Cardscape` | `~/.local/lib/FittyAr/Cardscape` | `~/Applications/FittyAr/Cardscape` |
| User config and data | `%APPDATA%\FittyAr\Cardscape` | `~/.config/FittyAr/Cardscape` | `~/Library/Application Support/FittyAr/Cardscape` |

There is no installer today, so the first row is reserved. The second row
applies when you run the API outside Docker.

## Docker

Volumes stay as defined in `docker-compose*.yml` and are not renamed:
`cardscape.data` (`/app/Data`), `cardscape.uploads` (`/app/Storage`),
`cardscape.keys` (`/app/DataProtectionKeys`) and `cardscape.postgres_data`.

## Running without Docker

Defaults are relative to the working directory (`Data/cardscape.db`,
`logs/`), which is fine for development. For a long-lived local run, set:

```powershell
$d = Join-Path $env:APPDATA 'FittyAr\Cardscape'
$env:ConnectionStrings__Default = "Data Source=$d\cardscape.db"
$env:Storage__LocalRoot = "$d\Storage"
$env:Cardscape__DataProtection__KeyDirectory = "$d\DataProtectionKeys"
```

```bash
d="${XDG_CONFIG_HOME:-$HOME/.config}/FittyAr/Cardscape"
export ConnectionStrings__Default="Data Source=$d/cardscape.db"
export Storage__LocalRoot="$d/Storage"
export Cardscape__DataProtection__KeyDirectory="$d/DataProtectionKeys"
```

Defaults in code were deliberately left unchanged (conservative): changing
them would move existing developers' databases.
