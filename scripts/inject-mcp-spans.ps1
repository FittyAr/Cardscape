# Inserts `using var span = McpToolSpan.Begin("...");` at the
# start of every [McpServerTool] method in the MCP tools
# directory. Idempotent: skips methods that already have the
# span line. Also ensures the file has the using for
# Cardscape.Mcp.Observability. Run from repo root.

$ErrorActionPreference = 'Stop'

$toolsDir = Join-Path $PSScriptRoot '..\src\Cardscape.Mcp\Tools'
$files = Get-ChildItem -Path $toolsDir -Filter '*Tools.cs' -File
if ($files.Count -eq 0) {
    Write-Error "No *Tools.cs files found in $toolsDir"
}

$totalInserted = 0
$totalSkipped = 0
$totalUsingAdded = 0
foreach ($file in $files) {
    $original = Get-Content -Path $file.FullName -Raw
    $lines = Get-Content -Path $file.FullName
    $out = New-Object System.Collections.Generic.List[string]
    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]
        $out.Add($line)
        if ($line -match '\[McpServerTool\(Name\s*=\s*"([^"]+)"\)\]') {
            $toolName = $matches[1]
            $j = $i + 1
            while ($j -lt $lines.Count -and $lines[$j] -notmatch '^\s*\{') {
                $out.Add($lines[$j])
                $j++
            }
            if ($j -ge $lines.Count) {
                Write-Warning "No opening brace found for $toolName in $($file.Name)"
                $i = $j
                continue
            }
            $braceLine = $lines[$j]
            $out.Add($braceLine)
            $nextLine = if ($j + 1 -lt $lines.Count) { $lines[$j + 1] } else { '' }
            if ($nextLine -match 'McpToolSpan\.Begin\("' + [regex]::Escape($toolName) + '"\)') {
                $totalSkipped++
            } else {
                $out.Add("        using var __mcpSpan = McpToolSpan.Begin(`"$toolName`");")
                $totalInserted++
            }
            $i = $j + 1
            continue
        }
        $i++
    }
    if ($out.Count -ne $lines.Count) {
        $newText = ($out -join "`r`n")
        if ($newText -notmatch 'using Cardscape\.Mcp\.Observability;') {
            $newText = $newText -replace '(using Cardscape\.Mcp\.Tools;\r?\n)', "`$1using Cardscape.Mcp.Observability;`r`n"
            if ($newText -notmatch 'using Cardscape\.Mcp\.Observability;') {
                $newText = $newText -replace '(using ModelContextProtocol\.Server;\r?\n)', "`$1using Cardscape.Mcp.Observability;`r`n"
            }
            if ($newText -notmatch 'using Cardscape\.Mcp\.Observability;') {
                $newText = $newText -replace '(using Wolverine;\r?\n)', "`$1using Cardscape.Mcp.Observability;`r`n"
            }
            if ($newText -notmatch 'using Cardscape\.Mcp\.Observability;') {
                $newText = "using Cardscape.Mcp.Observability;`r`n" + $newText
            }
            $totalUsingAdded++
        }
        Set-Content -Path $file.FullName -Value $newText -Encoding UTF8
        Write-Host "Updated $($file.Name)"
    }
}
Write-Host "Inserted: $totalInserted, Skipped: $totalSkipped, Using added: $totalUsingAdded"
