param(
    [string]$Filename
)

$token = (Get-Content "D:\GitHub\Cardscape\test-results\beta\round-2\a5-token.txt" -Raw).Trim()
$cardId = (Get-Content "D:\GitHub\Cardscape\test-results\beta\round-2\a5-card.txt" -Raw).Trim()
$tempFile = "D:\GitHub\Cardscape\test-results\beta\round-2\a5-test-traversal.txt"
if (-not (Test-Path $tempFile)) {
    $bytes = New-Object byte[] 100
    (New-Object Random).NextBytes($bytes)
    [System.IO.File]::WriteAllBytes($tempFile, $bytes)
}

curl.exe -sS -X POST "http://localhost:8080/api/cards/$cardId/attachments/" -H "Authorization: Bearer $token" -F "file=@$tempFile;type=text/plain;filename=$Filename" -w "\nSTATUS: %{http_code}\n"
