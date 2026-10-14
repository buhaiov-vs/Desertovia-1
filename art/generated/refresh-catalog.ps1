Add-Type -AssemblyName System.Drawing
$catalog = Get-Content (Join-Path $PSScriptRoot 'catalog.json') -Raw | ConvertFrom-Json
$files = Get-ChildItem $PSScriptRoot -Recurse -Filter *.png | Where-Object { $_.Name -notmatch '-(source|preview)\.png$' }
foreach ($row in $catalog) {
    if ($row.prompt -match '^(Keep code-drawn|Do not generate|No new sprite)') { $row.status = 'retained-as-directed'; continue }
    $filename = $null
    if ($row.section -match '^10-' -and $row.name -match '`([^`]+)`') { $filename = 'icon-task-' + $Matches[1] + '.png' }
    if ($row.section -match '^3-') { $filename = 'icon-building-' + ($row.name.ToLower() -replace '[^a-z0-9]+','-').Trim('-') + '.png' }
    if ($row.section -match '^(1|2|5|6|7|8|9)-') { $filename = $row.kind + '-' + ($row.name.ToLower() -replace '[^a-z0-9]+','-').Trim('-') + '.png' }
    if ($row.section -match '^9-' -and $row.name -match '^Citizen, (male|female) style (\d+)$') { $filename = 'character-citizen-' + $Matches[1] + '-' + $Matches[2] + '.png' }
    $file = $files | Where-Object Name -eq $filename | Select-Object -First 1
    if ($file) {
        $bitmap = [System.Drawing.Bitmap]::new($file.FullName)
        $row.status = 'generated-awaiting-final-style-audit'
        $row | Add-Member -Force NoteProperty file ([System.IO.Path]::GetRelativePath($PSScriptRoot, $file.FullName).Replace('\','/'))
        $row | Add-Member -Force NoteProperty width $bitmap.Width
        $row | Add-Member -Force NoteProperty height $bitmap.Height
        $bitmap.Dispose()
    }
}
$catalog | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'catalog.json')
$construction = Get-Content (Join-Path $PSScriptRoot 'construction-catalog.json') -Raw | ConvertFrom-Json
foreach ($stage in $construction) {
    $stageFile = $files | Where-Object Name -eq ($stage.name + '.png') | Select-Object -First 1
    if ($stageFile) {
        $bitmap = [System.Drawing.Bitmap]::new($stageFile.FullName)
        $stage.status = 'generated-awaiting-final-style-and-alignment-audit'
        $stage | Add-Member -Force NoteProperty file ([System.IO.Path]::GetRelativePath($PSScriptRoot, $stageFile.FullName).Replace('\','/'))
        $stage | Add-Member -Force NoteProperty width $bitmap.Width
        $stage | Add-Member -Force NoteProperty height $bitmap.Height
        $bitmap.Dispose()
    }
}
$construction | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'construction-catalog.json')
$pond = Get-Content (Join-Path $PSScriptRoot 'pond-catalog.json') -Raw | ConvertFrom-Json
foreach ($tile in $pond) {
    $tileFile = $files | Where-Object Name -eq ($tile.name + '.png') | Select-Object -First 1
    if ($tileFile) {
        $bitmap = [System.Drawing.Bitmap]::new($tileFile.FullName)
        $tile.status = 'generated-awaiting-final-style-and-seam-audit'
        $tile | Add-Member -Force NoteProperty file ([System.IO.Path]::GetRelativePath($PSScriptRoot, $tileFile.FullName).Replace('\','/'))
        $tile | Add-Member -Force NoteProperty width $bitmap.Width
        $tile | Add-Member -Force NoteProperty height $bitmap.Height
        $bitmap.Dispose()
    }
}
$pond | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'pond-catalog.json')
$sceneryVariants = Get-Content (Join-Path $PSScriptRoot 'scenery-variants-catalog.json') -Raw | ConvertFrom-Json
foreach ($variant in $sceneryVariants) {
    $variantFile = $files | Where-Object Name -eq ($variant.name + '.png') | Select-Object -First 1
    if ($variantFile) {
        $bitmap = [System.Drawing.Bitmap]::new($variantFile.FullName)
        $variant.status = 'generated-awaiting-final-style-and-alignment-audit'
        $variant | Add-Member -Force NoteProperty file ([System.IO.Path]::GetRelativePath($PSScriptRoot, $variantFile.FullName).Replace('\','/'))
        $variant | Add-Member -Force NoteProperty width $bitmap.Width
        $variant | Add-Member -Force NoteProperty height $bitmap.Height
        $bitmap.Dispose()
    }
}
$sceneryVariants | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $PSScriptRoot 'scenery-variants-catalog.json')
$cards = foreach ($row in (@($catalog) + @($construction) + @($pond) + @($sceneryVariants)) | Where-Object file) {
    $name = [System.Net.WebUtility]::HtmlEncode($row.name)
    "<figure><a href='$($row.file)'><img src='$($row.file)' alt='$name'></a><figcaption>$name ($($row.width)×$($row.height))</figcaption></figure>"
}
$html = "<!doctype html><html lang='en'><meta charset='utf-8'><title>Desertod sprite catalog</title><style>body{font:16px system-ui;background:#252b26;color:#f8edcf;margin:36px}main{display:flex;flex-wrap:wrap;gap:18px}figure{margin:0;padding:16px;background:#3b493c;border-radius:8px}img{width:128px;height:128px;object-fit:contain;image-rendering:pixelated}figcaption{margin-top:12px}a{color:#edc682}</style><h1>Desertod sprites</h1><p>Generated assets — full catalog remains in progress. Small exports enlarged below. Originals and prompts are saved in each batch folder.</p><main>$($cards -join '')</main><p>Built-in image generation using Tiny Town and Tiny Farm references. Assets await final style audit and game integration.</p></html>"
Set-Content (Join-Path $PSScriptRoot 'index.html') $html
$catalog | Group-Object status | Select-Object Count,Name
