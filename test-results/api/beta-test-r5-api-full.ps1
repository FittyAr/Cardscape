#requires -Version 7.0
# Beta R5 â€” exhaustive API end-to-end exercise (v2 â€” diagnostic)
# Run with: pwsh -NoProfile -NonInteractive -File beta-test-r5-api-full.ps1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$api = 'http://localhost:8080'
$env:CARDS_CAPE_JWT_KEY = 'dev-only-insecure-signing-key-please-override-in-production-32+chars'

$ts = (Get-Date).ToString('HHmmss')
$findings = New-Object System.Collections.Generic.List[object]
$total = 0
$passed = 0
$failures = New-Object System.Collections.Generic.List[object]

function Add-Finding { param($a,$t,$s,$d) $script:findings.Add([PSCustomObject]@{ Area=$a; Test=$t; Status=$s; Detail=$d }) }
function Run-Case {
    param([string]$Area, [string]$Name, [bool]$Ok, [string]$Detail = '')
    $script:total++
    if ($Ok) { $script:passed++ } else { $script:failures.Add([PSCustomObject]@{ Area=$Area; Test=$Name; Detail=$Detail }) }
    $tag = if ($Ok) { 'PASS' } else { 'FAIL' }
    Write-Host "  [$tag] $Area :: $Name  $Detail"
    Add-Finding $Area $Name $tag $Detail
}
function Auth-Header($token) { @{ Authorization = "Bearer $token" } }
function Merge-Headers($token, $extra) {
    $h = Auth-Header $token
    foreach ($k in $extra.Keys) { $h[$k] = $extra[$k] }
    return $h
}

# Call returns @{Status, Parsed, Raw}
function Call($method, $path, $token = $null, $body = $null, $extraHeaders = @{}, $expectAny = $false) {
    $headers = if ($null -ne $token) { Merge-Headers $token $extraHeaders } else { $extraHeaders.Clone() }
    $params = @{
        Method = $method; Uri = "$api$path"
        Headers = $headers; ContentType = 'application/json'
    }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json) }
    try {
        $r = Invoke-WebRequest @params -UseBasicParsing -SkipHttpErrorCheck
        $status = [int]$r.StatusCode
        $raw = $r.Content
        $parsed = $null
        if ($raw) {
            try { $parsed = ($raw | ConvertFrom-Json) } catch { $parsed = $null }
        }
        return @{ Status = $status; Parsed = $parsed; Raw = $raw; Headers = $r.Headers }
    } catch {
        return @{ Status = 0; Parsed = $null; Raw = $_.Exception.Message; Headers = @{} }
    }
}

function Get-Body-Snippet($raw) {
    if ($null -eq $raw) { return '<null>' }
    if ($raw -is [byte[]]) { $raw = [System.Text.Encoding]::UTF8.GetString($raw) }
    if ($raw.Length -gt 200) { $raw = $raw.Substring(0, 200) + '...' }
    return $raw
}

function Assert2xx($a, $t, $r) {
    $ok = $r.Status -ge 200 -and $r.Status -lt 300
    $detail = if ($ok) { "status=$($r.Status)" } else { "status=$($r.Status) body=$(Get-Body-Snippet $r.Raw)" }
    Run-Case $a $t $ok $detail
}

function Assert4xx($a, $t, $r) {
    $ok = $r.Status -ge 400 -and $r.Status -lt 500
    $detail = if ($ok) { "status=$($r.Status)" } else { "status=$($r.Status) body=$(Get-Body-Snippet $r.Raw)" }
    Run-Case $a $t $ok $detail
}

Write-Host "=========================================="
Write-Host "R5 EXHAUSTIVE API BETA"
Write-Host "=========================================="

# â”€â”€â”€ 1. Auth â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 1. AUTH â”€â”€"
$aliceEmail = "r5.alice.$ts@cardscape.test"
$bobEmail = "r5.bob.$ts@cardscape.test"
$pass = 'TestPass123!'

$r = Call POST '/api/auth/register' $null @{ email = $aliceEmail; displayName = 'Alice R5'; password = $pass }
Assert2xx 'Auth' 'register alice' $r
$aliceToken = $r.Parsed.accessToken
$aliceId = $r.Parsed.user.id

$r = Call POST '/api/auth/register' $null @{ email = $bobEmail; displayName = 'Bob R5'; password = $pass }
Assert2xx 'Auth' 'register bob' $r
$bobToken = $r.Parsed.accessToken
$bobId = $r.Parsed.user.id

