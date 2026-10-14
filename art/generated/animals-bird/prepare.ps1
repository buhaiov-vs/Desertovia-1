Add-Type -AssemblyName System.Drawing
$samples = (Get-Content (Join-Path $PSScriptRoot 'prompts.json') -Raw | ConvertFrom-Json).samples | ForEach-Object { ,@('', $_.name, $_.width, $_.height) }
foreach ($sample in $samples) {
    $name = $sample[1]
    if (-not (Test-Path (Join-Path $PSScriptRoot "$name-source.png")) -or (Test-Path (Join-Path $PSScriptRoot "$name.png"))) { continue }
    $canvasWidth = [int]$sample[2]; $canvasHeight = [int]$sample[3]
    $source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot "$name-source.png"))
    $left = [int]($source.Width / 4); $top = [int]($source.Height / 4)
    $width = [int]($source.Width / 2); $height = [int]($source.Height / 2)
    $targetWidth = $canvasWidth; $targetHeight = $canvasHeight
    $offsetX = 0; $offsetY = 0
    $target = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight)
    for ($y = 0; $y -lt $targetHeight; $y++) {
        for ($x = 0; $x -lt $targetWidth; $x++) {
            $sx = $left + [int][Math]::Floor(($x + 0.5) * $width / $targetWidth)
            $sy = $top + [int][Math]::Floor(($y + 0.5) * $height / $targetHeight)
            $target.SetPixel($offsetX + $x, $offsetY + $y, $source.GetPixel($sx, $sy))
        }
    }
    $target.Save((Join-Path $PSScriptRoot "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $preview = [System.Drawing.Bitmap]::new(($canvasWidth * 8), ($canvasHeight * 8))
    for ($y = 0; $y -lt $preview.Height; $y++) {
        for ($x = 0; $x -lt $preview.Width; $x++) {
            $preview.SetPixel($x, $y, $target.GetPixel([int][Math]::Floor($x / 8), [int][Math]::Floor($y / 8)))
        }
    }
    $preview.Save((Join-Path $PSScriptRoot "$name-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "$name : $canvasWidth x $canvasHeight; corner alpha $($target.GetPixel(0,0).A)"
    $source.Dispose(); $target.Dispose(); $preview.Dispose()
}




