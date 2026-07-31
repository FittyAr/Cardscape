#Requires -Version 7.0
<#
.SYNOPSIS
    Publishes the Cardscape.Web Blazor WASM client and copies its
    wwwroot verbatim into the API's output wwwroot.

.DESCRIPTION
    Historical workaround for the .NET 11 preview SDK
    (11.0.100-preview.6.26359.118): it did not run the
    static-web-assets merge for a server (API) project that
    references a Blazor WASM project, so UseBlazorFrameworkFiles() +
    UseStaticFiles() + MapFallbackToFile() could not auto-discover
    the client. As a stopgap, this script:

      1. Publishes the Web project to a known temp folder.
      2. Copies the entire published wwwroot on top of the API's
         output wwwroot.

    The published output must NOT be modified after `dotnet publish`.
    The framework .wasm / .js files keep their static-web-asset
    fingerprints (e.g. Microsoft.Extensions.Hosting.Abstractions
    .o74pq60xl6.wasm) because the boot manifest is embedded inline
    in _framework/dotnet.js, not in a separate blazor.boot.json file.
    The browser fetches the fingerprinted URLs and checks SRI against
    the hashes baked into the same embedded manifest, so renaming the
    files or rewriting the JSON only makes things worse.

    Now that the project targets net10.0 (LTS) the static-web-assets
    merge is expected to run correctly, so this script is no longer
    required. It is kept for safety and can be removed along with the
    CopyBlazorClientWwwroot target in Cardscape.Api.csproj once a
    `dotnet build` confirms the API hosts the Blazor client without
    help.

    Invoked from src/Cardscape.Api/Cardscape.Api.csproj as the
    AfterTargets="Build" target `CopyBlazorClientWwwroot`.

.PARAMETER ApiOutDir
    The API's output directory (i.e. the value of $(OutDir) when the
    API project builds). The script writes the client under
    "$ApiOutDir\wwwroot".

.PARAMETER WebProject
    Full path to the Cardscape.Web csproj.

.PARAMETER Configuration
    Debug | Release. Matches the API's $(Configuration).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ApiOutDir,
    [Parameter(Mandatory)][string]$WebProject,
    [Parameter(Mandatory)][string]$Configuration
)

$ErrorActionPreference = 'Stop'

# Standalone log helpers (don't import _common.ps1 — this script runs
# from MSBuild and must be self-contained).
function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor DarkGray }
function Write-Warn { param([string]$Message) Write-Host " [WARN] $Message" -ForegroundColor Yellow }
function Write-Ok   { param([string]$Message) Write-Host " [OK] $Message" -ForegroundColor Green }

# Publish the Web project to a per-build temp folder so we don't fight
# the build's own bin\...\wwwroot.
$stamp  = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff')
$pubDir = Join-Path $env:TEMP "cardscape-web-publish-$stamp"
$pubWww = Join-Path $pubDir 'wwwroot'
$apiWww = Join-Path $ApiOutDir 'wwwroot'

Write-Step "Publishing Cardscape.Web to $pubDir"
& dotnet publish $WebProject --configuration $Configuration --output $pubDir --no-build *>&1 |
    ForEach-Object { Write-Info "  $_" }

if ($LASTEXITCODE -ne 0) {
    Write-Warn "Publish --no-build failed; retrying with a full publish (build + publish)."
    & dotnet publish $WebProject --configuration $Configuration --output $pubDir *>&1 |
        ForEach-Object { Write-Info "  $_" }
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit $LASTEXITCODE"
    }
}

if (-not (Test-Path $pubWww)) {
    throw "Publish output is missing wwwroot at $pubWww"
}

# Sanity check: dotnet.js (which carries the embedded boot manifest in
# .NET 11) must be in the publish output, otherwise the browser will
# not be able to discover the fingerprinted runtime assets.
$dotnetJs = Join-Path $pubWww '_framework/dotnet.js'
if (-not (Test-Path $dotnetJs)) {
    throw "Expected $dotnetJs to exist after publish; the .NET 11 preview SDK build is missing the boot manifest carrier."
}

# Sanity check: blazor.boot.json must NOT be present. Earlier versions
# of this script tried to strip fingerprints and rewrite that file,
# which silently broke the runtime in .NET 11 because the manifest is
# no longer served as a separate JSON file (it's embedded in
# dotnet.js). If we ever see blazor.boot.json resurface, we need to
# revisit this script — the .NET SDK contract has changed.
$bootJson = Join-Path $pubWww '_framework/blazor.boot.json'
if (Test-Path $bootJson) {
    Write-Warn "blazor.boot.json present in publish output. The .NET 11 preview does not generate this file; if you are seeing this on a stable .NET 11 SDK, please review whether this script still needs to copy the file as-is or should re-introduce the JSON patch step."
}

Write-Step "Copying $pubWww -> $apiWww"
New-Item -ItemType Directory -Path $apiWww -Force | Out-Null
Copy-Item -Path (Join-Path $pubWww '*') -Destination $apiWww -Recurse -Force

# Clean up the temp publish folder.
Remove-Item -LiteralPath $pubDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Ok "Blazor client copied to $apiWww (fingerprints preserved; .NET 11 boot manifest lives inline in _framework/dotnet.js)"