$r = Call POST '/api/auth/login' $null @{ email = $aliceEmail; password = $pass }
Assert2xx 'Auth' 'login alice' $r
$aliceRefresh = $r.Parsed.refreshToken

$r = Call POST '/api/auth/login' $null @{ email = $aliceEmail; password = 'wrong' }
Assert4xx 'Auth' 'login wrong password' $r

$r = Call GET '/api/auth/me' $aliceToken
Run-Case 'Auth' '/me returns alice' ($r.Parsed.email -eq $aliceEmail) "email=$($r.Parsed.email)"

$r = Call POST '/api/auth/refresh' $null @{ refreshToken = $aliceRefresh; accessToken = $aliceToken }
Assert2xx 'Auth' 'refresh token' $r

$r = Call POST '/api/auth/register' $null @{ email = $aliceEmail; displayName = 'Alice Dup'; password = $pass }
Assert4xx 'Auth' 'duplicate register' $r

$r = Call POST '/api/auth/register' $null @{ email = "r5.weak.$ts@cardscape.test"; displayName = 'Weak'; password = 'x' }
Assert4xx 'Auth' 'weak password' $r

$r = Call POST '/api/auth/register' $null @{ email = 'not-an-email'; displayName = 'X'; password = $pass }
Assert4xx 'Auth' 'bad email' $r

$r = Call GET '/api/auth/me' $null
Run-Case 'Auth' '/me no token 401' ($r.Status -eq 401) "status=$($r.Status)"

# Logout
$r = Call POST '/api/auth/logout' $aliceToken
Assert2xx 'Auth' 'logout' $r
# Re-login alice (logout invalidates the token)
$r = Call POST '/api/auth/login' $null @{ email = $aliceEmail; password = $pass }
Assert2xx 'Auth' 're-login after logout' $r
$aliceToken = $r.Parsed.accessToken

# â”€â”€â”€ 2. Workspaces â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 2. WORKSPACES â”€â”€"
$r = Call POST '/api/workspaces' $aliceToken @{ name = "R5 WS $ts"; region = 0 }
Assert2xx 'Workspaces' 'create' $r
$wsId = $r.Parsed.id

$r = Call GET '/api/workspaces' $aliceToken
Run-Case 'Workspaces' 'list contains new ws' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call GET "/api/workspaces/$wsId" $aliceToken
Run-Case 'Workspaces' 'get by id' ($r.Parsed.id -eq $wsId) "id=$($r.Parsed.id)"

$r = Call POST "/api/workspaces/$wsId/rename" $aliceToken @{ name = "R5 WS renamed" }
Run-Case 'Workspaces' 'update name' ($r.Parsed.name -eq 'R5 WS renamed') "name=$($r.Parsed.name) status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call GET '/api/workspaces' $bobToken
Run-Case 'Workspaces' 'bob does not see alice ws' (-not ($r.Parsed | Where-Object { $_.id -eq $wsId })) "bob_count=$($r.Parsed.Count)"

# â”€â”€â”€ 3. Members â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 3. MEMBERS â”€â”€"
$r = Call POST "/api/workspaces/$wsId/invitations" $aliceToken @{ email = $bobEmail; role = 0 }
Assert2xx 'Members' 'invite bob' $r
$inviteToken = $r.Parsed.cleartextToken

$r = Call GET "/api/workspaces/$wsId/invitations" $aliceToken
Run-Case 'Members' 'list invitations 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call POST '/api/invitations/accept' $bobToken @{ token = $inviteToken }
Run-Case 'Members' 'bob accepts' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call GET '/api/workspaces' $bobToken
Run-Case 'Members' 'bob sees ws after accept' ($null -ne ($r.Parsed | Where-Object { $_.id -eq $wsId })) ""

$r = Call GET "/api/workspaces/$wsId/members" $bobToken
Run-Case 'Members' 'list members has 2' ($r.Parsed.Count -ge 2) "count=$($r.Parsed.Count)"

# â”€â”€â”€ 4. Boards â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 4. BOARDS â”€â”€"
$r = Call POST '/api/boards' $aliceToken @{ workspaceId = $wsId; name = "R5 Board"; description = "test"; visibility = 0 }
Assert2xx 'Boards' 'create' $r
$boardId = $r.Parsed.id

$r = Call GET "/api/boards?workspaceId=$wsId" $aliceToken
Run-Case 'Boards' 'list for ws 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call GET "/api/boards/$boardId" $aliceToken
Run-Case 'Boards' 'get by id' ($r.Parsed.id -eq $boardId) ""

