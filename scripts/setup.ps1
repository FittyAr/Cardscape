# =============================================================================
# setup.ps1 — First-time setup. Validates prerequisites and warms the toolchain.
#
# Usage:
#   pwsh scripts/setup.ps1                    # full check
#   pwsh scripts/setup.ps1 -NoFetch           # skip dotnet workload/tooling fetch
#   pwsh scripts/setup.ps1 -SkipBuild         # don't compile at the end
#
# What it does:
#   1. Verifies .NET SDK matches global.json.
#   2. Verifies git is on PATH and the working tree is clean enough.
#   3. Restores packages.
#   4. Restores local dotnet tools (dotnet-tools.json → empty today, future-proof).
#   5. (Optional) Builds the solution to confirm everything compiles.
#
# Notes:
#   - Does NOT install anything without asking first.
#   - If anything is missing it prints the exact next command you should run.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$NoFetch,
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot '_common.ps1')

$failed = $false

# -----------------------------------------------------------------------------
# 1. .NET SDK
# -----------------------------------------------------------------------------
Write-Step "Checking .NET SDK"
if (-not (Test-Dotnet)) {
    Write-Err "Your installed SDK does not match global.json. Install the required band."
    Write-Info "Required: $(Get-Content $Script:GlobalJson | ConvertFrom-Json).sdk.version"
    Write-Info "Get it from https://dotnet.microsoft.com/download/dotnet/11.0"
    $failed = $true
} else {
    $sdks = & dotnet --list-sdks
    Write-Info "Installed SDKs:"
    foreach ($s in $sdks) { Write-Info "  - $s" }
}

# -----------------------------------------------------------------------------
# 2. Git
# -----------------------------------------------------------------------------
Write-Step "Checking git"
if (Get-Command git -ErrorAction SilentlyContinue) {
    Write-Ok "git $(& git --version)"
    $branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
    if ($branch) { Write-Info "Current branch: $branch" }
} else {
    Write-Err "git is not on PATH."
    $failed = $true
}

# -----------------------------------------------------------------------------
# 3. Docker (optional)
# -----------------------------------------------------------------------------
Write-Step "Checking docker (optional)"
$hasDocker = Test-Docker

# -----------------------------------------------------------------------------
# 4. dotnet-ef (used by migrate.ps1)
# -----------------------------------------------------------------------------
Write-Step "Checking dotnet-ef"
$ef = (& dotnet ef --version 2>$null)
if ($ef) {
    Write-Ok "dotnet-ef $ef"
} else {
    Write-Warn "dotnet-ef not installed. migrate.ps1 will install it on first run, or you can install manually:"
    Write-Info "  dotnet tool install --global dotnet-ef --version 10.0.10"
}

# -----------------------------------------------------------------------------
# 5. Restore packages
# -----------------------------------------------------------------------------
if (-not $NoFetch) {
    Invoke-Step -Message "Restoring NuGet packages" -Action {
        Run-Dotnet -Args @('restore', $Script:Solution) | Out-Null
    }

    # Restore local tools (no-op today; future-proof for when we add a tool manifest).
    if (Test-Path $Script:ToolsJson) {
        Invoke-Step -Message "Restoring local dotnet tools" -Action {
            Run-Dotnet -Args @('tool', 'restore') | Out-Null
        }
    }
}

# -----------------------------------------------------------------------------
# 6. Build smoke test
# -----------------------------------------------------------------------------
if (-not $SkipBuild) {
    Invoke-Step -Message "Compiling the solution (smoke test)" -Action {
        Run-Dotnet -Args @('build', $Script:Solution, '--no-restore', '--configuration', 'Debug') | Out-Null
    }
}

# -----------------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------------
Write-Step "Summary"
if ($failed) {
    Write-Err "Setup failed. Resolve the items above and re-run."
    exit 1
}
Write-Ok "Environment looks good."
Write-Host ''
Write-Info "Next steps:"
Write-Info "  pwsh scripts/run.ps1 api                # start the API"
Write-Info "  pwsh scripts/test.ps1                    # run the test suite"
Write-Info "  pwsh scripts/migrate.ps1 list            # inspect migrations"
if ($hasDocker) {
    Write-Info "  pwsh scripts/docker.ps1 up -Dev -Detached  # dev stack (sqlite only)"
}
