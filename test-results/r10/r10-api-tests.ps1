#requires -Version 5.1
# r10-api-tests.ps1 — Cardscape v1.2.0 theming + general API regression
# R10 (post-theming-workstream).
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/api.ps1"

$LogFile = "$PSScriptRoot/r10-api-tests.log"
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
$script:LogFile = $LogFile

function R10-Log([string]$m) {
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $m"
    Add-Content -Path $LogFile -Value $line -Encoding utf8
    Write-Host $line
}

# Helper to build a JSON body as a real hashtable (avoid the
# `ConvertTo-Json` re-wrap trap that turns an already-JSON string
# into a JSON string of the string).
function Body([hashtable]$h) { $h | ConvertTo-Json -Compress }

$suffix = Get-Date -Format 'HHmmss'
$ownerEmail    = "owner-$suffix@cardscape.test"
$ownerName     = "Owner $suffix"
$ownerPassword = "P4ssw0rd!Strong"

$aliceEmail    = "alice-$suffix@cardscape.test"
$aliceName     = "Alice $suffix"
$alicePassword = "P4ssw0rd!Strong"

$bobEmail    = "bob-$suffix@cardscape.test"
$bobName     = "Bob $suffix"
$bobPassword = "P4ssw0rd!Strong"

$script:Total = 0
$script:PASS = 0
$script:FAIL = 0

R10-Log "============================================="
R10-Log "  R10 — v1.2.0 theming + regression tests"
R10-Log "  Suffix: $suffix"
R10-Log "============================================="

# ── 1. Auth: register three users ─────────────────────────────
R10-Log "--- 1. Auth ---"
$ownerBody = Body @{ email = $ownerEmail; displayName = $ownerName; password = $ownerPassword }
$ownerResp = ApiPost '/api/auth/register' -Body $ownerBody -Expect 201 -Tag 'register-owner'
$ownerToken = ($ownerResp.body | ConvertFrom-Json).accessToken
$ownerId    = ($ownerResp.body | ConvertFrom-Json).user.id
R10-Log "  Owner id: $ownerId"

$aliceBody = Body @{ email = $aliceEmail; displayName = $aliceName; password = $alicePassword }
$aliceResp = ApiPost '/api/auth/register' -Body $aliceBody -Expect 201 -Tag 'register-alice'
$aliceToken = ($aliceResp.body | ConvertFrom-Json).accessToken
$aliceId    = ($aliceResp.body | ConvertFrom-Json).user.id
R10-Log "  Alice id: $aliceId"

$bobBody = Body @{ email = $bobEmail; displayName = $bobName; password = $bobPassword }
$bobResp = ApiPost '/api/auth/register' -Body $bobBody -Expect 201 -Tag 'register-bob'
$bobToken = ($bobResp.body | ConvertFrom-Json).accessToken
$bobId    = ($bobResp.body | ConvertFrom-Json).user.id
R10-Log "  Bob id: $bobId"

ApiGet '/api/auth/me' -Token $ownerToken -Expect 200 -Tag 'me-owner'
ApiPost '/api/auth/register' -Body $ownerBody -Expect 400 -Tag 'register-dup-400'

# ── 2. UserPreferences: the v1.2.0 theming surface ───────────
R10-Log "--- 2. UserPreferences (v1.2.0 theming) ---"
$prefs = ApiGet '/api/users/me/preferences' -Token $ownerToken -Expect 200 -Tag 'get-prefs-fresh'
R10-Log "  Fresh user GET body: $($prefs.body)"

ApiPost '/api/users/me/preferences' -Token $ownerToken -Body '{}' -Expect 200 -Tag 'create-prefs-default'
ApiPost '/api/users/me/preferences' -Token $ownerToken -Body '{}' -Expect 200 -Tag 'create-prefs-idempotent'

# 12 themes from ThemeCatalog.cs:68-90
$themes = @(
    'default','dark',
    'humanistic','humanistic-dark',
    'material','material-dark',
    'software','software-dark',
    'standard','standard-dark',
    'cardscape-classic','cardscape-classic-dark'
)
foreach ($theme in $themes) {
    $r = ApiPut '/api/users/me/preferences' -Token $ownerToken -Body (Body @{ themeName = $theme }) -Expect 200 -Tag "put-theme-$theme"
    $obj = $r.body | ConvertFrom-Json
    if ($obj.themeName -ne $theme) { R10-Log "  EXPECTED themeName=$theme, GOT $($obj.themeName)" }
}

ApiPut '/api/users/me/preferences' -Token $ownerToken -Body '{"themeName":"does-not-exist"}' -Expect 400 -Tag 'put-theme-unknown-400'

foreach ($mode in @('Light','Dark','System')) {
    ApiPut '/api/users/me/preferences' -Token $ownerToken -Body (Body @{ mode = $mode }) -Expect 200 -Tag "put-mode-$mode"
}

ApiGet  '/api/users/me/preferences'                   -Expect 401 -Tag 'get-prefs-anon-401'
ApiPost '/api/users/me/preferences' -Body '{}'         -Expect 401 -Tag 'create-prefs-anon-401'
ApiPut  '/api/users/me/preferences' -Body '{}'         -Expect 401 -Tag 'put-prefs-anon-401'