$r = Call POST "/api/boards/$boardId/rename" $aliceToken @{ newName = "R5 Board renamed" }
Run-Case 'Boards' 'update name via POST' ($r.Parsed.name -eq 'R5 Board renamed') "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"
$r = Call POST "/api/boards/$boardId/description" $aliceToken @{ newDescription = "updated" }
Run-Case 'Boards' 'update description' ($r.Parsed.description -eq 'updated') "status=$($r.Status)"

$r = Call POST "/api/boards/$boardId/star" $aliceToken
Run-Case 'Boards' 'star' ($r.Parsed.isStarred -eq $true) "isStarred=$($r.Parsed.isStarred) status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call GET '/api/boards/starred' $aliceToken
Run-Case 'Boards' 'list starred contains board' ($null -ne ($r.Parsed | Where-Object { $_.id -eq $boardId })) ""

$r = Call DELETE "/api/boards/$boardId/star" $aliceToken
Run-Case 'Boards' 'unstar 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call POST "/api/boards/$boardId/visibility" $aliceToken @{ newVisibility = 0 }
$vis = $r.Parsed.visibility
$visInt = if ($vis -is [string]) { switch ($vis) { "private" { 0 } "workspace" { 1 } "public" { 2 } default { -1 } } } else { [int]$vis }
Run-Case 'Boards' 'update visibility private' ($visInt -eq 0) "status=$($r.Status) visibility=$vis"

# Non-member cannot see private board
$r = Call POST '/api/auth/register' $null @{ email = "r5.eve.$ts@cardscape.test"; displayName = 'Eve'; password = $pass }
$eveToken = $r.Parsed.accessToken
$r = Call GET "/api/boards/$boardId" $eveToken
Run-Case 'Boards' 'non-member cannot see private board' ($r.Status -eq 403 -or $r.Status -eq 404) "status=$($r.Status)"

$r = Call POST "/api/boards/$boardId/visibility" $aliceToken @{ newVisibility = 1 }
Assert2xx 'Boards' 'back to workspace visibility' $r

# â”€â”€â”€ 5. Lists â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 5. LISTS â”€â”€"
$r = Call POST '/api/lists' $aliceToken @{ boardId = $boardId; name = "To Do" }
Assert2xx 'Lists' 'create 1' $r
$listId1 = $r.Parsed.id
$r = Call POST '/api/lists' $aliceToken @{ boardId = $boardId; name = "Doing" }
$listId2 = $r.Parsed.id
$r = Call POST '/api/lists' $aliceToken @{ boardId = $boardId; name = "Done" }
$listId3 = $r.Parsed.id

$r = Call GET "/api/lists?boardId=$boardId" $aliceToken
Run-Case 'Lists' 'list for board 3' ($r.Parsed.Count -eq 3) "count=$($r.Parsed.Count)"

$r = Call POST "/api/lists/$listId1/rename" $aliceToken @{ newName = "To Do (renamed)" }
Run-Case 'Lists' 'update name' ($r.Parsed.name -eq 'To Do (renamed)') "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

# â”€â”€â”€ 6. Cards â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 6. CARDS â”€â”€"
$r = Call POST '/api/cards' $aliceToken @{ listId = $listId1; title = "Card 1"; description = "first card" }
Assert2xx 'Cards' 'create' $r
$cardId = $r.Parsed.id

$r = Call GET "/api/cards?boardId=$boardId" $aliceToken
Run-Case 'Cards' 'list for board 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call GET "/api/cards/$cardId" $aliceToken
Run-Case 'Cards' 'get by id' ($r.Parsed.id -eq $cardId) ""

$r = Call POST "/api/cards/$cardId/rename" $aliceToken @{ newTitle = "Card 1 renamed" }
Run-Case 'Cards' 'update via rename' ($r.Parsed.title -eq 'Card 1 renamed') "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"
$r = Call POST "/api/cards/$cardId/description" $aliceToken @{ newDescription = "updated" }
Run-Case 'Cards' 'update description' ($r.Parsed.description -eq 'updated') "status=$($r.Status)"

$r = Call POST "/api/cards/$cardId/rename" $aliceToken @{ newTitle = "Renamed via POST" }
Run-Case 'Cards' 'rename via POST' ($r.Parsed.title -eq 'Renamed via POST') ""

