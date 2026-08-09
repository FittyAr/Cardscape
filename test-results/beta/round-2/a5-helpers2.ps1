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

$ErrorActionPreference = "Continue"
$token = (Get-Content "D:\GitHub\Cardscape\test-results\beta\round-2\a5-token.txt" -Raw).Trim()

$headers = @{
    Authorization = "Bearer $token"
}

try {
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
} catch {
    $ex = $_.Exception
    if ($ex.Response) {
        $statusCode = [int]$ex.Response.StatusCode
        $reader = New-Object System.IO.StreamReader($ex.Response.GetResponseStream())
        $body = $reader.ReadToEnd()
        Write-Host "STATUS: $statusCode"
        Write-Host "CONTENT: $body"
    } else {
        Write-Host "STATUS: ERR"
        Write-Host "CONTENT: $_"
    }
}

if ($OutFile -ne "") {
    "$status`n$content" | Out-File -Encoding utf8 $OutFile
}
