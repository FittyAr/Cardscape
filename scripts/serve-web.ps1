# =============================================================================
# ⚠️  DEPRECATED — do not use.
# serve-web.ps1 — historical workaround for the .NET 11 preview SDK
# (11.0.100-preview.6.26359.118) where `dotnet run --project
# src/Cardscape.Web` did not run the token-replacement pass on
# wwwroot/index.html and the dev bin/ folder was missing per-assembly
# .wasm files.
#
# The project now targets net10.0 (LTS) where the SDK is stable and
# `dotnet run` works as expected. Use the dev menu (run.ps1 → "Run a
# service" → "Web") or run directly:
#
#   dotnet run --project src/Cardscape.Web
#
# This file is kept only for historical reference; running it now
# will error out because the downstream post-publish-web.ps1 is also
# deprecated. Safe to delete.
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

Write-Warning "serve-web.ps1 is deprecated. Use 'dotnet run --project src/Cardscape.Web' or the dev menu (run.ps1)."
return

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
