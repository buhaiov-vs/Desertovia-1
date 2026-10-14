Add-Type -AssemblyName System.Drawing
$strip = [System.Drawing.Bitmap]::new(32,16)
for ($i=1; $i -le 2; $i++) {
    $frame=[System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot "character-citizen-male-1-carry-frame-$i.png"))
    for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){$strip.SetPixel(($i-1)*16+$x,$y,$frame.GetPixel($x,$y))}}
    $frame.Dispose()
}
$strip.Save((Join-Path $PSScriptRoot 'character-citizen-male-1-carry.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$preview=[System.Drawing.Bitmap]::new(256,128)
for($y=0;$y -lt 128;$y++){for($x=0;$x -lt 256;$x++){$preview.SetPixel($x,$y,$strip.GetPixel([int][Math]::Floor($x/8),[int][Math]::Floor($y/8)))}}
$preview.Save((Join-Path $PSScriptRoot 'character-citizen-male-1-carry-preview.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose(); $strip.Dispose()



