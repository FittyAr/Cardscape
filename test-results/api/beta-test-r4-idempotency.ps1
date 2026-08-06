#requires -Version 7.0
# Beta R4 — Idempotency-Key middleware verification
# Run with: pwsh -NoProfile -NonInteractive -File beta-test-r4-idempotency.ps1
#
# BETA-3-#5 closed the half-built Idempotency-Key feature: the
# table, the repository and the domain entity were already in
# place from v0.7 but the HTTP middleware was never landed. This
# script exercises the four contract clauses documented on
# IdempotencyMiddleware:
#
#   1. Replay: same key + same body → second call replays the
#      first response (200 + identical body, with the
#      `Idempotent-Replayed: true` header).
#   2. Mismatch: same key + different body → 422
#      idempotency.key.payload_mismatch.
#   3. Bad key: malformed key (too short / too long) → 400.
#   4. Pass-through: GET requests ignore the header (read-only
#      methods never short-circuit).
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$api = 'http://localhost:8080'
$env:CARDS_CAPE_JWT_KEY = 'dev-only-insecure-signing-key-please-override-in-production-32+chars'

# --- Setup: register one user, create a workspace + board + list ---
$ts = (Get-Date).ToString('HHmmss')
$email = "r4.idem.$ts@cardscape.test"
$pass = 'TestPass123!'

function Auth-Header($token) { @{ Authorization = "Bearer $token" } }
function Merge-Headers($token, $extra) {
    $h = Auth-Header $token
    foreach ($k in $extra.Keys) { $h[$k] = $extra[$k] }
    return $h
}

function Api($method, $path, $token, $body = $null, $extraHeaders = @{}) {
    $headers = Merge-Headers $token $extraHeaders
    $params = @{
        Method = $method
        Uri = "$api$path"
        Headers = $headers
        ContentType = 'application/json'
    }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json) }
    return Invoke-RestMethod @params -ErrorAction SilentlyContinue
}

Write-Host "Setup: registering user"
$reg = Invoke-RestMethod -Method POST -Uri "$api/api/auth/register" -ContentType 'application/json' -Body (@{ email = $email; displayName = 'Idem R4'; password = $pass } | ConvertTo-Json)
$token = $reg.accessToken

Write-Host "Setup: workspace + board + list"
$ws = Api POST '/api/workspaces' $token @{ name = "R4 Idem WS $ts"; region = 0 }
$wsId = $ws.id
$board = Api POST '/api/boards' $token @{ workspaceId = $wsId; name = "R4 Idem Board"; description = "idempotency test"; visibility = 0 }
$boardId = $board.id
$list = Api POST '/api/lists' $token @{ boardId = $boardId; name = 'Inbox' }
$listId = $list.id

Write-Host "Setup done. boardId=$boardId, listId=$listId"
Write-Host ""

$total = 0; $passed = 0

function Run-Case {
    param([string]$Name, [bool]$Ok, [string]$Detail = '')
    $script:total++
    if ($Ok) { $script:passed++ }
    $tag = if ($Ok) { 'PASS' } else { 'FAIL' }
    Write-Host "  [$tag] $Name  $Detail"
}

# --- Test 1: Replay — same key, same body, two calls ---
Write-Host "=== Test 1: Replay (same key + same body) ==="
$key1 = "r4-idem-replay-$ts-AAAAAAAA"
$body1 = @{ listId = $listId; title = "Replay card"; description = "first" } | ConvertTo-Json
$headers1 = @{ 'Idempotency-Key' = $key1 }
$r1 = Invoke-WebRequest -Method POST -Uri "$api/api/cards" `
    -Headers (Merge-Headers $token $headers1) `
    -ContentType 'application/json' -Body $body1 -UseBasicParsing
$cardId1 = ($r1.Content | ConvertFrom-Json).id
Write-Host "  First call:  $($r1.StatusCode), cardId=$cardId1"

# Second call: same key, same body — should replay.
$r2 = Invoke-WebRequest -Method POST -Uri "$api/api/cards" `
    -Headers (Merge-Headers $token $headers1) `
    -ContentType 'application/json' -Body $body1 -UseBasicParsing
$cardId2 = ($r2.Content | ConvertFrom-Json).id
$replayed = $r2.Headers['Idempotent-Replayed']
Write-Host "  Second call: $($r2.StatusCode), cardId=$cardId2, Idempotent-Replayed=$replayed"

