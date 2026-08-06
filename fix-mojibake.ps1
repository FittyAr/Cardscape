$ErrorActionPreference = 'Stop'
$utf8 = New-Object System.Text.UTF8Encoding $false

$files = @(
    'src/Cardscape.Web/Pages/CardDetail.razor'
)

$middleDot = [char]0xB7           # ·
$emDash = [char]0x2014            # —
$hellip = [char]0x2026            # …

# Mojibake forms (UTF-8 bytes mis-decoded as Latin-1)
$badMiddleDot = [char]0xC2 + [char]0xB7
$badEmDash = [char]0xE2 + [char]0x80 + [char]0x94
$badHellip = [char]0xE2 + [char]0x80 + [char]0xA6

foreach ($f in $files) {
    $raw = [System.IO.File]::ReadAllText((Resolve-Path $f))
    $count1 = ([regex]::Matches($raw, [regex]::Escape($badMiddleDot))).Count
    $count2 = ([regex]::Matches($raw, [regex]::Escape($badEmDash))).Count
    $count3 = ([regex]::Matches($raw, [regex]::Escape($badHellip))).Count
    $fixed = $raw.Replace($badMiddleDot, $middleDot).Replace($badEmDash, $emDash).Replace($badHellip, $hellip)
    [System.IO.File]::WriteAllText($f, $fixed, $utf8)
    Write-Host "Fixed ${f}: middle-dot=$count1 em-dash=$count2 hellip=$count3"
}
