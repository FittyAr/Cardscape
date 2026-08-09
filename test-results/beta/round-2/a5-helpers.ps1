param(
    [string]$Email = "",
    [string]$Password = "P4ssw0rd!",
    [string]$Method = "GET",
    [string]$Path = "",
    [string]$BodyJson = "",
    [string]$ContentType = "application/json",
    [string]$OutFile = "",
    [string]$MultiPartFile = ""
)

$ErrorActionPreference = "Stop"
$token = (Get-Content "D:\GitHub\Cardscape\test-results\beta\round-2\a5-token.txt" -Raw).Trim()

$headers = @{
    Authorization = "Bearer $token"
}

if ($MultiPartFile -ne "") {
    $full = Resolve-Path $MultiPartFile
    $form = @{
        file = Get-Item -Path $full
    }
    $response = Invoke-WebRequest -Uri "http://localhost:8080$Path" -Method $Method -Form $form -Headers $headers -UseBasicParsing
} elseif ($BodyJson -ne "") {
    $response = Invoke-WebRequest -Uri "http://localhost:8080$Path" -Method $Method -Body $BodyJson -ContentType $ContentType -Headers $headers -UseBasicParsing
} else {
    $response = Invoke-WebRequest -Uri "http://localhost:8080$Path" -Method $Method -Headers $headers -UseBasicParsing
}

$status = $response.StatusCode
$content = $response.Content
Write-Host "STATUS: $status"
Write-Host "CONTENT: $content"

if ($OutFile -ne "") {
    "$status`n$content" | Out-File -Encoding utf8 $OutFile
}
