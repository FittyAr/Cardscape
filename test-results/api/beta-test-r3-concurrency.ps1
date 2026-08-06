#requires -Version 7.0
# Beta R3 — Concurrency / race-condition test
# Run with: pwsh -NoProfile -NonInteractive -File beta-test-r3-concurrency.ps1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$api = 'http://localhost:8080'
$jwtKey = $env:CARDS_CAPE_JWT_KEY
if ([string]::IsNullOrWhiteSpace($jwtKey)) { $env:CARDS_CAPE_JWT_KEY = 'dev-only-insecure-signing-key-please-override-in-production-32+chars' }

# --- Setup: register two users, create workspace/board/lists/card ---
$ts = (Get-Date).ToString('HHmmss')
$aliceEmail = "r3.alice.$ts@cardscape.test"
$bobEmail = "r3.bob.$ts@cardscape.test"
$pass = 'TestPass123!'

function Register-User($email, $name) {
    $body = @{ email = $email; displayName = $name; password = $pass } | ConvertTo-Json
    $r = Invoke-RestMethod -Method POST -Uri "$api/api/auth/register" -ContentType 'application/json' -Body $body
    return $r
}

function Auth-Header($token) { @{ Authorization = "Bearer $token" } }

function Api($method, $path, $token, $body = $null) {
    $params = @{
        Method = $method
        Uri = "$api$path"
        Headers = (Auth-Header $token)
        ContentType = 'application/json'
    }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json) }
    return Invoke-RestMethod @params -ErrorAction SilentlyContinue
}

Write-Host "Setup: registering users"
$alice = Register-User $aliceEmail 'Alice R3'
$bob = Register-User $bobEmail 'Bob R3'
$aliceToken = $alice.accessToken
$bobToken = $bob.accessToken
$aliceId = $alice.user.id
$bobId = $bob.user.id

Write-Host "Setup: creating workspace"
$ws = Api POST '/api/workspaces' $aliceToken @{ name = "R3 Workspace $ts"; region = 0 }
$wsId = $ws.id

Write-Host "Setup: creating board"
$board = Api POST '/api/boards' $aliceToken @{ workspaceId = $wsId; name = "R3 Board"; description = "concurrency test"; visibility = 0 }
$boardId = $board.id

Write-Host "Setup: creating lists"
$lists = @()
foreach ($n in 'To Do', 'Doing', 'Done') {
    $l = Api POST '/api/lists' $aliceToken @{ boardId = $boardId; name = $n }
    $lists += $l
}
$list1 = $lists[0].id
$list2 = $lists[1].id
$list3 = $lists[2].id

Write-Host "Setup: creating card"
$card = Api POST '/api/cards' $aliceToken @{ listId = $list1; title = "R3 Card"; description = "test" }
$cardId = $card.id

Write-Host "Setup: creating label"
$label = Api POST "/api/boards/$boardId/labels" $aliceToken @{ name = "Bug"; color = "#ff0000" }
$labelId = $label.id

Write-Host "Setup done. cardId=$cardId, list1=$list1, list2=$list2"
Write-Host ""

# --- Test 1: concurrent card moves (lost update) ---
Write-Host "=== Test 1: concurrent card moves (alternating target) ==="
$results1 = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
$jobs = 1..20 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $list1 = $using:list1
    $list2 = $using:list2
    $list3 = $using:list3
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $target = if ($i % 2 -eq 0) { $list2 } else { $list3 }
    $headers = @{ Authorization = "Bearer $token" }
    $body = @{ newListId = $target; newPosition = 0 } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/move" -Headers $headers -ContentType 'application/json' -Body $body -ErrorAction SilentlyContinue
        if ($r) { $r.id } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$statuses = $jobs | Group-Object | Sort-Object Count -Descending
Write-Host "  Status distribution:"
$statuses | ForEach-Object { Write-Host "    $($_.Name): $($_.Count)" }
$finalCard = Api GET "/api/cards/$cardId" $aliceToken
Write-Host "  Final listId: $($finalCard.listId) (expected either list2 or list3, NEVER list1)"
$ok1 = ($finalCard.listId -in @($list2, $list3)) -and ($statuses | Where-Object { $_.Name -eq '500' }).Count -eq 0
Write-Host "  Result: $(if ($ok1) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 2: concurrent card rename ---
Write-Host "=== Test 2: concurrent card rename (20 parallel) ==="
$jobs2 = 1..20 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $headers = @{ Authorization = "Bearer $token" }
    $body = @{ newTitle = "Renamed $i" } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/rename" -Headers $headers -ContentType 'application/json' -Body $body -ErrorAction SilentlyContinue
        if ($r) { $r.title } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$finalCard2 = Api GET "/api/cards/$cardId" $aliceToken
