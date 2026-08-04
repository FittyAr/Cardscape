# =============================================================================
# ⚠️  DEPRECATED — do not use.
# post-publish-web.ps1 — historical workaround for the .NET 11
# preview SDK (11.0.100-preview.6.26359.118). The BlazorWebAssembly
# SDK shipped .wasm files with content fingerprints in the name
# (e.g. `Cardscape.Web.sm9dqa7yal.wasm`) but the token-replacement
# pass that rewrites the boot manifest did not run, so the app
# booted into an infinite "Loading" state.
#
# The project now targets net10.0 (LTS) where the SDK runs the
# token-replacement pass correctly, so this script is no longer
# required. It is kept for historical reference only; running it
# against current build output is a no-op because the publish
# already strips the fingerprints. Safe to delete.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [string]$PublishDir = (Join-Path $PSScriptRoot '..\src\Cardscape.Web\bin\Release\net10.0\publish\wwwroot')
)

$ErrorActionPreference = 'Stop'

Write-Warning "post-publish-web.ps1 is deprecated. The .NET 10 SDK already runs the token-replacement pass; nothing to do."
return

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
