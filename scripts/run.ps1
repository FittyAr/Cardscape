# =============================================================================
# run.ps1 — Run Cardscape services locally.
#
# Usage:
#   pwsh scripts/run.ps1 api                   # run REST API + Blazor host
#   pwsh scripts/run.ps1 web                   # run Blazor WASM client only
#   pwsh scripts/run.ps1 mcp                   # run MCP server
#   pwsh scripts/run.ps1 api -Database PostgreSQL
#   pwsh scripts/run.ps1 api -Port 5291 -NoHttps
#   pwsh scripts/run.ps1 api -- --urls=http://localhost:5291
#
# Notes:
#   - Default provider is Sqlite (Data Source=Data/cardscape.db).
#   - For Postgres / MariaDB you'll need a running instance — see
#     scripts/db.ps1 helpers or docker-compose.dev.yml + the postgres compose.
#   - The API also hosts the Blazor WASM client (see src/Cardscape.Api),
#     so `api` is the typical one-shot run.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('api', 'web', 'mcp')]
    [string]$Service,

    [ValidateSet('Sqlite', 'PostgreSQL', 'MariaDB')]
    [string]$Database,

    [string]$ConnectionString,
    [string]$Port,
    [switch]$NoHttps,
    [switch]$NoBuild,
    [switch]$NoLaunchProfile,
    [string[]]$Forward
)

. (Join-Path $PSScriptRoot '_common.ps1')

if (-not (Test-Dotnet)) { exit 1 }

$project = switch ($Service) {
    'api' { $Script:ApiProject }
    'web' { $Script:WebProject }
    'mcp' { $Script:McpProject }
}

if (-not (Test-Path $project)) {
    Write-Err "Project not found: $project"
    exit 1
}

# Environment overrides for the child dotnet process.
$envOverrides = @{}
if ($Database)        { $envOverrides['Database__Provider']        = $Database }
if ($ConnectionString){ $envOverrides['ConnectionStrings__Default'] = $ConnectionString }
if ($Port) {
    $url = if ($NoHttps) { "http://localhost:$Port" } else { "https://localhost:$Port" }
    $envOverrides['ASPNETCORE_URLS'] = $url
}

# Default per-service expected URLs (read from launchSettings.json if present).
$expectedUrls = switch ($Service) {
    'api' { @('http://localhost:5291', 'https://localhost:7259') }
    'web' { @('http://localhost:5292', 'https://localhost:7253') }
    'mcp' { @('http://localhost:5100') }
    default { @() }
}

$args = @('run', '--project', $project)
if ($NoBuild) { $args += '--no-build' }
if ($NoLaunchProfile) { $args += '--no-launch-profile' }
if ($Forward.Count -gt 0) { $args += '--'; $args += $Forward }

Write-Step "Starting $Service from $project"
Write-Info "dotnet $($args -join ' ')"
foreach ($k in $envOverrides.Keys) {
    Write-Info "$k = $($envOverrides[$k])"
    Set-Item -Path "Env:$k" -Value $envOverrides[$k]
}

# Show the expected URLs the user can open. These are the default ports
# from launchSettings.json; the actual ports the service prints will also
# appear in the streaming output below.
if ($expectedUrls.Count -gt 0) {
    Write-Info 'expected URLs (from launchSettings.json):'
    foreach ($u in $expectedUrls) {
        Write-Info "  - $u"
    }
}

$ips = Get-LocalIPs
Write-Info 'reachable on the LAN as:'
foreach ($ip in $ips) {
    foreach ($u in $expectedUrls) {
        # NOTE: do NOT use $host here — $Host is the read-only console-host
        # automatic variable in PowerShell and assigning to it throws.
        $proto = ($u -split '://')[0]
        $port  = if ($u -match ':\d+') { ($u -split ':')[-1] } else { '' }
        if ($port) {
            Write-Info ("  - {0}://{1}:{2}" -f $proto, $ip, $port)
        }
    }
}

Write-Info '(streaming dotnet output below — Ctrl+C to stop the service)'
Write-Host ''

& dotnet @args
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Err "Service exited with code $code."
    exit $code
}
