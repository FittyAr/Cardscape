# =============================================================================
# test.ps1 — Run the Cardscape test suite.
#
# Usage:
#   pwsh scripts/test.ps1                       # all tests
#   pwsh scripts/test.ps1 -Unit                 # unit only
#   pwsh scripts/test.ps1 -Integration          # integration only
#   pwsh scripts/test.ps1 -Architecture         # architecture only
#   pwsh scripts/test.ps1 -Functional           # functional only
#   pwsh scripts/test.ps1 -Filter "FullyQualifiedName~Workspaces"
#   pwsh scripts/test.ps1 -Coverage             # collect coverlet coverage
#   pwsh scripts/test.ps1 -NoBuild              # skip rebuild
#   pwsh scripts/test.ps1 -Watch                # live test run on file change
#
# Notes:
#   - The matrix is SQLite-only today (see docs/development/03-testing-strategy.md).
#     The convention is `--filter "Database=Sqlite|Database!=Sqlite"` (everything).
#   - Coverage uses coverlet.collector and writes Cobertura + JSON to TestResults/.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Unit,
    [switch]$Integration,
    [switch]$Functional,
    [switch]$Architecture,
    [string]$Filter,
    [switch]$Coverage,
    [switch]$NoBuild,
    [switch]$Watch,
    [string]$Configuration,
    [switch]$Detailed,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not $Configuration) { $Configuration = 'Debug' }
if (-not (Test-Dotnet)) { exit 1 }

# Build the project list to scope the test run.
$projects = @()
if ($Unit)         { $projects += Join-Path $TestsDir 'Cardscape.UnitTests/Cardscape.UnitTests.csproj' }
if ($Integration)  { $projects += Join-Path $TestsDir 'Cardscape.IntegrationTests/Cardscape.IntegrationTests.csproj' }
if ($Functional)   { $projects += Join-Path $TestsDir 'Cardscape.FunctionalTests/Cardscape.FunctionalTests.csproj' }
if ($Architecture) { $projects += Join-Path $TestsDir 'Cardscape.ArchitectureTests/Cardscape.ArchitectureTests.csproj' }

if ($projects.Count -eq 0) {
    # Default: everything.
    $projects = @(
        (Join-Path $TestsDir 'Cardscape.UnitTests/Cardscape.UnitTests.csproj'),
        (Join-Path $TestsDir 'Cardscape.IntegrationTests/Cardscape.IntegrationTests.csproj'),
        (Join-Path $TestsDir 'Cardscape.ArchitectureTests/Cardscape.ArchitectureTests.csproj')
    )
}

# Build the filter. Default = the matrix documented in the testing-strategy doc.
$filterArgs = @()
if ($Filter) {
    $filterArgs = @('--filter', $Filter)
} else {
    $filterArgs = @('--filter', 'Database=Sqlite|Database!=Sqlite')
}

$common = @(
    '--configuration', $Configuration,
    '--nologo'
)
if ($NoBuild) { $common += '--no-build' }
if ($Detailed) { $common += '--logger', 'console;verbosity=detailed' }
if ($Forward.Count -gt 0) { $common += $Forward }

if ($Watch) {
    foreach ($p in $projects) {
        if (-not (Test-Path $p)) { Write-Warn "Skipping missing project: $p"; continue }
        Invoke-Step -Message "Watch $($p | Split-Path -Leaf)" -Action {
            Run-Dotnet -Args (@('watch', 'test', '--project', $p) + $filterArgs + $common) | Out-Null
        }
    }
    return
}

$exit = 0
foreach ($p in $projects) {
    if (-not (Test-Path $p)) { Write-Warn "Skipping missing project: $p"; continue }
    Invoke-Step -Message "Testing $($p | Split-Path -Leaf)" -Action {
        $args = @('test', $p) + $filterArgs + $common
        if ($Coverage) {
            $args += @(
                '--collect', 'XPlat Code Coverage',
                '--results-directory', (Join-Path $RepoRoot 'TestResults'),
                '--', 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,lcov,json'
            )
        }
        Run-Dotnet -Args $args | Out-Null
        if ($LASTEXITCODE -ne 0) { $exit = $LASTEXITCODE }
    }
}

if ($Coverage) {
    Write-Step "Coverage report at TestResults/"
    Get-ChildItem (Join-Path $RepoRoot 'TestResults') -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty FullName
}

if ($exit -ne 0) {
    Write-Err "Tests failed (exit $exit)."
    exit $exit
}
Write-Ok "All tests passed."
