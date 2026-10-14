$ErrorActionPreference = 'Stop'
$catalogPath = Join-Path $PSScriptRoot 'catalog.json'
$old = @(Get-Content $catalogPath -Raw | ConvertFrom-Json)
$snapshot = Join-Path $PSScriptRoot 'catalog-before-reconciliation-2026-10-14.json'
if (-not (Test-Path $snapshot)) { Copy-Item -LiteralPath $catalogPath -Destination $snapshot }
$document = Join-Path $PSScriptRoot '../../../../docs/sprites.md'
$rows = @(); $added = @(); $renamed = @(); $changed = @(); $section = ''
foreach ($line in Get-Content $document) {
    if ($line -match '^## (\d+)\. (.+)') { $section = $Matches[1] + '-' + $Matches[2] }
    if ($line -cnotmatch '^\| (item|building|icon|character|prop|terrain|effect) \|') { continue }
    if (-not $section) { continue }
    $parts = $line.Split('|')
    if ($parts.Count -lt 7) { throw "Incomplete sprite row: $line" }
    $kind = $parts[1].Trim(); $name = $parts[2].Trim() -replace '^\*\*New\*\*\s*',''
    $lookup = $name
    if ($section -match '^9-' -and $name -eq 'Tsar') { $lookup = 'Governor' }
    if ($section -match '^19-' -and $name -eq 'Tsar badge') { $lookup = 'Governor badge' }
    $sectionNumber = ($section -split '-',2)[0]
    $prior = @($old | Where-Object { ($_.section -split '-',2)[0] -eq $sectionNumber -and $_.kind -eq $kind -and $_.name -eq $name })
    if ($prior.Count -eq 0 -and $lookup -ne $name) { $prior = @($old | Where-Object { ($_.section -split '-',2)[0] -eq $sectionNumber -and $_.kind -eq $kind -and $_.name -eq $lookup }) }
    if ($prior.Count -gt 1) { throw "Duplicate catalog row: $section / $name" }
    if ($prior.Count -eq 1) {
        $row = $prior[0]
        if ($row.name -ne $name) { $renamed += "$($row.name) -> $name" }
        if ($row.prompt -ne $parts[5].Trim()) { $changed += "$section / $name" }
        $row.name = $name; $row.section = $section; $row.description = $parts[3].Trim(); $row.prompt = $parts[5].Trim()
    } else {
        $row = [pscustomobject]@{section=$section;kind=$kind;name=$name;description=$parts[3].Trim();prompt=$parts[5].Trim();status='pending'}
        $added += "$section / $name"
    }
    $rows += $row
}
$rows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $catalogPath
$report = [pscustomobject]@{document='docs/sprites.md';previousRows=$old.Count;currentRows=$rows.Count;added=$added;renamed=$renamed;changedPrompts=$changed;note='Row coverage only; variants, animations and final visual approval still require separate audits. Player skins and animations remain deferred.'}
$report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'catalog-reconciliation-2026-10-14.json')
Write-Output "Rows: $($old.Count) -> $($rows.Count); added $($added.Count); renamed $($renamed.Count); changed prompts $($changed.Count)"
