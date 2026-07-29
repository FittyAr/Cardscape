# =============================================================================
# run.ps1 — Interactive dev menu for Cardscape.
#
# A no-memory launcher: every task the project supports is one menu pick
# away. Each pick dispatches to scripts/cardscape.ps1, which in turn runs
# the matching scripts/<command>.ps1 with the right flags.
#
# Usage:
#   pwsh run.ps1                  # interactive menu (this is the default)
#   pwsh run.ps1 -NonInteractive  # print the menu once and exit (CI / docs)
#   pwsh run.ps1 -SkipWelcome     # skip the intro screen on first paint
#
# Conventions:
#   - Returns to the top menu after an action (except 0 = Exit, h = Help).
#   - Sub-menus accept single-key choices and Enter alone to go back.
#   - Destructive actions are NOT auto-confirmed here. They delegate to
#     scripts/<x>.ps1 which already uses Confirm-Destructive.
# =============================================================================

#Requires -Version 7.0

[CmdletBinding()]
param(
    [switch]$NonInteractive,
    [switch]$SkipWelcome
)

$ErrorActionPreference = 'Stop'

# -----------------------------------------------------------------------------
# Paths and dispatcher resolution.
# -----------------------------------------------------------------------------
$RepoRoot   = (Resolve-Path $PSScriptRoot).Path
$Dispatcher = Join-Path $RepoRoot 'scripts/cardscape.ps1'
if (-not (Test-Path $Dispatcher)) {
    Write-Host "Could not find scripts/cardscape.ps1 at $Dispatcher" -ForegroundColor Red
    exit 1
}
Set-Location $RepoRoot

# -----------------------------------------------------------------------------
# Output helpers (local, self-contained — keeps the menu snappy).
#
# Names are intentionally non-conflicting with PowerShell built-in aliases.
# In particular, `H` is the standard alias for `Get-History`, which has a
# mandatory -Id <Int64> parameter — calling `Section 'Build & restore'` would be
# routed to Get-History and explode with a binding error.
# -----------------------------------------------------------------------------
function Title {
    Write-Host ''
    Write-Host '  CARDS' -ForegroundColor Magenta -NoNewline
    Write-Host 'cape' -ForegroundColor White -NoNewline
    Write-Host '  dev menu' -ForegroundColor DarkGray
    Write-Host ('  ' + ('-' * 56)) -ForegroundColor DarkGray
    Write-Host ("  repo : $RepoRoot") -ForegroundColor DarkGray
}
function Section { param([string]$Text) Write-Host ''; Write-Host $Text -ForegroundColor Cyan }
function Muted   { param([string]$Text) Write-Host $Text -ForegroundColor DarkGray }
function Alert   { param([string]$Text) Write-Host $Text -ForegroundColor Yellow }
function Good    { param([string]$Text) Write-Host $Text -ForegroundColor Green }

