Add-Type -AssemblyName System.Drawing
$sourceDir = 'C:\Users\bugai\.codex\generated_images\01a13753-9e3d-7e70-a8c3-0294a2cb72aa'
$samples = @(
 @('exec-cff1453e-a96b-4931-917f-1d03deca0c36.png', 'item-stone', 16),
 @('exec-6065e224-c008-4e1a-8c5d-8698f6b0d70f.png', 'item-food', 16),
 @('exec-ac770126-b07c-4bbf-a607-cd1105f81494.png', 'item-coal', 16),
 @('exec-39f109fb-d8f9-4725-acba-9c643deeaca4.png', 'item-iron-ore', 16),
 @('exec-25b85097-b225-436b-ac18-4a56b85e72e8.png', 'item-iron-ingot', 16),
 @('exec-7903a411-40dc-4ed3-8dfc-668a4011d870.png', 'item-planks', 16)
)
foreach ($sample in $samples) {
    $name = $sample[1]
    $size = [int]$sample[2]
    Copy-Item -LiteralPath (Join-Path $sourceDir $sample[0]) -Destination (Join-Path $PSScriptRoot "$name-source.png")
    $source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot "$name-source.png"))
    $left = $source.Width; $top = $source.Height; $right = 0; $bottom = 0
    for ($y = 0; $y -lt $source.Height; $y++) {
        for ($x = 0; $x -lt $source.Width; $x++) {
            if ($source.GetPixel($x, $y).A -ge 128) {
                $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
                $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    $width = $right - $left + 1; $height = $bottom - $top + 1
    $scale = ($size - 2) / [Math]::Max($width, $height)
    $targetWidth = [int][Math]::Round($width * $scale)
    $targetHeight = [int][Math]::Round($height * $scale)
    $offsetX = [int][Math]::Floor(($size - $targetWidth) / 2)
    $offsetY = if ($name.StartsWith('character-')) { $size - $targetHeight } else { [int][Math]::Floor(($size - $targetHeight) / 2) }
    $target = [System.Drawing.Bitmap]::new($size, $size)
    for ($y = 0; $y -lt $targetHeight; $y++) {
        for ($x = 0; $x -lt $targetWidth; $x++) {
            $sx = $left + [int][Math]::Floor(($x + 0.5) * $width / $targetWidth)
            $sy = $top + [int][Math]::Floor(($y + 0.5) * $height / $targetHeight)
            $target.SetPixel($offsetX + $x, $offsetY + $y, $source.GetPixel($sx, $sy))
        }
    }
    $target.Save((Join-Path $PSScriptRoot "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $preview = [System.Drawing.Bitmap]::new(($size * 8), ($size * 8))
    for ($y = 0; $y -lt $preview.Height; $y++) {
        for ($x = 0; $x -lt $preview.Width; $x++) {
            $preview.SetPixel($x, $y, $target.GetPixel([int][Math]::Floor($x / 8), [int][Math]::Floor($y / 8)))
        }
    }
    $preview.Save((Join-Path $PSScriptRoot "$name-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "$name : $size x $size; corner alpha $($target.GetPixel(0,0).A)"
    $source.Dispose(); $target.Dispose(); $preview.Dispose()
}

