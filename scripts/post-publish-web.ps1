# =============================================================================
# post-publish-web.ps1 — Finalize the Blazor WASM publish output for
# the .NET 11 preview SDK.
#
# Background
# ----------
# With .NET 11.0.100-preview.6.26359.118, the BlazorWebAssembly SDK ships
# .wasm files with content fingerprints in the name
# (e.g. `Cardscape.Web.sm9dqa7yal.wasm`) but the token-replacement pass
# that rewrites the placeholder <link id="webassembly" /> and the
# <script src="_framework/blazor.webassembly#[.{fingerprint}].js"> tag in
# wwwroot/index.html does not run. The Blazor runtime needs the boot
# manifest to know which fingerprinted file maps to which assembly, and
# since the manifest is missing the app boots into an infinite "Loading"
# state.
#
# This script copies every fingerprinted _framework asset to its plain
# name (drop the .xxxxxxxxx infix) so the runtime can find them via the
# canonical paths. It also patches the .br / .gz / .map siblings.
#
# It is intentionally idempotent and only operates on the publish
# output directory. Re-run after every `dotnet publish` and the same
# fingerprinted files are simply re-copied.
#
# Usage
# -----
#   pwsh scripts/post-publish-web.ps1                       # default
#   pwsh scripts/post-publish-web.ps1 -PublishDir <path>     # override
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot '..\src\Cardscape.Web\bin\Release\net11.0\publish\wwwroot')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PublishDir)) {
    Write-Error "Publish dir not found: $PublishDir"
}

$framework = Join-Path $PublishDir '_framework'
if (-not (Test-Path $framework)) {
    Write-Error "_framework not found: $framework"
}

# A fingerprinted asset name looks like:
#   <base>.<10 lowercase base32 chars>.<ext>           e.g.  Cardscape.Web.sm9dqa7yal.wasm
#   <base>.<10 lowercase base32 chars>.<ext>.br
#   <base>.<10 lowercase base32 chars>.<ext>.gz
#   <base>.<10 lowercase base32 chars>.<ext>.map
# We rewrite to <base>.<ext>, dropping only the 10-char infix.
$fingerprint = '\.[a-z0-9]{10}\.'
$rewritten = 0

Get-ChildItem -Path $framework -File | ForEach-Object {
    $name = $_.Name
    if ($name -match "^(?<base>.+?)$fingerprint(?<ext>.+)$") {
        $plain = "$($Matches.base).$($Matches.ext)"
        $target = Join-Path $framework $plain
        if (-not (Test-Path $target) -or $_.LastWriteTimeUtc -gt (Get-Item $target).LastWriteTimeUtc) {
            Copy-Item -Path $_.FullName -Destination $target -Force
            $rewritten++
        }
    }
}

Write-Host "post-publish-web: copied $rewritten fingerprinted asset(s) to plain names in $framework" -ForegroundColor Green
