# Generates a multi-size Poké Ball–inspired app.ico (original geometric art).
# Embeds PNG images inside the ICO (Vista+), including alpha transparency.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "src\PokeHex.Desktop\Assets"
$outIco = Join-Path $outDir "app.ico"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sizes = @(16, 32, 48, 256)

function New-PokeBallBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1, [int]($size * 0.06))
    $d = $size - (2 * $pad)
    $cx = $pad + ($d / 2.0)
    $cy = $pad + ($d / 2.0)
    $r = $d / 2.0

    $red = [System.Drawing.Color]::FromArgb(255, 214, 45, 52)
    $cream = [System.Drawing.Color]::FromArgb(255, 245, 238, 224)
    $band = [System.Drawing.Color]::FromArgb(255, 32, 34, 40)
    $ring = [System.Drawing.Color]::FromArgb(255, 28, 30, 36)
    $btn = [System.Drawing.Color]::FromArgb(255, 250, 248, 243)
    $btnInner = [System.Drawing.Color]::FromArgb(255, 210, 205, 195)
    $outline = [System.Drawing.Color]::FromArgb(255, 22, 24, 28)

    $pathClip = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pathClip.AddEllipse([float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r))
    $g.SetClip($pathClip)

    $brushRed = New-Object System.Drawing.SolidBrush $red
    $brushCream = New-Object System.Drawing.SolidBrush $cream
    $g.FillRectangle($brushRed, [float]($cx - $r - 1), [float]($cy - $r - 1), [float](2 * $r + 2), [float]($r + 2))
    $g.FillRectangle($brushCream, [float]($cx - $r - 1), [float]($cy - 1), [float](2 * $r + 2), [float]($r + 2))

    $bandH = [Math]::Max(2.0, $d * 0.11)
    $brushBand = New-Object System.Drawing.SolidBrush $band
    $g.FillRectangle($brushBand, [float]($cx - $r - 1), [float]($cy - $bandH / 2.0), [float](2 * $r + 2), [float]$bandH)

    $g.ResetClip()

    $penW = [Math]::Max(1.0, $size * 0.045)
    $penOutline = New-Object System.Drawing.Pen $outline, $penW
    $g.DrawEllipse($penOutline, [float]($cx - $r + $penW / 2), [float]($cy - $r + $penW / 2), [float](2 * $r - $penW), [float](2 * $r - $penW))

    $outerBtn = [Math]::Max(3.0, $d * 0.28)
    $midBtn = $outerBtn * 0.62
    $innerBtn = $outerBtn * 0.34
    $brushRing = New-Object System.Drawing.SolidBrush $ring
    $brushBtn = New-Object System.Drawing.SolidBrush $btn
    $brushBtnInner = New-Object System.Drawing.SolidBrush $btnInner
    $g.FillEllipse($brushRing, [float]($cx - $outerBtn / 2), [float]($cy - $outerBtn / 2), [float]$outerBtn, [float]$outerBtn)
    $g.FillEllipse($brushBtn, [float]($cx - $midBtn / 2), [float]($cy - $midBtn / 2), [float]$midBtn, [float]$midBtn)
    $g.FillEllipse($brushBtnInner, [float]($cx - $innerBtn / 2), [float]($cy - $innerBtn / 2), [float]$innerBtn, [float]$innerBtn)

    $brushRed.Dispose(); $brushCream.Dispose(); $brushBand.Dispose()
    $brushRing.Dispose(); $brushBtn.Dispose(); $brushBtnInner.Dispose()
    $penOutline.Dispose(); $pathClip.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

$images = @()
foreach ($s in $sizes) {
    $bmp = New-PokeBallBitmap $s
    $png = Get-PngBytes $bmp
    $images += [pscustomobject]@{ Size = $s; Data = $png }
    $bmp.Dispose()
}

$count = $images.Count
$headerSize = 6
$entrySize = 16
$dataOffset = $headerSize + ($count * $entrySize)

$msIco = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $msIco
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)

$offset = $dataOffset
foreach ($img in $images) {
    $w = if ($img.Size -ge 256) { [byte]0 } else { [byte]$img.Size }
    $bw.Write($w)
    $bw.Write($w)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$img.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $img.Data.Length
}

foreach ($img in $images) {
    $bw.Write($img.Data)
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($outIco, $msIco.ToArray())
$bw.Dispose(); $msIco.Dispose()

Write-Host "Icono generado: $outIco ($((Get-Item $outIco).Length) bytes)"
