# =============================================================================
# docker.ps1 — Manage the local docker-compose stack.
#
# Usage:
#   pwsh scripts/docker.ps1 up                    # full stack (Api + Postgres)
#   pwsh scripts/docker.ps1 up -Dev               # sqlite-only dev stack
#   pwsh scripts/docker.ps1 down                  # stop + remove containers
#   pwsh scripts/docker.ps1 down -V               # also drop named volumes
#   pwsh scripts/docker.ps1 logs                  # tail logs
#   pwsh scripts/docker.ps1 logs -Service cardscape.api --since 10m
#   pwsh scripts/docker.ps1 build                 # rebuild images
#   pwsh scripts/docker.ps1 ps                    # running services
#   pwsh scripts/docker.ps1 restart api           # restart a single service
#
# Notes:
#   - Default compose file is docker-compose.yml (production-ish, includes
#     Postgres). For local development without Postgres, use -Dev to pick
#     docker-compose.dev.yml.
#   - Reads CARDS_CAPE_JWT_KEY / CARDS_CAPE_DB_PASSWORD from .env if present.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('up', 'down', 'logs', 'build', 'ps', 'restart', 'pull')]
    [string]$Action,

    [string]$Service,
    [switch]$Dev,
    [switch]$Detached,
    [switch]$Build,
    [switch]$V,
    [int]$Tail = 100,
    [string]$Since,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Docker)) { exit 1 }

$composeFile = if ($Dev) { 'docker-compose.dev.yml' } else { 'docker-compose.yml' }
$composePath = Join-Path $RepoRoot $composeFile
if (-not (Test-Path $composePath)) {
    Write-Err "Compose file not found: $composePath"
    exit 1
}

# Detect docker compose v1 / v2.
$composeCmd = $null
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $ver = (& docker compose version 2>$null)
    if ($ver) {
        $composeCmd = @('docker', 'compose')
    } elseif (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        $composeCmd = @('docker-compose')
    }
}
if (-not $composeCmd) {
    Write-Err "Neither 'docker compose' (v2) nor 'docker-compose' (v1) is available."
    exit 1
}

$common = @('-f', $composePath)
if (Test-Path (Join-Path $RepoRoot '.env')) { $common += '--env-file', (Join-Path $RepoRoot '.env') }

switch ($Action) {
    'up' {
        $args = $common + @('up')
        if ($Detached) { $args += '-d' }
        if ($Build)    { $args += '--build' }
        if ($Service)  { $args += $Service }
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'down' {
        $args = $common + @('down')
        if ($V)         { $args += '-v' }
        if ($Service)   { $args += $Service }
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'logs' {
        $args = $common + @('logs', '--tail', "$Tail")
        if ($Since)    { $args += '--since', $Since }
        if ($Service)  { $args += $Service }
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'build' {
        $args = $common + @('build')
        if ($Service)  { $args += $Service }
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'ps' {
        $args = $common + @('ps')
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'restart' {
        if (-not $Service) { Write-Err "Pass -Service <name> to restart."; exit 2 }
        $args = $common + @('restart', $Service)
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
    'pull' {
        $args = $common + @('pull')
        if ($Service) { $args += $Service }
        if ($Forward.Count -gt 0) { $args += $Forward }
        & $composeCmd @args
    }
}
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Err "docker compose $Action exited with code $code."
    exit $code
}