$r = Call POST "/api/cards/$cardId/move" $aliceToken @{ newListId = $listId2; newPosition = 0 }
Run-Case 'Cards' 'move list' ($r.Parsed.listId -eq $listId2) ""

$r = Call POST "/api/cards/$cardId/complete" $aliceToken
Run-Case 'Cards' 'complete' ($r.Parsed.isCompleted -eq $true) ""

$r = Call POST "/api/cards/$cardId/reopen" $aliceToken
Run-Case 'Cards' 'reopen' ($r.Parsed.isCompleted -eq $false) ""

$r = Call POST "/api/cards/$cardId/assign/$aliceId" $aliceToken
Run-Case 'Cards' 'assign alice 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call DELETE "/api/cards/$cardId/assign/$aliceId" $aliceToken
Run-Case 'Cards' 'unassign 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call POST "/api/cards/$cardId/archive" $aliceToken
Assert2xx 'Cards' 'archive' $r

$r = Call POST "/api/cards/$cardId/restore" $aliceToken
Assert2xx 'Cards' 'restore' $r

$r = Call DELETE "/api/cards/$cardId" $aliceToken
Run-Case 'Cards' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call GET "/api/cards/$cardId" $aliceToken
Run-Case 'Cards' 'get after delete 404' ($r.Status -eq 404) "status=$($r.Status)"

# â”€â”€â”€ 7. Comments â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 7. COMMENTS â”€â”€"
$r = Call POST '/api/cards' $aliceToken @{ listId = $listId1; title = "Card 2" }
$card2Id = $r.Parsed.id

$r = Call POST "/api/cards/$card2Id/comments" $aliceToken @{ body = "First comment" }
Assert2xx 'Comments' 'create' $r
$commentId = $r.Parsed.id

$r = Call GET "/api/cards/$card2Id/comments" $aliceToken
Run-Case 'Comments' 'list 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call PUT "/api/comments/$commentId" $aliceToken @{ newBody = "First comment (edited)" }
Run-Case 'Comments' 'update 200' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call DELETE "/api/comments/$commentId" $aliceToken
Run-Case 'Comments' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

# â”€â”€â”€ 8. Labels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 8. LABELS â”€â”€"
$r = Call POST "/api/boards/$boardId/labels" $aliceToken @{ name = "Bug"; color = "#ff0000" }
Assert2xx 'Labels' 'create' $r
$labelId = $r.Parsed.id

$r = Call GET "/api/boards/$boardId/labels" $aliceToken
Run-Case 'Labels' 'list 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call PUT "/api/labels/$labelId" $aliceToken @{ name = "Bug (renamed)"; color = "#cc0000" }
Run-Case 'Labels' 'update 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call POST "/api/cards/$card2Id/labels/$labelId" $aliceToken
Run-Case 'Labels' 'attach 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call DELETE "/api/cards/$card2Id/labels/$labelId" $aliceToken
Run-Case 'Labels' 'detach 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call DELETE "/api/labels/$labelId" $aliceToken
Run-Case 'Labels' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

# â”€â”€â”€ 9. Checklists â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 9. CHECKLISTS â”€â”€"
$r = Call POST "/api/cards/$card2Id/checklists" $aliceToken @{ title = "My checklist" }
Assert2xx 'Checklists' 'create' $r
$clId = $r.Parsed.id

$r = Call POST "/api/checklists/$clId/items/" $aliceToken @{ text = "Item 1" }
Assert2xx 'Checklists' 'add item' $r
$itemId = $r.Parsed.items[0].id

$r = Call GET "/api/cards/$card2Id/checklists" $aliceToken
Run-Case 'Checklists' 'list 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"

$r = Call PATCH "/api/checklists/$clId/items/$itemId/toggle" $aliceToken
Run-Case 'Checklists' 'toggle 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call PATCH "/api/checklists/$clId/items/$itemId/rename" $aliceToken @{ text = "Item 1 renamed" }
Run-Case 'Checklists' 'rename item 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call DELETE "/api/checklists/$clId/items/$itemId" $aliceToken
Run-Case 'Checklists' 'delete item 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call DELETE "/api/checklists/$clId" $aliceToken
Run-Case 'Checklists' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

# â”€â”€â”€ 10. Voting â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 10. VOTING â”€â”€"
$r = Call POST "/api/cards/$card2Id/votes" $aliceToken
Run-Case 'Voting' 'toggle 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call GET "/api/cards/$card2Id/votes" $aliceToken
Run-Case 'Voting' 'list 200' ($r.Status -eq 200) "count=$($r.Parsed.voteCount)"

