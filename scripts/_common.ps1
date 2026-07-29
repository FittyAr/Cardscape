# =============================================================================
# _common.ps1 — Shared helpers for the Cardscape scripts.
#
# Loaded by every script in scripts/ and by the cardscape.ps1 dispatcher.
# Exposes:
#   - Paths           (RepoRoot, ScriptsDir, SrcDir, TestsDir, etc.)
#   - Logging         (Write-Step, Write-Info, Write-Ok, Write-Warn, Write-Err)
#   - Prereqs         (Test-Dotnet, Test-Docker, Require-Cmd)
#   - Confirmation    (Confirm-Destructive)
#   - Network         (Get-LocalIPs)
#   - Run helpers     (Invoke-Step, Run, Run-With)
#
# Conventions:
#   - All scripts are PowerShell 7+ (pwsh). Compatible with Windows / WSL2.
#   - LF line endings, UTF-8 (no BOM).
#   - No external dependencies besides the .NET SDK and (optionally) Docker.
# =============================================================================

#Requires -Version 7.0

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

# -----------------------------------------------------------------------------
# Paths
# -----------------------------------------------------------------------------
# _common.ps1 lives in <repo>/scripts/. Resolve the repo root from here.
$Script:ScriptsDir = $PSScriptRoot
$Script:RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Script:SrcDir     = Join-Path $RepoRoot 'src'
$Script:TestsDir   = Join-Path $RepoRoot 'tests'
$Script:DocsDir    = Join-Path $RepoRoot 'docs'
$Script:DataDir    = Join-Path $RepoRoot 'src/Cardscape.Api/Data'
$Script:StorageDir = Join-Path $RepoRoot 'src/Cardscape.Api/Storage'
$Script:Solution   = Join-Path $RepoRoot 'Cardscape.slnx'
$Script:ApiProject = Join-Path $RepoRoot 'src/Cardscape.Api/Cardscape.Api.csproj'
$Script:WebProject = Join-Path $RepoRoot 'src/Cardscape.Web/Cardscape.Web.csproj'
$Script:McpProject = Join-Path $RepoRoot 'src/Cardscape.Mcp/Cardscape.Mcp.csproj'
$Script:InfraProject = Join-Path $RepoRoot 'src/Cardscape.Infrastructure/Cardscape.Infrastructure.csproj'
$Script:GlobalJson   = Join-Path $RepoRoot 'global.json'
$Script:ToolsJson    = Join-Path $RepoRoot 'dotnet-tools.json'

# -----------------------------------------------------------------------------
# Logging (TTY-aware; falls back to plain text when not interactive)
# -----------------------------------------------------------------------------
$Script:IsTty = [bool] ([Environment]::IsInteractive) -and $Host.Name -eq 'ConsoleHost'

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    if ($Script:IsTty) { Write-Host "==> $Message" -ForegroundColor Cyan }
    else { Write-Output "==> $Message" }
}

function Write-Info {
    param([Parameter(Mandatory)][string]$Message)
    if ($Script:IsTty) { Write-Host "    $Message" -ForegroundColor DarkGray }
    else { Write-Output "    $Message" }
}

function Write-Ok {
    param([Parameter(Mandatory)][string]$Message)
    if ($Script:IsTty) { Write-Host " [OK] $Message" -ForegroundColor Green }
    else { Write-Output " [OK] $Message" }
}

function Write-Warn {
    param([Parameter(Mandatory)][string]$Message)
    if ($Script:IsTty) { Write-Host " [WARN] $Message" -ForegroundColor Yellow }
    else { Write-Warning $Message }
}

function Write-Err {
    param([Parameter(Mandatory)][string]$Message)
    if ($Script:IsTty) { Write-Host " [ERR] $Message" -ForegroundColor Red }
    else { Write-Error $Message }
}

# -----------------------------------------------------------------------------
# Prerequisite checks
# -----------------------------------------------------------------------------

function Test-Dotnet {
    [CmdletBinding()]
    param(
        [switch]$RequirePreview
    )

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Err ".NET SDK not found on PATH. Install the SDK pinned in global.json and retry."
        return $false
    }

    $installed = & dotnet --version 2>$null
    if (-not $installed) {
        Write-Err "Failed to read dotnet --version."
        return $false
    }

    if ($RequirePreview) {
        if ($installed -notmatch '-' -and $installed -notmatch 'preview') {
            Write-Warn "Installed SDK is $installed (stable). global.json requires a preview band."
            return $false
        }
    }

    Write-Ok "dotnet $installed"
    return $true
}