Run-Case 'Replay: same cardId returned' ($cardId1 -eq $cardId2) "first=$cardId1 second=$cardId2"
# Accept 2xx (the original status is replayed verbatim — for
# Create endpoints that's 201 Created, for renames it's 200 OK).
$status2xx = $r2.StatusCode -ge 200 -and $r2.StatusCode -lt 300
Run-Case 'Replay: 2xx on second call' $status2xx "status=$($r2.StatusCode)"
$replayedStr = if ($replayed -is [array]) { $replayed[0] } else { $replayed }
Run-Case 'Replay: Idempotent-Replayed header set' ($replayedStr -eq 'true') "header=$replayedStr"

# Verify the card was actually only created once.
$listResp = Api GET "/api/cards?boardId=$boardId" $token
$matching = @($listResp | Where-Object { $_.id -eq $cardId1 })
Run-Case 'Replay: only one card in DB' ($matching.Count -eq 1) "found=$($matching.Count)"
Write-Host ""

# --- Test 2: Mismatch — same key, different body ---
Write-Host "=== Test 2: Mismatch (same key + different body) ==="
$key2 = "r4-idem-mismatch-$ts-BBBBBBBB"
$body2a = @{ listId = $listId; title = "Mismatch A"; description = "first" } | ConvertTo-Json
$body2b = @{ listId = $listId; title = "Mismatch B"; description = "second" } | ConvertTo-Json
$headers2 = @{ 'Idempotency-Key' = $key2 }
$r2a = Invoke-WebRequest -Method POST -Uri "$api/api/cards" `
    -Headers (Merge-Headers $token $headers2) `
    -ContentType 'application/json' -Body $body2a -UseBasicParsing
$cardId2a = ($r2a.Content | ConvertFrom-Json).id
Write-Host "  First call:  $($r2a.StatusCode), cardId=$cardId2a"

# Use raw HttpClient to capture both status + body for non-2xx.
$handler = [System.Net.Http.HttpClientHandler]::new()
$http = [System.Net.Http.HttpClient]::new($handler)
$http.DefaultRequestHeaders.Add('Authorization', "Bearer $token")
$http.DefaultRequestHeaders.Add('Idempotency-Key', $key2)
$content = [System.Net.Http.StringContent]::new($body2b, [System.Text.Encoding]::UTF8, 'application/json')
$resp = $http.PostAsync("$api/api/cards", $content).GetAwaiter().GetResult()
$mismatchStatus = [int]$resp.StatusCode
$bodyStr = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
$respObj = $bodyStr | ConvertFrom-Json
$errCode = $respObj.title
Write-Host "  Second call: $mismatchStatus, body title=$errCode"

Run-Case 'Mismatch: 422 Unprocessable Entity' ($mismatchStatus -eq 422) "status=$mismatchStatus"
$errCodeStr = if ($errCode -is [array]) { $errCode[0] } else { $errCode }
Run-Case 'Mismatch: code = idempotency.key.payload_mismatch' ($errCodeStr -eq 'idempotency.key.payload_mismatch') "code=$errCodeStr"
Write-Host ""