# Toggle back
$r = Call POST "/api/cards/$card2Id/votes" $aliceToken
Run-Case 'Voting' 'second toggle' ($r.Status -ge 200 -and $r.Status -lt 300) "votedByMe=$($r.Parsed.currentUserHasVoted)"

# â”€â”€â”€ 11. Custom Fields â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 11. CUSTOM FIELDS â”€â”€"
$r = Call POST "/api/boards/$boardId/custom-fields" $aliceToken @{ name = "Estimate"; kind = 0 }
Run-Case 'CustomFields' 'create 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

# â”€â”€â”€ 12. Activities â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 12. ACTIVITIES â”€â”€"
$r = Call GET "/api/boards/$boardId/activities" $aliceToken
Assert2xx 'Activities' 'board' $r
$r = Call GET "/api/cards/$card2Id/activities" $aliceToken
Assert2xx 'Activities' 'card' $r

# â”€â”€â”€ 13. Notifications â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 13. NOTIFICATIONS â”€â”€"
$r = Call GET '/api/notifications' $aliceToken
Assert2xx 'Notifications' 'list' $r

# â”€â”€â”€ 14. Recurrence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 14. RECURRENCE â”€â”€"
$r = Call POST '/api/cards' $aliceToken @{ listId = $listId1; title = "Recurring" }
$recCardId = $r.Parsed.id
$r = Call PUT "/api/cards/$recCardId/recurrence/" $aliceToken @{ intervalDays = 1; firstOccurrenceAt = (Get-Date).ToString('o') }
Run-Case 'Recurrence' 'set 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

# â”€â”€â”€ 15. Search â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 15. SEARCH â”€â”€"
$r = Call GET "/api/search?q=Card" $aliceToken
Assert2xx 'Search' '200' $r

# â”€â”€â”€ 16. Idempotency-Key re-confirm â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 16. IDEMPOTENCY-KEY â”€â”€"
$idk1 = "r5-idem-$ts-AAAAAAAA"
$bodyI = @{ listId = $listId1; title = "Idem card" } | ConvertTo-Json
$hdrI = @{ 'Idempotency-Key' = $idk1 }
$r1 = Call POST '/api/cards' $aliceToken @{ listId = $listId1; title = "Idem card" } $hdrI
$cardI1 = $r1.Parsed.id
$r2 = Call POST '/api/cards' $aliceToken @{ listId = $listId1; title = "Idem card" } $hdrI
$cardI2 = $r2.Parsed.id
Run-Case 'Idempotency' 'replay same cardId' ($cardI1 -eq $cardI2) "first=$cardI1 second=$cardI2 replayed=$($r2.Headers['Idempotent-Replayed'])"

# â”€â”€â”€ 17. Error cases â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 17. ERROR CASES â”€â”€"
$r = Call GET '/api/boards/00000000-0000-0000-0000-000000000000' $aliceToken
Run-Case 'Errors' 'GET non-existent board 404' ($r.Status -eq 404) "status=$($r.Status)"

$r = Call GET '/api/boards/not-a-guid' $aliceToken
Run-Case 'Errors' 'GET bad guid 404' ($r.Status -eq 404) "status=$($r.Status)"

$r = Call POST '/api/cards' $aliceToken @{ listId = 'not-a-guid'; title = 'x' }
Run-Case 'Errors' 'create card bad listId 4xx' ($r.Status -ge 400 -and $r.Status -lt 500) "status=$($r.Status)"

$r = Call POST '/api/cards' $aliceToken @{ listId = '00000000-0000-0000-0000-000000000000'; title = 'x' }
Run-Case 'Errors' 'create card non-existent list 4xx' ($r.Status -ge 400 -and $r.Status -lt 500) "status=$($r.Status)"

$r = Call POST '/api/boards' $aliceToken @{ workspaceId = '00000000-0000-0000-0000-000000000000'; name = 'x' }
Run-Case 'Errors' 'create board with non-existent ws 4xx' ($r.Status -ge 400 -and $r.Status -lt 500) "status=$($r.Status)"

# â”€â”€â”€ 18. Security headers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 18. SECURITY HEADERS â”€â”€"
$r = Call GET '/api/auth/me' $aliceToken
$h = $r.Headers
Run-Case 'Security' 'X-Content-Type-Options' ($null -ne $h['X-Content-Type-Options']) ""
Run-Case 'Security' 'X-Frame-Options' ($null -ne $h['X-Frame-Options']) ""
Run-Case 'Security' 'Referrer-Policy' ($null -ne $h['Referrer-Policy']) ""

