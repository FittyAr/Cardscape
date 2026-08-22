# =============================================================================
# migrate.ps1 — Manage EF Core migrations for Cardscape.
#
# Usage:
#   pwsh scripts/migrate.ps1 list                       # list applied + pending
#   pwsh scripts/migrate.ps1 apply                      # apply pending SQLite migrations
#   pwsh scripts/migrate.ps1 add IssueFooBar            # create a new migration
#   pwsh scripts/migrate.ps1 script                     # generate SQL script (idempotent)
#   pwsh scripts/migrate.ps1 script -From 0 -To Latest  # explicit range
#   pwsh scripts/migrate.ps1 drop                       # drop the database
#   pwsh scripts/migrate.ps1 bundle                     # build a self-contained ef bundle
#
# Notes:
#   - SQLite is the only supported provider.
#   - The Infrastructure project owns the EF Core migration history.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('list', 'apply', 'add', 'script', 'drop', 'remove', 'bundle')]
    [string]$Action,

    [string]$Name,
    [string]$From,
    [string]$To,
    [string]$Output,
    [switch]$Force,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Dotnet -RequirePreview)) { exit 1 }

# Ensure dotnet-ef is installed. The repo uses Directory.Packages.props
# (CPM), so the tool manifest in dotnet-tools.json is the source of truth.
$efVer = '10.0.10'
$efInstalled = (& dotnet ef --version 2>$null)
if (-not $efInstalled) {
    Write-Warn "dotnet-ef not found. Installing version $efVer as a global tool (you may be prompted for elevation)..."
    Run-Dotnet -Args @('tool', 'install', '--global', 'dotnet-ef', '--version', $efVer) | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Info "Effective provider: SQLite"

$project = $Script:InfraProject
if (-not (Test-Path $project)) {
    Write-Err "Infra project not found: $project"
    exit 1
}

switch ($Action) {
    'list' {
        $args = @('ef', 'migrations', 'list', '--project', $project, '--no-build')
        if ($Forward) { $args += $Forward }
        Run-Dotnet -Args $args | Out-Null
    }
    'apply' {
        $args = @('ef', 'database', 'update', '--project', $project, '--no-build')
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Applying migrations" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
        Write-Ok "Database is up to date."
    }
    'add' {
        if (-not $Name) { Write-Err "Pass -Name <MigrationName>."; exit 2 }
        $args = @('ef', 'migrations', 'add', $Name, '--project', $project, '--no-build', '--output-dir', 'Persistence/Migrations')
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Creating migration $Name" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
        Write-Ok "Migration $Name created under src/Cardscape.Infrastructure/Persistence/Migrations/."
    }
    'script' {
        $args = @('ef', 'migrations', 'script', '--project', $project, '--no-build', '--idempotent')
        if ($From) { $args += @('--from', $From) }
        if ($To)   { $args += @('--to',   $To) }
        if ($Output) {
            $args += @('--output', $Output)
        } else {
            $args += @('--output', (Join-Path $RepoRoot 'migrations.sql'))
            Write-Info "No -Output given; writing to migrations.sql in the repo root."
        }
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Generating SQL script" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
    }
    'drop' {
        Confirm-Destructive -What "drop the SQLite database" -Force:$Force
        $args = @('ef', 'database', 'drop', '--project', $project, '--no-build', '--force')
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Dropping database" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
    }
    'remove' {
        Confirm-Destructive -What "remove the most recent migration file" -Force:$Force
        $args = @('ef', 'migrations', 'remove', '--project', $project, '--no-build', '--force')
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Removing last migration" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
    }
    'bundle' {
        $args = @('ef', 'migrations', 'bundle', '--project', $project, '--no-build', '--self-contained', '-o', (Join-Path $RepoRoot 'efbundle'))
        if ($Forward) { $args += $Forward }
        Invoke-Step -Message "Building efbundle" -Action {
            Run-Dotnet -Args $args | Out-Null
        }
        Write-Ok "Bundle written to ./efbundle. Use './efbundle --connection ...' to apply."
    }
}