# Per-user independence
ApiPost '/api/users/me/preferences' -Token $aliceToken -Body '{}' -Expect 200 -Tag 'create-prefs-alice'
ApiPut  '/api/users/me/preferences' -Token $aliceToken -Body (Body @{ themeName = 'humanistic'; mode = 'Dark' }) -Expect 200 -Tag 'put-prefs-alice'
$alicePrefs = (ApiGet '/api/users/me/preferences' -Token $aliceToken -Expect 200 -Tag 'get-prefs-alice').body | ConvertFrom-Json
if ($alicePrefs.themeName -ne 'humanistic' -or $alicePrefs.mode -ne 'Dark') {
    R10-Log "  Alice prefs EXPECTED humanistic/Dark, GOT $($alicePrefs.themeName)/$($alicePrefs.mode)"
}
$ownerPrefs = (ApiGet '/api/users/me/preferences' -Token $ownerToken -Expect 200 -Tag 'get-prefs-owner-after').body | ConvertFrom-Json
R10-Log "  Owner prefs still: $($ownerPrefs.themeName)/$($ownerPrefs.mode)"

# ── 3. Workspace + Board + List + Card ────────────────────────
R10-Log "--- 3. CRUD: workspace/board/list/card ---"
$wsBody = Body @{ name = "R10 Workspace $suffix"; region = 'unspecified' }
$wsResp = ApiPost '/api/workspaces' -Token $ownerToken -Body $wsBody -Expect 201 -Tag 'create-workspace'
$workspaceId = ($wsResp.body | ConvertFrom-Json).id
R10-Log "  Workspace id: $workspaceId"

$boardBody = Body @{ workspaceId = $workspaceId; name = "R10 Board $suffix"; visibility = 'private' }
$boardResp = ApiPost '/api/boards' -Token $ownerToken -Body $boardBody -Expect 201 -Tag 'create-board'
$boardId = ($boardResp.body | ConvertFrom-Json).id
R10-Log "  Board id: $boardId"

$listBody = Body @{ boardId = $boardId; name = 'To Do' }
$listResp = ApiPost '/api/lists' -Token $ownerToken -Body $listBody -Expect 201 -Tag 'create-list'
$listId = ($listResp.body | ConvertFrom-Json).id
R10-Log "  List id: $listId"

$cardBody = Body @{ listId = $listId; title = 'R10 first card'; description = 'beta test' }
$cardResp = ApiPost '/api/cards' -Token $ownerToken -Body $cardBody -Expect 201 -Tag 'create-card'
$cardId = ($cardResp.body | ConvertFrom-Json).id
R10-Log "  Card id: $cardId"

ApiPost "/api/cards/$cardId/rename" -Token $ownerToken -Body (Body @{ title = 'R10 first card (updated)' }) -Expect 200 -Tag 'rename-card'

$newListBody = Body @{ boardId = $boardId; name = 'In Progress' }
$newListResp = ApiPost '/api/lists' -Token $ownerToken -Body $newListBody -Expect 201 -Tag 'create-list-2'
$newListId = ($newListResp.body | ConvertFrom-Json).id
ApiPost "/api/cards/$cardId/move" -Token $ownerToken -Body (Body @{ listId = $newListId; position = 0 }) -Expect 200 -Tag 'move-card'

# ── 4. Comments + Checklists ──────────────────────────────────
R10-Log "--- 4. Comments + Checklists ---"
$commentResp = ApiPost "/api/cards/$cardId/comments" -Token $ownerToken -Body (Body @{ body = 'first comment' }) -Expect 201 -Tag 'create-comment'
$commentId = ($commentResp.body | ConvertFrom-Json).id
ApiPut "/api/comments/$commentId" -Token $ownerToken -Body (Body @{ newBody = 'updated' }) -Expect 200 -Tag 'update-comment'

$checklistResp = ApiPost "/api/cards/$cardId/checklists" -Token $ownerToken -Body (Body @{ title = 'Acceptance checklist' }) -Expect 201 -Tag 'create-checklist'
$checklistId = ($checklistResp.body | ConvertFrom-Json).id

$itemResp = ApiPost "/api/checklists/$checklistId/items" -Token $ownerToken -Body (Body @{ text = 'first item' }) -Expect 200 -Tag 'add-checklist-item'
$itemId = ($itemResp.body | ConvertFrom-Json).id
ApiPatch "/api/checklists/$checklistId/items/$itemId/toggle" -Token $ownerToken -Expect 200 -Tag 'check-checklist-item'

# ── 5. Card archive/restore ───────────────────────────────────
R10-Log "--- 5. Card archive/restore ---"
ApiPost "/api/cards/$cardId/archive" -Token $ownerToken -Expect 200 -Tag 'archive-card'
ApiPost "/api/cards/$cardId/restore" -Token $ownerToken -Expect 200 -Tag 'restore-card'

# ── 6. GDPR: bob self-deletes ─────────────────────────────────
R10-Log "--- 6. GDPR ---"
ApiDelete '/api/users/me' -Token $bobToken -Expect 204 -Tag 'bob-self-delete'
# After soft-delete, the user row stays (30-day grace period before
# the retention sweeper hard-deletes it), so re-registering the same
# email must 400. The preferences row IS hard-deleted by the
# SoftDeleteUserCommandHandler as part of the GDPR cascade.
ApiPost '/api/auth/register' -Body $bobBody -Expect 400 -Tag 'bob-reregister-after-delete'
# Anonymous GET on preferences still 401 (no auth)
ApiGet  '/api/users/me/preferences'                  -Expect 401 -Tag 'bob-prefs-anon-after-delete'

# ── 7. Final summary ──────────────────────────────────────────
R10-Log "============================================="
R10-Log "  R10 result: $script:PASS / $script:Total passed ($script:FAIL failed)"
R10-Log "============================================="
if ($script:FAIL -gt 0) {
    exit 1
}
