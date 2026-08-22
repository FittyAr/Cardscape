# =============================================================================
# db.ps1 — Database utilities (inspect, reset, seed).
#
# Usage:
#   pwsh scripts/db.ps1 info                          # show SQLite path/connection
#   pwsh scripts/db.ps1 reset -Force                  # drop + recreate + apply migrations (Sqlite)
#   pwsh scripts/db.ps1 open                          # open the sqlite file with sqlite3 (best effort)
#   pwsh scripts/db.ps1 tables                        # list tables via dotnet ef
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('info', 'reset', 'open', 'tables')]
    [string]$Action,

    [string]$ConnectionString,
    [switch]$DropOnly,
    [switch]$Force
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Dotnet)) { exit 1 }

if ($ConnectionString) { $env:ConnectionStrings__Default = $ConnectionString }
if (-not $env:ConnectionStrings__Default) {
    $env:ConnectionStrings__Default = 'Data Source=Data/cardscape.db'
    Write-Info "ConnectionStrings__Default not set; defaulting to Data Source=Data/cardscape.db"
}

switch ($Action) {
    'info' {
        Write-Step "Database configuration"
        Write-Info "Provider   : SQLite"
        Write-Info "Connection : $($env:ConnectionStrings__Default)"
        $resolved = $env:ConnectionStrings__Default -replace '^Data Source=', ''
        $candidate = Join-Path $RepoRoot $resolved
        if (Test-Path $candidate) {
            $size = (Get-Item $candidate).Length
            Write-Info "File       : $candidate ($([math]::Round($size/1KB, 1)) KB)"
        } else {
            Write-Info "File       : (does not exist yet) $candidate"
        }
    }
    'reset' {
        Confirm-Destructive -What "drop and re-create the SQLite database" -Force:$Force
        Write-Step "Dropping database"
        & (Join-Path $PSScriptRoot 'migrate.ps1') drop -Force:$Force
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (-not $DropOnly) {
            Write-Step "Re-applying migrations"
            & (Join-Path $PSScriptRoot 'migrate.ps1') apply
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
        Write-Ok "Reset complete."
    }
    'open' {
        $resolved = $env:ConnectionStrings__Default -replace '^Data Source=', ''
        $candidate = Join-Path $RepoRoot $resolved
        if (-not (Test-Path $candidate)) {
            Write-Err "Sqlite file not found at $candidate. Run 'pwsh scripts/run.ps1 api' once to create it, or 'pwsh scripts/migrate.ps1 apply'."
            exit 1
        }
        $cli = Get-Command sqlite3 -ErrorAction SilentlyContinue
        if ($cli) {
            & sqlite3 $candidate
        } else {
            Write-Info "sqlite3 CLI not on PATH. Opening with the default OS handler."
            Start-Process $candidate
        }
    }
    'tables' {
        & (Join-Path $PSScriptRoot 'migrate.ps1') list
    }
}
