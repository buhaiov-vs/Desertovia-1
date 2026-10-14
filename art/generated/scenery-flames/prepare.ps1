Add-Type -AssemblyName System.Drawing
$strip = [System.Drawing.Bitmap]::new(64,16)
for ($i=1; $i -le 4; $i++) {
    $name = "effect-campfire-flames-frame-$i"
    $src = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot "$name-source.png"))
    $left=$src.Width; $top=$src.Height; $right=0; $bottom=0
    for ($y=0; $y -lt $src.Height; $y++) { for ($x=0; $x -lt $src.Width; $x++) {
        if ($src.GetPixel($x,$y).A -ge 128) {
            $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
            $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
        }
    }}
    $frame=[System.Drawing.Bitmap]::new(16,16)
    for ($y=0; $y -lt 10; $y++) { for ($x=0; $x -lt 8; $x++) {
        $sx=$left+[int][Math]::Floor(($x+0.5)*($right-$left+1)/8)
        $sy=$top+[int][Math]::Floor(($y+0.5)*($bottom-$top+1)/10)
        $pixel=$src.GetPixel($sx,$sy)
        $frame.SetPixel($x+4,$y+6,$pixel)
    }}
    $frame.Save((Join-Path $PSScriptRoot "$name.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $preview=[System.Drawing.Bitmap]::new(128,128)
    for ($y=0; $y -lt 16; $y++) { for ($x=0; $x -lt 16; $x++) { $strip.SetPixel(($i-1)*16+$x,$y,$frame.GetPixel($x,$y)) }}
    for ($y=0; $y -lt 128; $y++) { for ($x=0; $x -lt 128; $x++) { $preview.SetPixel($x,$y,$frame.GetPixel([int][Math]::Floor($x/8),[int][Math]::Floor($y/8))) }}
    $preview.Save((Join-Path $PSScriptRoot "$name-preview.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $basePixels=0
    for ($x=0; $x -lt 16; $x++) { if ($frame.GetPixel($x,15).A -ge 128) { $basePixels++ }}
    Write-Output "$name : 16x16, occupied bottom pixels=$basePixels"
    $frame.Dispose(); $preview.Dispose(); $src.Dispose()
}
$strip.Save((Join-Path $PSScriptRoot 'effect-campfire-flames.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$preview=[System.Drawing.Bitmap]::new(512,128)
for ($y=0; $y -lt 128; $y++) { for ($x=0; $x -lt 512; $x++) { $preview.SetPixel($x,$y,$strip.GetPixel([int][Math]::Floor($x/8),[int][Math]::Floor($y/8))) }}
$preview.Save((Join-Path $PSScriptRoot 'effect-campfire-flames-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose(); $strip.Dispose()