# -----------------------------------------------------------------------------
# Network: surface the host's LAN IPs so the user knows where a freshly
# started API / Web / MCP server can be reached from another device.
# Implemented locally to avoid depending on the sub-script's _common.ps1.
# -----------------------------------------------------------------------------
function Get-LocalIPs-Quick {
    $ips = @()
    try {
        $hn = [System.Net.Dns]::GetHostName()
        $ips = @([System.Net.Dns]::GetHostAddresses($hn)) |
            Where-Object { $_.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
            Where-Object { $_.ToString() -notmatch '^127\.' -and $_.ToString() -notmatch '^169\.254\.' } |
            ForEach-Object { $_.ToString() } |
            Sort-Object -Unique
    } catch { }
    if (-not $ips) { $ips = @('(no LAN IP detected)') }
    return $ips
}

# -----------------------------------------------------------------------------
# Dispatcher wrapper.
#
# Streams the sub-script's output live (no Out-Null — the user wants to
# see what is happening), wraps it in a visual banner with elapsed time,
# and for "run" actions surfaces the host's LAN IPs so the user knows
# which URL to open.
# -----------------------------------------------------------------------------
function Invoke-Cardscape {
    param([Parameter(Mandatory)][string[]]$Args)

    $cmd = "pwsh scripts/cardscape.ps1 $($Args -join ' ')"

    Write-Host ''
    Write-Host ('  ' + ('-' * 60)) -ForegroundColor DarkGray
    Write-Host "  $cmd" -ForegroundColor Cyan
    Write-Host ('  ' + ('-' * 60)) -ForegroundColor DarkGray

    # For "run" actions, show the LAN IPs BEFORE the service starts so the
    # user knows which hostnames to try once the sub-script reports the ports.
    if ($Args.Count -gt 0 -and $Args[0] -eq 'run') {
        # The sub-script knows the default URLs from launchSettings.json; mirror
        # that knowledge here so the user sees real URLs (not <port> placeholders)
        # before the service starts.
        $service = if ($Args.Count -ge 2) { $Args[1] } else { 'api' }
        $defaultUrls = switch ($service) {
            'api' { @('http://localhost:5291', 'https://localhost:7259') }
            'web' { @('http://localhost:5292', 'https://localhost:7253') }
            'mcp' { @('http://localhost:5100') }
            default { @() }
        }

        Write-Host ''
        Write-Host "  This machine ($service)" -ForegroundColor DarkCyan
        foreach ($u in $defaultUrls) { Write-Host "    $u" -ForegroundColor DarkGray }
        Write-Host '  Reachable from the LAN as' -ForegroundColor DarkCyan
        $ips = Get-LocalIPs-Quick
        foreach ($ip in $ips) {
            foreach ($u in $defaultUrls) {
                $proto = ($u -split '://')[0]
                $port  = if ($u -match ':\d+') { ($u -split ':')[-1] } else { '' }
                if ($port) {
                    # Wrap the -f expression in parens so PowerShell doesn't
                    # bind $ip to -ForegroundColor by mistake.
                    $line = "    {0}://{1}:{2}" -f $proto, $ip, $port
                    Write-Host $line -ForegroundColor DarkGray
                }
            }
        }
        Write-Host ''
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    # Stream the child's output — no Out-Null.
    & pwsh $Dispatcher @Args
    $code = $LASTEXITCODE
    $sw.Stop()
    $elapsed = ('{0:N1}s' -f $sw.Elapsed.TotalSeconds)

    Write-Host ''
    Write-Host ('  ' + ('-' * 60)) -ForegroundColor DarkGray
    if ($code -eq 0) {
        Good "  done in $elapsed"
    } else {
        Alert "  failed (exit $code) after $elapsed"
    }
    Write-Host ('  ' + ('-' * 60)) -ForegroundColor DarkGray
}

# =============================================================================
# Welcome banner (one-time)
# =============================================================================
if (-not $SkipWelcome -and -not $NonInteractive) {
    Title
    Muted '  type a number and press Enter. 0 to exit. Ctrl+C any time to bail.'
    Muted '  all commands are also runnable directly:'
    Muted '    pwsh scripts/cardscape.ps1 <command> [options]'
    Muted '    pwsh scripts/<command>.ps1 [options]'
}

# =============================================================================
# Sub-menus
# =============================================================================
function Menu-Build {
    Section 'Build & restore'
    Write-Host '   1) Debug (default)'               -ForegroundColor White
    Write-Host '   2) Release'                        -ForegroundColor White
    Write-Host '   3) Build only (no restore, fast)'  -ForegroundColor White
    Write-Host '   4) Restore only'                   -ForegroundColor White
    Write-Host '   5) Build + run tests'              -ForegroundColor White
    Write-Host '   b) Back'                           -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('build') }
        '2' { Invoke-Cardscape @('build', '-Release') }
        '3' { Invoke-Cardscape @('build', '-NoRestore') }
        '4' { & dotnet restore (Join-Path $RepoRoot 'Cardscape.slnx') }
        '5' { Invoke-Cardscape @('build', '-RunTests') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Test {
    Section 'Test'
    Write-Host '   1) Run all (default)'                  -ForegroundColor White
    Write-Host '   2) Unit tests only'                   -ForegroundColor White
    Write-Host '   3) Integration tests only'            -ForegroundColor White
    Write-Host '   4) Architecture tests only'           -ForegroundColor White
    Write-Host '   5) Functional tests only'             -ForegroundColor White
    Write-Host '   6) Unit + coverage (coverlet)'        -ForegroundColor White
    Write-Host '   7) Live-watch unit tests'             -ForegroundColor White
    Write-Host '   8) Custom filter'                     -ForegroundColor White
    Write-Host '   b) Back'                              -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('test') }
        '2' { Invoke-Cardscape @('test', '-Unit') }
        '3' { Invoke-Cardscape @('test', '-Integration') }
        '4' { Invoke-Cardscape @('test', '-Architecture') }
        '5' { Invoke-Cardscape @('test', '-Functional') }
        '6' { Invoke-Cardscape @('test', '-Unit', '-Coverage') }
        '7' { Invoke-Cardscape @('test', '-Unit', '-Watch') }
        '8' {
            $f = Read-Host '   filter (e.g. "FullyQualifiedName~Workspaces")'
            if ($f) { Invoke-Cardscape @('test', '-Filter', $f) }
        }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Run {
    Section 'Run a service'
    Write-Host '   1) API + Blazor (default)'   -ForegroundColor White
    Write-Host '   2) Web (Blazor WASM client)' -ForegroundColor White
    Write-Host '   3) MCP server'              -ForegroundColor White
    Write-Host '   4) API with PostgreSQL'     -ForegroundColor White
    Write-Host '   5) API with MariaDB'        -ForegroundColor White
    Write-Host '   6) API with custom URL'     -ForegroundColor White
    Write-Host '   b) Back'                    -ForegroundColor DarkGray
    Muted '   tip: services block until you Ctrl+C them — output streams live.'
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('run', 'api') }
        '2' { Invoke-Cardscape @('run', 'web') }
        '3' { Invoke-Cardscape @('run', 'mcp') }
        '4' { Invoke-Cardscape @('run', 'api', '-Database', 'PostgreSQL') }
        '5' { Invoke-Cardscape @('run', 'api', '-Database', 'MariaDB') }
        '6' {
            $u = Read-Host '   ASPNETCORE_URLS (e.g. http://localhost:5291)'
            if ($u) { Invoke-Cardscape @('run', 'api', '--', '--urls', $u) }
        }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Migrate {
    Section 'EF Core migrations'
    Write-Host '   1) List applied + pending'   -ForegroundColor White
    Write-Host '   2) Apply pending (Sqlite)'   -ForegroundColor White
    Write-Host '   3) Apply pending (Postgres)' -ForegroundColor White
    Write-Host '   4) Apply pending (MariaDB)'  -ForegroundColor White
    Write-Host '   5) Add a new migration'      -ForegroundColor White
    Write-Host '   6) Generate SQL script'      -ForegroundColor White
    Write-Host '   7) Drop the database'        -ForegroundColor White
    Write-Host '   8) Remove last migration'    -ForegroundColor White
    Write-Host '   9) Build self-contained efbundle' -ForegroundColor White
    Write-Host '   b) Back'                     -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('migrate', 'list') }
        '2' { Invoke-Cardscape @('migrate', 'apply', '-Database', 'Sqlite') }
        '3' { Invoke-Cardscape @('migrate', 'apply', '-Database', 'PostgreSQL') }
        '4' { Invoke-Cardscape @('migrate', 'apply', '-Database', 'MariaDB') }
        '5' {
            $n = Read-Host '   migration name (e.g. IssueFooBar)'
            if ($n) { Invoke-Cardscape @('migrate', 'add', $n) }
        }
        '6' {
            $o = Read-Host '   output path (Enter for migrations.sql)'
            $args = @('migrate', 'script')
            if ($o) { $args += @('-Output', $o) }
            Invoke-Cardscape $args
        }
        '7' {
            Alert '   This DROPS the database. The sub-script will ask for confirmation.'
            $db = Read-Host '   provider [Sqlite/PostgreSQL/MariaDB] (Enter for Sqlite)'
            if (-not $db) { $db = 'Sqlite' }
            Invoke-Cardscape @('migrate', 'drop', '-Database', $db)
        }
        '8' { Invoke-Cardscape @('migrate', 'remove') }
        '9' { Invoke-Cardscape @('migrate', 'bundle') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Database {
    Section 'Database'
    Write-Host '   1) Show current provider + connection'   -ForegroundColor White
    Write-Host '   2) Reset Sqlite (drop + re-apply)'       -ForegroundColor White
    Write-Host '   3) Reset PostgreSQL (drop + re-apply)'   -ForegroundColor White
    Write-Host '   4) Open Sqlite file (sqlite3 CLI / OS)'  -ForegroundColor White
    Write-Host '   5) List tables'                          -ForegroundColor White
    Write-Host '   b) Back'                                 -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('db', 'info') }
        '2' {
            Alert '   This DROPS the database. The sub-script will ask for confirmation.'
            Invoke-Cardscape @('db', 'reset', '-Database', 'Sqlite')
        }
        '3' {
            Alert '   This DROPS the database. The sub-script will ask for confirmation.'
            Invoke-Cardscape @('db', 'reset', '-Database', 'PostgreSQL')
        }
        '4' { Invoke-Cardscape @('db', 'open') }
        '5' { Invoke-Cardscape @('db', 'tables') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Docker {
    Section 'Docker compose'
    Write-Host '   1) Up dev stack (Sqlite only, -Detached)' -ForegroundColor White
    Write-Host '   2) Up full stack (with Postgres, -Detached)' -ForegroundColor White
    Write-Host '   3) Up attached (foreground logs)' -ForegroundColor White
    Write-Host '   4) Down (keep volumes)' -ForegroundColor White
    Write-Host '   5) Down + drop volumes'  -ForegroundColor White
    Write-Host '   6) Tail logs (cardscape.api)' -ForegroundColor White
    Write-Host '   7) Tail logs (all services)' -ForegroundColor White
    Write-Host '   8) Status (docker compose ps)' -ForegroundColor White
    Write-Host '   9) Rebuild images' -ForegroundColor White
    Write-Host '   b) Back' -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('docker', 'up',   '-Dev', '-Detached') }
        '2' { Invoke-Cardscape @('docker', 'up',   '-Detached') }
        '3' { Invoke-Cardscape @('docker', 'up') }
        '4' { Invoke-Cardscape @('docker', 'down') }
        '5' { Invoke-Cardscape @('docker', 'down', '-V') }
        '6' { Invoke-Cardscape @('docker', 'logs', '-Service', 'cardscape.api') }
        '7' { Invoke-Cardscape @('docker', 'logs') }
        '8' { Invoke-Cardscape @('docker', 'ps') }
        '9' { Invoke-Cardscape @('docker', 'build') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Format {
    Section 'Format'
    Write-Host '   1) Apply (dotnet format)'  -ForegroundColor White
    Write-Host '   2) Verify only (CI gate)'  -ForegroundColor White
    Write-Host '   3) Apply (warnings only)'  -ForegroundColor White
    Write-Host '   4) Verify (warnings only)' -ForegroundColor White
    Write-Host '   b) Back'                   -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('format') }
        '2' { Invoke-Cardscape @('format', '-Verify') }
        '3' { Invoke-Cardscape @('format', '-Severity', 'warn') }
        '4' { Invoke-Cardscape @('format', '-Verify', '-Severity', 'warn') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Clean {
    Section 'Clean'
    Write-Host '   1) Dry run (show what would be removed)' -ForegroundColor White
    Write-Host '   2) Artifacts only (bin/, obj/, TestResults/)' -ForegroundColor White
    Write-Host '   3) + local Sqlite database' -ForegroundColor White
    Write-Host '   4) + uploaded file storage'  -ForegroundColor White
    Write-Host '   5) Everything (db + storage + artifacts)' -ForegroundColor White
    Write-Host '   b) Back' -ForegroundColor DarkGray
    $p = (Read-Host '  >').Trim().ToLowerInvariant()
    switch ($p) {
        '1' { Invoke-Cardscape @('clean', '-DryRun') }
        '2' { Invoke-Cardscape @('clean') }
        '3' { Invoke-Cardscape @('clean', '-Database', '-Force') }
        '4' { Invoke-Cardscape @('clean', '-Storage',  '-Force') }
        '5' { Invoke-Cardscape @('clean', '-All',      '-Force') }
        'b' { return }
        default { Alert "  unknown option: $p" }
    }
}

function Menu-Setup {
    Section 'Setup'
    Muted '  Validating SDK, git, restoring packages, smoke-building the solution.'
    Invoke-Cardscape @('setup')
}

# =============================================================================
# Top-level menu + main loop
# =============================================================================
function Show-Top-Menu {
    Title
    Write-Host ''
    Write-Host '   1) Build & restore'     -ForegroundColor White
    Write-Host '   2) Test'                -ForegroundColor White
    Write-Host '   3) Run a service'       -ForegroundColor White
    Write-Host '   4) Migrations (EF)'     -ForegroundColor White
    Write-Host '   5) Database'            -ForegroundColor White
    Write-Host '   6) Docker'              -ForegroundColor White
    Write-Host '   7) Format'              -ForegroundColor White
    Write-Host '   8) Clean'               -ForegroundColor White
    Write-Host '   9) Setup (first-time)'  -ForegroundColor White
    Write-Host '   h) Help (catalogue)'    -ForegroundColor White
    Write-Host '   0) Exit'                -ForegroundColor DarkGray
    Write-Host ''
}

if ($NonInteractive) {
    Show-Top-Menu
    Muted '  (non-interactive mode — exiting. Run `pwsh run.ps1` for the menu.)'
    exit 0
}

while ($true) {
    try {
        Show-Top-Menu
        $raw = Read-Host '  >'
        if ($null -eq $raw) {
            # Stdin was closed (redirected input exhausted, broken pipe, etc.).
            # Exit cleanly instead of looping on a null .Trim() forever.
            Write-Host ''
            Good '  stdin closed — exiting.'
            exit 0
        }
        $pick = $raw.Trim().ToLowerInvariant()

        switch ($pick) {
            '1' { Menu-Build }
            '2' { Menu-Test }
            '3' { Menu-Run }
            '4' { Menu-Migrate }
            '5' { Menu-Database }
            '6' { Menu-Docker }
            '7' { Menu-Format }
            '8' { Menu-Clean }
            '9' { Menu-Setup }
            'h' {
                Section 'Help'
                Muted '  The menu is a thin wrapper over scripts/cardscape.ps1.'
                Muted '  Anything reachable from the menu is also runnable directly:'
                Muted '    pwsh scripts/cardscape.ps1 <command> [options]'
                Muted '    pwsh scripts/<command>.ps1 [options]'
                Muted ''
                Muted '  Pressing Ctrl+C in any sub-script aborts back to the menu.'
            }
            '0' { Write-Host ''; Good '  Bye.'; exit 0 }
            ''  { }   # Enter alone — redraw the menu (loop continues, no pause)
            default { Alert "  unknown option: $pick" }
        }

        # Pause-and-return only after real actions, not for menu redraws or self-contained options.
        if ($pick -notin @('', '0', 'h')) {
            Write-Host ''
            Write-Host '  Press Enter to return to the menu (Ctrl+C to exit).' -ForegroundColor DarkGray
            [void](Read-Host '  >')
        }
    } catch {
        # Safety net: a binding error or any other exception in the menu
        # must not eject the user to a bare prompt. Show what happened,
        # then loop back to the top menu.
        $color = if ($Script:IsTty = [bool]([Environment]::IsInteractive)) { 'Red' } else { $null }
        Write-Host ''
        Write-Host "  [menu error] $($_.Exception.Message)" -ForegroundColor Red
        if ($_.InvocationInfo) {
            Write-Host ("    at {0}:{1}" -f $_.InvocationInfo.ScriptName, $_.InvocationInfo.ScriptLineNumber) -ForegroundColor DarkGray
        }
        Write-Host ''
        Write-Host '  Returning to the top menu...' -ForegroundColor DarkGray
        [void](Read-Host '  >')
    }
}
