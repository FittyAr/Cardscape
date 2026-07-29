# =============================================================================
# format.ps1 — Run `dotnet format` against the solution.
#
# Usage:
#   pwsh scripts/format.ps1                       # format with default style (whitespace + style)
#   pwsh scripts/format.ps1 -Verify               # CI mode: exit non-zero if reformatting is needed
#   pwsh scripts/format.ps1 -Severity warn        # only fix warnings
#   pwsh scripts/format.ps1 -FixAnalyzers         # also fix analyzer suggestions
#   pwsh scripts/format.ps1 -Include src/Cardscape.Api
#
# Notes:
#   - The repo's .editorconfig is the source of truth for whitespace/indentation.
#   - -Verify is the typical CI gate.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Verify,
    [ValidateSet('info', 'warn', 'error')]
    [string]$Severity,
    [switch]$FixAnalyzers,
    [string]$Include,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Dotnet)) { exit 1 }

$args = @('format', $Script:Solution, '--no-restore', '--verbosity', 'minimal')
if ($Verify)        { $args += '--verify-no-changes' }
if ($Severity)      { $args += '--severity', $Severity }
if ($FixAnalyzers)  { $args += '--include-analyzers' }
if ($Include)       { $args += '--include', $Include }
if ($Forward.Count -gt 0) { $args += $Forward }

$mode = if ($Verify) { 'verify' } else { 'apply' }
Invoke-Step -Message "dotnet format ($mode)" -Action {
    Run-Dotnet -Args $args | Out-Null
}

if ($Verify -and $LASTEXITCODE -ne 0) {
    Write-Err "Format check failed. Run 'pwsh scripts/format.ps1' to fix."
    exit $LASTEXITCODE
}

Write-Ok "Format clean."
