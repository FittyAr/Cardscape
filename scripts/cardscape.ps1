# =============================================================================
# cardscape.ps1 — Top-level dispatcher for the Cardscape scripts.
#
# Forwards `pwsh cardscape.ps1 <command> [args]` to scripts/<command>.ps1
# so contributors only need to remember one entry point. Each command
# accepts its own --help (or -h) for detailed usage.
#
# Quick reference:
#   pwsh cardscape.ps1 setup
#   pwsh cardscape.ps1 build
#   pwsh cardscape.ps1 test
#   pwsh cardscape.ps1 test -Coverage
#   pwsh cardscape.ps1 run api
#   pwsh cardscape.ps1 migrate list
#   pwsh cardscape.ps1 migrate add IssueFooBar
#   pwsh cardscape.ps1 db info
#   pwsh cardscape.ps1 db reset -Force
#   pwsh cardscape.ps1 docker up -Dev -Detached
#   pwsh cardscape.ps1 format -Verify
#   pwsh cardscape.ps1 clean
#
# Discovery rule:
#   - If the first argument matches a .ps1 file in scripts/, dispatch.
#   - Otherwise fall through to `scripts/setup.ps1` (with -h) so first-timers
#     see the catalogue.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command,

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
$ScriptsDir  = $PSScriptRoot
$RepoRootDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $RepoRootDir

function Show-Catalogue {
    $rows = @(
        @{ Cmd = 'setup';   What = 'First-time environment check' }
        @{ Cmd = 'build';   What = 'Restore + build the solution' }
        @{ Cmd = 'test';    What = 'Run the test matrix' }
        @{ Cmd = 'run';     What = 'Run api / web / mcp locally' }
        @{ Cmd = 'migrate'; What = 'EF Core migrations (list/apply/add/script/drop/bundle)' }
        @{ Cmd = 'db';      What = 'Database info, reset, open, tables' }
        @{ Cmd = 'docker';  What = 'docker-compose helper (up/down/logs/build/ps)' }
        @{ Cmd = 'format';  What = 'dotnet format (apply or verify)' }
        @{ Cmd = 'clean';   What = 'Remove build artifacts, caches, local db' }
    )

    Write-Host ''
    Write-Host 'Cardscape — developer command catalogue' -ForegroundColor Cyan
    Write-Host ('-' * 72)
    Write-Host ''
    Write-Host ('  {0,-10} {1}' -f 'command', 'what it does')
    Write-Host ('  {0,-10} {1}' -f '-------', '-------------')
    foreach ($r in $rows) {
        Write-Host ('  {0,-10} {1}' -f $r.Cmd, $r.What)
    }
    Write-Host ''
    Write-Host 'Usage:'
    Write-Host '  pwsh scripts/cardscape.ps1 <command> [options]'
    Write-Host ''
    Write-Host 'Examples:'
    Write-Host '  pwsh scripts/cardscape.ps1 setup'
    Write-Host '  pwsh scripts/cardscape.ps1 build -Release'
    Write-Host '  pwsh scripts/cardscape.ps1 test -Unit -Coverage'
    Write-Host '  pwsh scripts/cardscape.ps1 run api -ConnectionString "Data Source=Data/cardscape-local.db"'
    Write-Host '  pwsh scripts/cardscape.ps1 migrate add IssueFooBar'
    Write-Host '  pwsh scripts/cardscape.ps1 db reset -Force'
    Write-Host ''
    Write-Host 'Or, from the repo root, launch the interactive menu:'
    Write-Host '  pwsh run.ps1'
    Write-Host ''
    Write-Host 'Each command also accepts its own -h / --help for detail.'
}

if (-not $Command -or $Command -in @('-h', '--help', 'help', '?') ) {
    Show-Catalogue
    exit 0
}

$script = Join-Path $ScriptsDir "$Command.ps1"
if (-not (Test-Path $script)) {
    Write-Error "Unknown command '$Command'. Run 'pwsh scripts/cardscape.ps1' for the catalogue."
    Show-Catalogue
    exit 2
}

# Hand off. Forward $Rest as the argument array.
& pwsh $script @Rest
exit $LASTEXITCODE
