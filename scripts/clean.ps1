# =============================================================================
# clean.ps1 — Remove build artifacts, caches, and transient outputs.
#
# Usage:
#   pwsh scripts/clean.ps1                  # bin/obj + TestResults + caches
#   pwsh scripts/clean.ps1 -Database        # also wipe the local Sqlite file
#   pwsh scripts/clean.ps1 -Storage         # also wipe uploaded files
#   pwsh scripts/clean.ps1 -All             # everything (db + storage + artifacts)
#   pwsh scripts/clean.ps1 -DryRun          # report what would be removed
#
# Notes:
#   - This is purely a local hygiene script. It never touches git-tracked
#     files, the docker volumes, or anything outside the repo.
#   - Destructive (-Database, -Storage, -All) requires -Force in non-TTY shells.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$Database,
    [switch]$Storage,
    [switch]$All,
    [switch]$DryRun,
    [switch]$Force
)

. (Join-Path $PSScriptRoot '_common.ps1')

if ($All) { $Database = $true; $Storage = $true }

$removed = 0

function Remove-IfPresent {
    param(
        [Parameter(Mandatory)][string]$Path,
        [string]$Description
    )
    if (-not (Test-Path $Path)) { return }

    $kind = if ((Get-Item $Path) -is [System.IO.DirectoryInfo]) { 'dir' } else { 'file' }
    if ($DryRun) {
        Write-Info ("[dry-run] would remove {0}: {1} ({2})" -f $kind, $Path, $Description)
    } else {
        Write-Info ("removing {0}: {1}" -f $kind, $Path)
        if ($kind -eq 'dir') {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Remove-Item -Path $Path -Force -ErrorAction SilentlyContinue
        }
    }
    $Script:removed++
}

# 1. Build artifacts across the whole tree.
Write-Step "Build artifacts (bin/, obj/)"
$artifacts = Get-ChildItem -Path $RepoRoot -Recurse -Force -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj') -and $_.FullName -notmatch '[\\/]node_modules[\\/]' }
foreach ($d in $artifacts) {
    Remove-IfPresent -Path $d.FullName -Description 'build output'
}

# 2. Test results.
Remove-IfPresent -Path (Join-Path $RepoRoot 'TestResults') -Description 'test results'

# 3. dotnet temp caches.
$userProfile = $env:USERPROFILE
if ($userProfile) {
    Remove-IfPresent -Path (Join-Path $userProfile '.nuget/packages/.tools') -Description 'nuget tools cache'
    Remove-IfPresent -Path (Join-Path $userProfile '.dotnet/extensions') -Description 'dotnet extensions cache'
}
Remove-IfPresent -Path (Join-Path $RepoRoot '.vs') -Description 'visual studio cache'
Remove-IfPresent -Path (Join-Path $RepoRoot '.idea') -description 'rider cache'

# 4. Local log files declared in .gitignore.
foreach ($name in @('.apilog.txt', '.weblog.txt', '.buildlog.txt', '.fmtlog.txt', '.testlog.txt')) {
    Remove-IfPresent -Path (Join-Path $RepoRoot $name) -Description 'log file'
}

# 5. Generated SQL/scripts from migrate.ps1.
Remove-IfPresent -Path (Join-Path $RepoRoot 'migrations.sql') -Description 'generated SQL script'
Remove-IfPresent -Path (Join-Path $RepoRoot 'efbundle') -description 'ef bundle binary'

# 6. Optional: database file (Sqlite).
if ($Database) {
    Confirm-Destructive -What "delete the local Sqlite database at $Script:DataDir" -Force:$Force
    if (Test-Path $Script:DataDir) {
        Get-ChildItem -Path $Script:DataDir -Force -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-IfPresent -Path $_.FullName -Description 'sqlite db file'
        }
    }
}

# 7. Optional: uploaded storage.
if ($Storage) {
    Confirm-Destructive -What "delete the uploaded file storage at $Script:StorageDir" -Force:$Force
    if (Test-Path $Script:StorageDir) {
        Get-ChildItem -Path $Script:StorageDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-IfPresent -Path $_.FullName -Description 'uploaded file'
        }
    }
}

if ($DryRun) {
    Write-Ok "Dry run complete. $Script:removed item(s) would be removed."
} else {
    Write-Ok "Done. $Script:removed item(s) removed."
}
