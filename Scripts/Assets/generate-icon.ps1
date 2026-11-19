<#
.SYNOPSIS
    Renders the Voicer application icon: five rounded bars of a sound wave, violet at the top and blue at the bottom,
    over a soft glow.

.DESCRIPTION
    The master image is drawn at 1024x1024 and downscaled with bicubic filtering. Produces voicer.ico (16-256 px,
    PNG-compressed frames), voicer-256.png and voicer-512.png. The vector original is Assets/voicer.svg. Windows only
    (System.Drawing).

.PARAMETER OutputDirectory
    The target directory. Default: Sources/Apps/Voicer.UI/Assets.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $root 'Sources\Apps\Voicer.UI\Assets'
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function New-Color([string] $hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function New-Point([float] $x, [float] $y) { New-Object System.Drawing.PointF $x, $y }

$size = 1024
$master = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# The glow: a disc that fades from a translucent violet-blue in the centre to nothing at the edge.
$glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$glowPath.AddEllipse(32, 32, 960, 960)
$glow = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
$glow.CenterColor = [System.Drawing.Color]::FromArgb(110, 120, 130, 250)
$glow.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 96, 165, 250))
$g.FillPath($glow, $glowPath)

# The wave: five pills 112 wide with gaps of 56, centred on the canvas; the middle one is the tallest.
$heights = @(320, 600, 840, 560, 280)
$barWidth = 112
$gap = 56
$left = ($size - ($heights.Count * $barWidth + ($heights.Count - 1) * $gap)) / 2
$wave = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Point 0 92), (New-Point 0 932), (New-Color '#C084FC'), (New-Color '#3B82F6')
for ($i = 0; $i -lt $heights.Count; $i++) {
    $x = $left + $i * ($barWidth + $gap)
    $top = ($size - $heights[$i]) / 2
    $pill = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pill.AddArc($x, $top, $barWidth, $barWidth, 180, 180)
    $pill.AddArc($x, $top + $heights[$i] - $barWidth, $barWidth, $barWidth, 0, 180)
    $pill.CloseFigure()
    $g.FillPath($wave, $pill)
}
$g.Dispose()

function Get-ScaledPng([System.Drawing.Bitmap] $source, [int] $target) {
    $bitmap = New-Object System.Drawing.Bitmap $target, $target, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.DrawImage($source, 0, 0, $target, $target)
    $graphics.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return , $stream.ToArray()
}

[System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'voicer-512.png'), (Get-ScaledPng $master 512))
[System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'voicer-256.png'), (Get-ScaledPng $master 256))

# ICO with PNG-compressed frames.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = foreach ($s in $sizes) { , (Get-ScaledPng $master $s) }
$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$frames[$i].Length)
    $writer.Write([UInt32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'voicer.ico'), $ico.ToArray())
$writer.Dispose()
$master.Dispose()

Write-Host "Icon written to $OutputDirectory"
