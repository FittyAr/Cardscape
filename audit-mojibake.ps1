$ErrorActionPreference = 'Stop'
# Comprehensive mojibake audit: any UTF-8 bytes interpreted as Latin-1
# in the source files.
$patterns = @(
    @{ Pattern = ([char]0xC2 + [char]0xA0);   Desc = 'NBSP (U+00A0)' },
    @{ Pattern = ([char]0xC2 + [char]0xB7);   Desc = 'middle-dot (U+00B7)' },
    @{ Pattern = ([char]0xC2 + [char]0xB0);   Desc = 'degree (U+00B0)' },
    @{ Pattern = ([char]0xC2 + [char]0xA9);   Desc = 'copyright (U+00A9)' },
    @{ Pattern = ([char]0xC2 + [char]0xAE);   Desc = 'registered (U+00AE)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x93); Desc = 'en-dash (U+2013)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x94); Desc = 'em-dash (U+2014)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x98); Desc = 'left-single-quote (U+2018)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x99); Desc = 'right-single-quote (U+2019)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x9C); Desc = 'left-double-quote (U+201C)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0x9D); Desc = 'right-double-quote (U+201D)' },
    @{ Pattern = ([char]0xE2 + [char]0x80 + [char]0xA6); Desc = 'ellipsis (U+2026)' }
)
$files = Get-ChildItem -Path src -Recurse -Include *.razor,*.cs -ErrorAction SilentlyContinue
$totalFixes = 0
foreach ($f in $files) {
    if ($f.FullName -like '*\bin\*' -or $f.FullName -like '*\obj\*') { continue }
    $raw = [System.IO.File]::ReadAllText($f.FullName)
    $counts = @{}
    $sum = 0
    foreach ($p in $patterns) {
        $n = ([regex]::Matches($raw, [regex]::Escape($p.Pattern))).Count
        if ($n -gt 0) { $counts[$p.Desc] = $n; $sum += $n }
    }
    if ($sum -gt 0) {
        Write-Host ($f.FullName + ' ' + (($counts.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '))
        $totalFixes += $sum
    }
}
Write-Host ('TOTAL fixes needed: ' + $totalFixes)
