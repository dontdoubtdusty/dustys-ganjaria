# Turns a square picture (default: resources/art-reference/icon-reference.jpg, the chosen icon) into the game's
# icon files:
#   resources/icon.ico  the .exe icon (16 to 256 px, PNG-compressed entries)
#   resources/icon.png  256 px, loaded at start-up as the window and taskbar icon
# Run:  powershell -File art/icon/make_ico.ps1 [-Source <picture>]   (e.g. -Source art/icon/icon_2048.png for the
# Blender-drawn version)
param([string]$Source)
Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here "..\..")
if (-not $Source) { $Source = Join-Path $repo "resources\art-reference\icon-reference.jpg" }
$picture = [System.Drawing.Image]::FromFile((Resolve-Path $Source).Path)
$sizes = 16, 24, 32, 48, 64, 128, 256

# Scales the master down to one square size with high-quality filtering and returns PNG bytes.
function Get-SizedPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.DrawImage($picture, 0, 0, $size, $size)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

# Writes the .ico: a 6-byte header, one 16-byte entry per size, then each size's PNG data.
$images = @{}
foreach ($s in $sizes) { $images[$s] = Get-SizedPng $s }
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$s].Length); $w.Write([UInt32]$offset)
    $offset += $images[$s].Length
}
foreach ($s in $sizes) { $w.Write($images[$s]) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $repo "resources\icon.ico"), $out.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $repo "resources\icon.png"), $images[256])
[System.IO.File]::WriteAllBytes((Join-Path $here "icon_preview_48.png"), $images[48])
$picture.Dispose()
"Wrote resources\icon.ico and resources\icon.png"
