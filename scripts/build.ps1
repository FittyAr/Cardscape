# =============================================================================
# build.ps1 — Restore + build the Cardscape solution.
#
# Usage:
#   pwsh scripts/build.ps1                    # restore + Debug build
#   pwsh scripts/build.ps1 -Release           # Release build
#   pwsh scripts/build.ps1 -NoRestore         # skip restore
#   pwsh scripts/build.ps1 -Project src/Cardscape.Api
#   pwsh scripts/build.ps1 --verbosity minimal
#
# Notes:
#   - Directory.Build.props pins TreatWarningsAsErrors=true. A green build
#     means 0 errors, 0 warnings.
#   - When -RunTests is passed, runs dotnet test after the build.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Release,
    [switch]$NoRestore,
    [switch]$RunTests,
    [string]$Project,
    [string]$Configuration,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if ($Release -and -not $Configuration) { $Configuration = 'Release' }
if (-not $Configuration) { $Configuration = 'Debug' }

if (-not (Test-Dotnet -RequirePreview)) { exit 1 }

$dotnetArgs = @('build')

if (-not $NoRestore) {
    $dotnetArgs = @('restore') + $dotnetArgs[1..0]  # keep 'build' as last, prepend restore
    # Simpler: split into two steps.
    Invoke-Step -Message "Restoring packages ($Configuration)" -Action {
        Run-Dotnet -Args @('restore', $Script:Solution) | Out-Null
    }
}

$targetArgs = @('build', $Script:Solution, '--configuration', $Configuration, '--no-incremental')

if ($Project) {
    $targetArgs = @('build', $Project, '--configuration', $Configuration, '--no-incremental')
}

if ($Forward.Count -gt 0) {
    $targetArgs += $Forward
}

Invoke-Step -Message "Building solution ($Configuration)" -Action {
    Run-Dotnet -Args $targetArgs | Out-Null
}

Write-Ok "Build succeeded ($Configuration)."

if ($RunTests) {
    Write-Step "Running test suite"
    & (Join-Path $PSScriptRoot 'test.ps1') @Forward
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Tests failed (exit $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}