Write-Host "  Final title: $($finalCard2.title)"
$ok2 = $finalCard2.title -match '^Renamed \d+$' -and ($jobs2 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Result: $(if ($ok2) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 3: concurrent voting (toggle) from same user ---
Write-Host "=== Test 3: concurrent voting (20 toggles from same user) ==="
$jobs3 = 1..20 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $token = $using:aliceToken
    $api = $using:api
    $headers = @{ Authorization = "Bearer $token" }
    try {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/votes" -Headers $headers -ErrorAction SilentlyContinue
        if ($r) { $r.votedByMe } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$voteState = Api GET "/api/cards/$cardId/votes" $aliceToken
Write-Host "  Final state: votedByMe=$($voteState.votedByMe), count=$($voteState.count)"
$ok3 = ($jobs3 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Result: $(if ($ok3) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 4: concurrent checklist item toggle ---
Write-Host "=== Test 4: concurrent checklist item toggle ==="
$cl = Api POST "/api/cards/$cardId/checklists" $aliceToken @{ title = "R3 Checklist" }
$clId = $cl.id
$item = Api POST "/api/checklists/$clId/items/" $aliceToken @{ text = "Item 1" }
$itemId = $item.id
$jobs4 = 1..10 | ForEach-Object -Parallel {
    $clId = $using:clId
    $itemId = $using:itemId
    $token = $using:aliceToken
    $api = $using:api
    $headers = @{ Authorization = "Bearer $token" }
    try {
        $r = Invoke-RestMethod -Method PATCH -Uri "$api/api/checklists/$clId/items/$itemId/toggle" -Headers $headers -ErrorAction SilentlyContinue
        if ($r) { $r.items[0].isChecked } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$finalCl = Api GET "/api/cards/$cardId/checklists" $aliceToken
$ok4 = ($jobs4 | Where-Object { $_ -eq '500' }).Count -eq 0 -and $finalCl[0].items[0].isChecked -is [bool]
Write-Host "  Final isChecked: $($finalCl[0].items[0].isChecked)"
Write-Host "  Result: $(if ($ok4) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 5: concurrent comment add + delete ---
Write-Host "=== Test 5: concurrent comment add (20) + delete (10) ==="
$jobs5add = 1..20 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $headers = @{ Authorization = "Bearer $token" }
    $body = @{ body = "Comment $i" } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/comments" -Headers $headers -ContentType 'application/json' -Body $body -ErrorAction SilentlyContinue
        if ($r) { $r.id } else { 'error' }
    } catch {
        'add-fail'
    }
}
$commentIds = $jobs5add | Where-Object { $_ -is [string] -and $_ -ne 'error' -and $_ -ne 'add-fail' }
$toDelete = $commentIds | Select-Object -First 10
$jobs5del = $toDelete | ForEach-Object -Parallel {
    $commentId = $_
    $token = $using:aliceToken
    $api = $using:api
    $headers = @{ Authorization = "Bearer $token" }
    try {
        $r = Invoke-RestMethod -Method DELETE -Uri "$api/api/comments/$commentId" -Headers $headers -ErrorAction SilentlyContinue
        if ($r) { 'ok' } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$finalComments = Api GET "/api/cards/$cardId/comments" $aliceToken
Write-Host "  Created: 20, Deleted: 10, Final count: $($finalComments.Count) (expected 10)"
$ok5 = $finalComments.Count -eq 10
Write-Host "  Result: $(if ($ok5) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 6: concurrent board star/unstar ---
Write-Host "=== Test 6: concurrent board star/unstar (50 alternations) ==="
$jobs6 = 1..50 | ForEach-Object -Parallel {
    $boardId = $using:boardId
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $headers = @{ Authorization = "Bearer $token" }
    $method = if ($i % 2 -eq 0) { 'POST' } else { 'DELETE' }
    $path = if ($i % 2 -eq 0) { "$api/api/boards/$boardId/star" } else { "$api/api/boards/$boardId/star" }
    try {
        Invoke-RestMethod -Method $method -Uri $path -Headers $headers -ErrorAction SilentlyContinue
        if ($?) { 'ok' } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$starred = Api GET '/api/boards/starred' $aliceToken
$isStarred = $starred | Where-Object { $_.id -eq $boardId }
$ok6 = ($jobs6 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Final starred: $(if ($isStarred) { 'YES' } else { 'NO' })"
Write-Host "  Result: $(if ($ok6) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 7: concurrent label attach/detach ---
Write-Host "=== Test 7: concurrent label attach/detach (50 alternations) ==="
$jobs7 = 1..50 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $labelId = $using:labelId
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $headers = @{ Authorization = "Bearer $token" }
    if ($i % 2 -eq 0) {
        $method = 'POST'
        $path = "$api/api/cards/$cardId/labels/$labelId"
    } else {
        $method = 'DELETE'
        $path = "$api/api/cards/$cardId/labels/$labelId"
    }
    try {
        Invoke-RestMethod -Method $method -Uri $path -Headers $headers -ErrorAction SilentlyContinue
        if ($?) { 'ok' } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$finalCard3 = Api GET "/api/cards/$cardId" $aliceToken
$hasLabel = $finalCard3.labels | Where-Object { $_.id -eq $labelId }
$ok7 = ($jobs7 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Final labels count: $($finalCard3.labels.Count), has Bug label: $(if ($hasLabel) { 'YES' } else { 'NO' })"
Write-Host "  Result: $(if ($ok7) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 8: concurrent complete + reopen ---
Write-Host "=== Test 8: concurrent complete + reopen (30 alternations) ==="
$jobs8 = 1..30 | ForEach-Object -Parallel {
    $cardId = $using:cardId
    $token = $using:aliceToken
    $api = $using:api
    $i = $_
    $headers = @{ Authorization = "Bearer $token" }
    if ($i % 2 -eq 0) {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/complete" -Headers $headers -ErrorAction SilentlyContinue
    } else {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/cards/$cardId/reopen" -Headers $headers -ErrorAction SilentlyContinue
    }
    if ($r) { $r.isCompleted } else { 'error' }
}
$finalCard4 = Api GET "/api/cards/$cardId" $aliceToken
$ok8 = ($jobs8 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Final isCompleted: $($finalCard4.isCompleted)"
Write-Host "  Result: $(if ($ok8) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 9: two users concurrent assign ---
Write-Host "=== Test 9: two users concurrent assign same card ==="
$jobs9 = @(
    1..5 | ForEach-Object -Parallel { Invoke-RestMethod -Method POST -Uri "$using:api/api/cards/$using:cardId/assign/$using:aliceId" -Headers @{Authorization="Bearer $using:aliceToken"} -ErrorAction SilentlyContinue }
    1..5 | ForEach-Object -Parallel { Invoke-RestMethod -Method POST -Uri "$using:api/api/cards/$using:cardId/assign/$using:bobId" -Headers @{Authorization="Bearer $using:bobToken"} -ErrorAction SilentlyContinue }
)
$finalCard5 = Api GET "/api/cards/$cardId" $aliceToken
Write-Host "  Final assignments: $($finalCard5.assignments -join ', ')"
$ok9 = ($jobs9 | Where-Object { $_ -eq '500' }).Count -eq 0
Write-Host "  Result: $(if ($ok9) { 'PASS' } else { 'FAIL' })"
Write-Host ""

# --- Test 10: 20 parallel logins same credentials ---
Write-Host "=== Test 10: 20 parallel /api/auth/login (same creds) ==="
$jobs10 = 1..20 | ForEach-Object -Parallel {
    $email = $using:aliceEmail
    $pass = $using:pass
    $api = $using:api
    $body = @{ email = $email; password = $pass } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Method POST -Uri "$api/api/auth/login" -ContentType 'application/json' -Body $body -ErrorAction SilentlyContinue
        if ($r) { $r.user.id } else { 'error' }
    } catch {
        $_.Exception.Response.StatusCode.value__
    }
}
$uniqueUserIds = $jobs10 | Where-Object { $_ -is [string] -and $_ -ne 'error' } | Sort-Object -Unique
$ok10 = $uniqueUserIds.Count -eq 1 -and $uniqueUserIds[0] -eq $aliceId
Write-Host "  Unique user IDs: $($uniqueUserIds.Count) (expected 1)"
Write-Host "  Result: $(if ($ok10) { 'PASS' } else { 'FAIL' })"
Write-Host ""

Write-Host "=== Summary ==="
$summary = [ordered]@{
    Test1_CardMoves = $ok1
    Test2_CardRename = $ok2
    Test3_Voting = $ok3
    Test4_ChecklistToggle = $ok4
    Test5_Comments = $ok5
    Test6_BoardStar = $ok6
    Test7_LabelAttach = $ok7
    Test8_CompleteReopen = $ok8
    Test9_TwoUserAssign = $ok9
    Test10_ParallelLogins = $ok10
}
$passCount = ($summary.Values | Where-Object { $_ }).Count
Write-Host "  Passed: $passCount / 10"
$summary | ConvertTo-Json | Out-File -FilePath 'D:/GitHub/Cardscape/test-results/api/beta-test-r3-concurrency-summary.json' -Encoding UTF8