# â”€â”€â”€ 19. Two-user interaction â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 19. TWO-USER INTERACTION â”€â”€"
# Bob is workspace member, now also add him as board member (BETA-5-#12)
$r = Call POST "/api/boards/$boardId/members" $aliceToken @{ userId = $bobId; role = 0 }
Run-Case 'TwoUser' 'add bob to board' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"

$r = Call GET "/api/boards/$boardId" $bobToken
Run-Case 'TwoUser' 'bob sees ws-visible board' ($r.Parsed.id -eq $boardId) "status=$($r.Status)"

$r = Call POST '/api/cards' $bobToken @{ listId = $listId1; title = "Bob's card" }
Run-Case 'TwoUser' 'bob creates card' ($null -ne $r.Parsed.id) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"
$bobCardId = $r.Parsed.id

$r = Call POST "/api/cards/$bobCardId/rename" $aliceToken @{ newTitle = "Alice's edit on Bob's card" }
Run-Case 'TwoUser' 'alice edits bobs card' ($r.Parsed.title -eq "Alice's edit on Bob's card") ""

$r = Call GET "/api/cards/$bobCardId" $eveToken
Run-Case 'TwoUser' 'eve cannot see card 403/404' ($r.Status -eq 403 -or $r.Status -eq 404) "status=$($r.Status)"

$r = Call POST "/api/cards/$card2Id/votes" $bobToken
Run-Case 'TwoUser' 'bob votes on alice card 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

$r = Call POST "/api/cards/$card2Id/comments" $bobToken @{ body = "Bob's take" }
Run-Case 'TwoUser' 'bob comments 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"

# â”€â”€â”€ 20. Webhooks (BETA-4-#1/#3 verification) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 20. WEBHOOKS â”€â”€"
$r = Call POST "/api/boards/$boardId/webhooks" $aliceToken @{ url = "https://example.com/hook"; secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"; events = @("card.created","card.completed") }
Run-Case 'Webhooks' 'create 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"
if ($r.Parsed.endpoint.id) {
    $webhookId = $r.Parsed.endpoint.id
    $r = Call GET "/api/boards/$boardId/webhooks" $aliceToken
    Run-Case 'Webhooks' 'list 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"
    $r = Call DELETE "/api/boards/$boardId/webhooks/$webhookId" $aliceToken
    Run-Case 'Webhooks' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"
}

# â”€â”€â”€ 21. ApiToken â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Write-Host ""
Write-Host "â”€â”€ 21. API TOKENS â”€â”€"
$r = Call POST '/api/security/api-tokens' $aliceToken @{ name = "test-token"; scopes = @("read","write") }
Run-Case 'ApiToken' 'create 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status) body=$((Get-Body-Snippet $r.Raw))"
if ($r.Parsed.id) {
    $tokenId = $r.Parsed.id
    $r = Call GET '/api/security/api-tokens' $aliceToken
    Run-Case 'ApiToken' 'list 1+' ($r.Parsed.Count -ge 1) "count=$($r.Parsed.Count)"
    $r = Call DELETE "/api/security/api-tokens/$tokenId" $aliceToken
    Run-Case 'ApiToken' 'delete 2xx' ($r.Status -ge 200 -and $r.Status -lt 300) "status=$($r.Status)"
}

# Summary
Write-Host ""
Write-Host "=========================================="
Write-Host "TOTAL: $passed / $total passed"
Write-Host "=========================================="

$findings | Group-Object Area | ForEach-Object {
    $area = $_.Name
    $pass = ($_.Group | Where-Object { $_.Status -eq 'PASS' }).Count
    $fail = ($_.Group | Where-Object { $_.Status -eq 'FAIL' }).Count
    Write-Host "  ${area}: $pass pass / $fail fail"
}

if ($failures.Count -gt 0) {
    Write-Host ""
    Write-Host "FAILURES:"
    $failures | Select-Object Area, Test, Detail | Format-Table -AutoSize -Wrap
}

$findings | ConvertTo-Json -Depth 5 | Out-File -FilePath 'D:/GitHub/Cardscape/test-results/api/beta-test-r5-api-full.json' -Encoding UTF8
Write-Host ""
Write-Host "Findings saved to test-results/api/beta-test-r5-api-full.json"



