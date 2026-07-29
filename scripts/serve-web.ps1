# =============================================================================
# serve-web.ps1 — Run the Blazor WASM web client as a static site.
#
# Why this exists
# ---------------
# `dotnet run --project src/Cardscape.Web` uses the Blazor WASM dev server
# from the .NET 11 preview SDK (11.0.100-preview.6.26359.118). That dev
# server does not run the token-replacement pass over
# wwwroot/index.html, so the browser cannot resolve the fingerprinted
# script path and the app hangs on the initial loading spinner. The
# preview SDK also leaves the dev bin/ folder missing the per-assembly
# .wasm files, so even after patching index.html the runtime cannot
# download its dependencies.
#
# Workaround: publish the Web project, run a post-publish step that
# copies every fingerprinted asset to its plain name (so the runtime
# can find them without a boot manifest), then serve the publish
# output as static files. This is what `dotnet run` would do in a
# stable release.
#
# Usage
# -----
#   pwsh scripts/serve-web.ps1                          # default port 5206
#   pwsh scripts/serve-web.ps1 -Port 5206 -Configuration Release
#   pwsh scripts/serve-web.ps1 -NoPublish               # skip the rebuild
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [int]$Port = 5206,
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',
    [switch]$NoPublish,
    [switch]$NoApi,
    [string]$ApiUrl = 'http://localhost:5291'
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$WebProject = Join-Path $RepoRoot 'src/Cardscape.Web/Cardscape.Web.csproj'
$PublishDir = Join-Path $RepoRoot "src/Cardscape.Web/bin/$Configuration/net11.0/publish/wwwroot"
$PostPublish = Join-Path $PSScriptRoot 'post-publish-web.ps1'

# --- 1. Publish ---------------------------------------------------------------
if (-not $NoPublish) {
    Write-Host "serve-web: publishing $WebProject ($Configuration)..." -ForegroundColor Cyan
    & dotnet publish $WebProject -c $Configuration --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed (exit $LASTEXITCODE)"
    }
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish output not found: $PublishDir. Pass -NoPublish only after a successful publish."
}

# --- 2. Post-publish: drop the fingerprints ----------------------------------
Write-Host "serve-web: running post-publish step..." -ForegroundColor Cyan
& pwsh $PostPublish -PublishDir $PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "post-publish-web failed (exit $LASTEXITCODE)"
}

# --- 3. Make sure the API is up (the Blazor client will call it) -------------
if (-not $NoApi) {
    $apiUp = Test-NetConnection -ComputerName 'localhost' -Port 5291 -WarningAction SilentlyContinue
    if (-not $apiUp) {
        Write-Warning "API not reachable at $ApiUrl. Start the API in another terminal:"
        Write-Warning "  pwsh scripts/run.ps1 api"
    } else {
        Write-Host "serve-web: API reachable at $ApiUrl" -ForegroundColor Green
    }
}

# --- 4. Serve the publish output --------------------------------------------
Write-Host "serve-web: serving $PublishDir on http://localhost:$Port" -ForegroundColor Green
Write-Host "serve-web: open http://localhost:$Port in your browser. Ctrl+C to stop." -ForegroundColor Green
& python -m http.server $Port --bind 127.0.0.1 --directory $PublishDir