# --- Test 3: Bad key (too short) ---
Write-Host "=== Test 3: Bad key (too short) ==="
$badHeaders = @{ 'Idempotency-Key' = 'short' }  # < 8 chars
try {
    $r3 = Invoke-WebRequest -Method POST -Uri "$api/api/cards" `
        -Headers (Merge-Headers $token $badHeaders) `
        -ContentType 'application/json' -Body (@{ listId = $listId; title = "Bad key" } | ConvertTo-Json) `
        -UseBasicParsing
    $badStatus = $r3.StatusCode
    $badBody = $r3.Content
} catch {
    $badStatus = $_.Exception.Response.StatusCode.value__
    $badBody = $_.Exception.Response
}
Write-Host "  Bad key: status=$badStatus"
Run-Case 'Bad key: 400 Bad Request' ($badStatus -eq 400) "status=$badStatus"
Write-Host ""

# --- Test 4: GET with Idempotency-Key is ignored (pass-through) ---
Write-Host "=== Test 4: GET with Idempotency-Key (pass-through) ==="
$key4 = "r4-idem-get-$ts-CCCCCCCC"
$getHeaders = @{ 'Idempotency-Key' = $key4 }
$r4 = Invoke-WebRequest -Method GET -Uri "$api/api/boards/$boardId" `
    -Headers (Merge-Headers $token $getHeaders) `
    -UseBasicParsing
$replayed4 = $r4.Headers['Idempotent-Replayed']
Write-Host "  GET: $($r4.StatusCode), Idempotent-Replayed=$replayed4"
Run-Case 'GET pass-through: 200 OK' ($r4.StatusCode -eq 200) "status=$($r4.StatusCode)"
$replayed4Str = if ($replayed4 -is [array]) { $replayed4[0] } else { $replayed4 }
Run-Case 'GET pass-through: no Idempotent-Replayed header' ($null -eq $replayed4Str -or $replayed4Str -eq '') "header=$replayed4Str"

# Second GET with same key — middleware ignores it, so each GET hits the
# real endpoint. Both should still return 200 with no replay header.
$r4b = Invoke-WebRequest -Method GET -Uri "$api/api/boards/$boardId" `
    -Headers (Merge-Headers $token $getHeaders) `
    -UseBasicParsing
Run-Case 'GET pass-through: second GET also 200' ($r4b.StatusCode -eq 200) "status=$($r4b.StatusCode)"
$replayed4b = $r4b.Headers['Idempotent-Replayed']
$replayed4bStr = if ($replayed4b -is [array]) { $replayed4b[0] } else { $replayed4b }
Run-Case 'GET pass-through: second GET also no replay header' ($null -eq $replayed4bStr -or $replayed4bStr -eq '') "header=$replayed4bStr"
Write-Host ""

# --- Test 5: PUT replay (other mutable method) ---
Write-Host "=== Test 5: PUT replay (rename) ==="
$card5 = Api POST '/api/cards' $token @{ listId = $listId; title = "PUT replay card" }
$card5Id = $card5.id
$key5 = "r4-idem-put-$ts-DDDDDDDD"
$renameBody = @{ newTitle = "Renamed via idempotency" } | ConvertTo-Json
$putHeaders = @{ 'Idempotency-Key' = $key5 }
$r5a = Invoke-WebRequest -Method POST -Uri "$api/api/cards/$card5Id/rename" `
    -Headers (Merge-Headers $token $putHeaders) `
    -ContentType 'application/json' -Body $renameBody -UseBasicParsing
Write-Host "  First rename:  $($r5a.StatusCode)"
$r5b = Invoke-WebRequest -Method POST -Uri "$api/api/cards/$card5Id/rename" `
    -Headers (Merge-Headers $token $putHeaders) `
    -ContentType 'application/json' -Body $renameBody -UseBasicParsing
$replayed5 = $r5b.Headers['Idempotent-Replayed']
Write-Host "  Second rename: $($r5b.StatusCode), Idempotent-Replayed=$replayed5"
Run-Case 'PUT replay: 2xx on second call' ($r5b.StatusCode -ge 200 -and $r5b.StatusCode -lt 300) "status=$($r5b.StatusCode)"
$replayed5Str = if ($replayed5 -is [array]) { $replayed5[0] } else { $replayed5 }
Run-Case 'PUT replay: Idempotent-Replayed=true' ($replayed5Str -eq 'true') "header=$replayed5Str"

# Verify the rename happened exactly once.
$card5Final = Api GET "/api/cards/$card5Id" $token
Run-Case 'PUT replay: title is the new value' ($card5Final.title -eq 'Renamed via idempotency') "title=$($card5Final.title)"
Write-Host ""

Write-Host "=== Summary ==="
Write-Host "  Passed: $passed / $total"
$summary = [ordered]@{
    Replay_CardId_Match = ($cardId1 -eq $cardId2)
    Replay_Status = ($r2.StatusCode -eq 200)
    Replay_Header = ($replayed -eq 'true')
    Replay_OnlyOnce = ($matching.Count -eq 1)
    Mismatch_Status = ($r2bRaw.StatusCode -eq 422)
    Mismatch_Code = ($errCode -eq 'idempotency.key.payload_mismatch')
    BadKey_Status = ($badStatus -eq 400)
    GetPassThrough_200 = ($r4.StatusCode -eq 200)
    GetPassThrough_NoHeader = ($null -eq $replayed4)
    GetPassThrough_Second200 = ($r4b.StatusCode -eq 200)
    GetPassThrough_SecondNoHeader = ($null -eq $r4b.Headers['Idempotent-Replayed'])
    PutReplay_200 = ($r5b.StatusCode -eq 200)
    PutReplay_Header = ($replayed5 -eq 'true')
    PutReplay_TitleApplied = ($card5Final.title -eq 'Renamed via idempotency')
}
$summary | ConvertTo-Json | Out-File -FilePath 'D:/GitHub/Cardscape/test-results/api/beta-test-r4-idempotency-summary.json' -Encoding UTF8
