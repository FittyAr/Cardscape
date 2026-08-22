# =============================================================================
# db.ps1 — Database utilities (inspect, reset, seed).
#
# Usage:
#   pwsh scripts/db.ps1 info                          # show provider + path/conn
#   pwsh scripts/db.ps1 reset -Force                  # drop + recreate + apply migrations (Sqlite)
#   pwsh scripts/db.ps1 reset -Database PostgreSQL -Force
#   pwsh scripts/db.ps1 reset -Database PostgreSQL -DropOnly
#   pwsh scripts/db.ps1 open                          # open the sqlite file with sqlite3 (best effort)
#   pwsh scripts/db.ps1 tables                        # list tables via dotnet ef
#
# Notes:
#   - Sqlite is the default. PostgreSQL / MySQL use the same connection string
#     layout that DesignTimeCardscapeDbContextFactory expects.
#   - `reset` runs migrate.ps1 drop + apply under the hood. For Postgres /
#     MySQL, the database itself must already exist (the role/user too).
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('info', 'reset', 'open', 'tables')]
    [string]$Action,

    [ValidateSet('Sqlite', 'PostgreSQL', 'MySql')]
    [string]$Database,
    [string]$ConnectionString,
    [switch]$DropOnly,
    [switch]$Force
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Dotnet)) { exit 1 }

if ($Database) { $env:Database__Provider = $Database }
if ($ConnectionString) { $env:ConnectionStrings__Default = $ConnectionString }
if (-not $env:Database__Provider) { $env:Database__Provider = 'Sqlite' }

$provider = $env:Database__Provider
$defaultCs = switch ($provider.ToLowerInvariant()) {
    'sqlite' { 'Data Source=Data/cardscape.db' }
    'postgresql' { 'Host=localhost;Port=5432;Database=cardscape;Username=cardscape;Password=cardscape' }
    'mysql'      { 'server=localhost;port=3306;database=cardscape;user=cardscape;password=cardscape' }
    default {
        if ($env:ConnectionStrings__Default) { $env:ConnectionStrings__Default }
        else { 'Data Source=Data/cardscape.db' }
    }
}
if (-not $env:ConnectionStrings__Default) {
    $env:ConnectionStrings__Default = $defaultCs
    Write-Info "ConnectionStrings__Default not set; defaulting to $defaultCs"
}

switch ($Action) {
    'info' {
        Write-Step "Database configuration"
        Write-Info "Provider   : $provider"
        Write-Info "Connection : $($env:ConnectionStrings__Default)"
        if ($provider -eq 'Sqlite') {
            $resolved = $env:ConnectionStrings__Default -replace '^Data Source=', ''
            $candidate = Join-Path $RepoRoot $resolved
            if (Test-Path $candidate) {
                $size = (Get-Item $candidate).Length
                Write-Info "File       : $candidate ($([math]::Round($size/1KB, 1)) KB)"
            } else {
                Write-Info "File       : (does not exist yet) $candidate"
            }
        }
    }
    'reset' {
        Confirm-Destructive -What "drop and re-create the database ($provider)" -Force:$Force
        Write-Step "Dropping database"
        & (Join-Path $PSScriptRoot 'migrate.ps1') drop -Database $provider -Force:$Force
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (-not $DropOnly) {
            Write-Step "Re-applying migrations"
            & (Join-Path $PSScriptRoot 'migrate.ps1') apply -Database $provider
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
        Write-Ok "Reset complete."
    }
    'open' {
        if ($provider -ne 'Sqlite') {
            Write-Err "open is only meaningful for Sqlite. Use your DB client for $provider."
            exit 2
        }
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
        & (Join-Path $PSScriptRoot 'migrate.ps1') list -Database $provider
    }
}