function Test-Docker {
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        $ver = (& docker --version 2>$null) -replace '\s+', ' '
        Write-Ok "$ver"
        return $true
    }
    Write-Warn "Docker not found on PATH. Docker-related commands will fail."
    return $false
}

function Require-Cmd {
    param([Parameter(Mandatory)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Err "Required command '$Name' is not on PATH."
        exit 127
    }
}

# -----------------------------------------------------------------------------
# Network helpers
# -----------------------------------------------------------------------------

# Returns the machine's non-loopback, non-link-local IPv4 addresses as an
# array of strings. Cross-platform: works on Windows, macOS, and Linux.
# Falls back to "(no LAN IP detected)" when nothing is found so the caller
# can always print a line.
function Get-LocalIPs {
    $ips = @()

    # Preferred path: System.Net.Dns — same on every platform, no modules needed.
    try {
        $hostName = [System.Net.Dns]::GetHostName()
        $ips = @([System.Net.Dns]::GetHostAddresses($hostName)) |
            Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
            Where-Object { $_.ToString() -notmatch '^127\.' -and $_.ToString() -notmatch '^169\.254\.' } |
            ForEach-Object { $_.ToString() } |
            Sort-Object -Unique
    } catch {
        # Ignore — fall through to the platform CLI fallback.
    }

    # Fallback: parse `hostname -I` (Linux/macOS) when Dns didn't yield anything.
    if (-not $ips -or $ips.Count -eq 0) {
        $hostnameI = (& hostname -I 2>$null)
        if ($hostnameI) {
            $ips = $hostnameI -split '\s+' | Where-Object { $_ -match '^\d+\.\d+\.\d+\.\d+$' }
        }
    }

    if (-not $ips -or $ips.Count -eq 0) {
        $ips = @('(no LAN IP detected)')
    }
    return $ips
}

# -----------------------------------------------------------------------------
# Confirmation gate (destructive actions only)
# -----------------------------------------------------------------------------
function Confirm-Destructive {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$What,
        [switch]$Force
    )

    if ($Force) { return }
    if (-not $Script:IsTty) {
        Write-Warn "Non-interactive shell; pass -Force to confirm: $What"
        exit 2
    }

    $answer = Read-Host "About to $What. Type 'yes' to continue"
    if ($answer -ne 'yes') {
        Write-Info "Cancelled."
        exit 0
    }
}

# -----------------------------------------------------------------------------
# Run helpers
# -----------------------------------------------------------------------------

# Run a step and fail loudly with a clean prefix.
function Invoke-Step {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Message,
        [Parameter(Mandatory)][scriptblock]$Action
    )
    Write-Step $Message
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Step failed: $Message (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

# Run a .NET command with consistent logging. Returns $LASTEXITCODE.
function Run-Dotnet {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$Args)
    Write-Info "dotnet $($Args -join ' ')"
    & dotnet @Args
    return $LASTEXITCODE
}

# -----------------------------------------------------------------------------
# Argument / option helpers
# -----------------------------------------------------------------------------
function Test-Flag {
    param(
        [Parameter(Mandatory)][string[]]$Args,
        [Parameter(Mandatory)][string[]]$Names
    )
    foreach ($n in $Names) {
        if ($Args -contains $n) { return $true }
    }
    return $false
}

function Get-OptionValue {
    param(
        [Parameter(Mandatory)][string[]]$Args,
        [Parameter(Mandatory)][string[]]$Names
    )
    foreach ($n in $Names) {
        for ($i = 0; $i -lt $Args.Count; $i++) {
            if ($Args[$i] -eq $n -and ($i + 1) -lt $Args.Count) {
                return $Args[$i + 1]
            }
        }
    }
    return $null
}

# Print a uniform help preamble for every script.
function Write-ScriptHeader {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Synopsis,
        [Parameter(Mandatory)][string[]]$Examples
    )
    Write-Host ''
    Write-Host ("$Name - $Synopsis") -ForegroundColor Cyan
    Write-Host ('-' * 72)
    Write-Host ''
    Write-Host 'Usage:  pwsh scripts/<file>.ps1 [options] [-- extra args forwarded]'
    Write-Host ''
    Write-Host 'Examples:'
    foreach ($e in $Examples) {
        Write-Host ("  $e")
    }
    Write-Host ''
}
