<#
.SYNOPSIS
    Draws the animated sound wave of Voicer: the banner of the README and the wave of the project page.

.DESCRIPTION
    Rounded bars like those of the logo beat like a heart: a double pulse (lub-dub) runs across them from left to right,
    two faint waves sway behind them and the violet-to-blue colours of the logo flow through all of it. The animation
    is CSS and SMIL inside the SVG, so it plays wherever the picture is shown as an image (GitHub, the page) and stops
    for viewers who ask for less motion. Produces Docs/images/banner.svg (the logo and the name over the wave) and
    Sources/Apps/Voicer.Pages/assets/wave.svg (the wave alone, on a transparent ground).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$culture = [System.Globalization.CultureInfo]::InvariantCulture

function Format-Number([double] $value, [int] $digits) {
    $value.ToString("F$digits", $culture)
}

# The bars: a resting shape like the logo (higher in the middle, with a little life of its own); each bar starts its
# beat a moment after its left neighbour, so the pulse runs across.
function Get-Bars([double] $width, [double] $height, [int] $count, [double] $top) {
    $gap = $width / $count
    $barWidth = $gap * 0.46
    $middle = $top + $height / 2
    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $count; $i++) {
        $t = $i / ($count - 1)
        $envelope = 0.28 + 0.72 * [Math]::Pow([Math]::Sin([Math]::PI * $t), 1.4)
        $ripple = 0.75 + 0.25 * [Math]::Sin($i * 1.7) * [Math]::Cos($i * 0.6)
        $barHeight = [Math]::Max($barWidth, $height * $envelope * $ripple)
        $x = ($i + 0.5) * $gap - $barWidth / 2
        [void]$builder.Append(('<rect class="b" style="animation-delay:{0}s" x="{1}" y="{2}" width="{3}" height="{4}" rx="{5}"/>' -f
            (Format-Number ($i * 0.028) 3), (Format-Number $x 1), (Format-Number ($middle - $barHeight / 2) 1),
            (Format-Number $barWidth 1), (Format-Number $barHeight 1), (Format-Number ($barWidth / 2) 1)))
    }
    $builder.ToString()
}

# A sine two periods wider than the picture on each side, so a shift by one period loops without a seam.
function Get-Sine([double] $width, [double] $middle, [double] $amplitude, [double] $length, [double] $phase) {
    $points = for ($x = -$length * 2; $x -le $width + $length * 2; $x += 8) {
        $y = $middle + $amplitude * [Math]::Sin(($x / $length) * 2 * [Math]::PI + $phase)
        '{0} {1}' -f (Format-Number $x 0), (Format-Number $y 1)
    }
    'M' + ($points -join ' L')
}

# The flowing gradients and the glow of the bars.
function Get-Definitions([int] $width) {
    @"

  <defs>
    <linearGradient id="flow" x1="0" y1="0" x2="$width" y2="0" gradientUnits="userSpaceOnUse" spreadMethod="reflect">
      <stop offset="0" stop-color="#C084FC"/>
      <stop offset="0.35" stop-color="#818CF8"/>
      <stop offset="0.65" stop-color="#60A5FA"/>
      <stop offset="1" stop-color="#3B82F6"/>
      <animateTransform attributeName="gradientTransform" type="translate" values="0 0; $width 0; 0 0" dur="12s" repeatCount="indefinite"/>
    </linearGradient>
    <linearGradient id="flow2" x1="0" y1="0" x2="$width" y2="0" gradientUnits="userSpaceOnUse" spreadMethod="reflect">
      <stop offset="0" stop-color="#3B82F6"/>
      <stop offset="0.5" stop-color="#A78BFA"/>
      <stop offset="1" stop-color="#C084FC"/>
      <animateTransform attributeName="gradientTransform" type="translate" values="0 0; -$width 0; 0 0" dur="16s" repeatCount="indefinite"/>
    </linearGradient>
    <filter id="glow" x="-10%" y="-40%" width="120%" height="180%">
      <feGaussianBlur stdDeviation="6" result="blur"/>
      <feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter>
  </defs>
"@.TrimEnd()
}

# The wave: its styles and its shapes, in a band of the given top and height.
function Get-Wave([int] $width, [double] $top, [double] $height, [int] $count) {
    $middle = $top + $height / 2
    $length1 = 420
    $length2 = 300
    $style = @"

  <style>
    .b { fill: url(#flow); transform-box: fill-box; transform-origin: center; animation: beat 1.9s ease-in-out infinite; }
    /* lub-dub: two quick rises, then rest */
    @keyframes beat {
      0%, 100% { transform: scaleY(0.42); opacity: 0.75; }
      9% { transform: scaleY(1.08); opacity: 1; }
      17% { transform: scaleY(0.58); }
      25% { transform: scaleY(0.92); opacity: 1; }
      38% { transform: scaleY(0.45); opacity: 0.8; }
    }
    .s1 { animation: sway 7s linear infinite; }
    .s2 { animation: sway2 9s linear infinite; }
    @keyframes sway { from { transform: translateX(0); } to { transform: translateX(-${length1}px); } }
    @keyframes sway2 { from { transform: translateX(-${length2}px); } to { transform: translateX(0); } }
    @media (prefers-reduced-motion: reduce) { .b, .s1, .s2 { animation: none; } }
  </style>
"@.TrimEnd()
    $body = @"

  <g fill="none" stroke-linecap="round">
    <path class="s1" d="$(Get-Sine $width $middle ($height * 0.32) $length1 0)" stroke="url(#flow2)" stroke-width="3" opacity="0.35"/>
    <path class="s2" d="$(Get-Sine $width $middle ($height * 0.22) $length2 1.3)" stroke="url(#flow)" stroke-width="2" opacity="0.3"/>
  </g>
  <g filter="url(#glow)">$(Get-Bars $width $height $count $top)</g>
"@.TrimEnd()
    @{ Style = $style; Body = $body }
}

function Write-Svg([string] $path, [string] $content) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [System.IO.File]::WriteAllText($path, $content.Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding $false))
}

# The README banner: the logo and the name over the wave.
$width = 1280
$height = 360
$wave = Get-Wave $width 190 140 72
$font = "Inter, 'Segoe UI', system-ui, -apple-system, sans-serif"
$banner = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 $width $height" width="$width" height="$height" role="img" aria-labelledby="title desc">
  <title id="title">Voicer</title>
  <desc id="desc">Voicer: speech to text and text to speech on your own computer. A sound wave beats like a heart.</desc>$(Get-Definitions $width)
  <defs>
    <linearGradient id="ground" x1="0" y1="0" x2="$width" y2="$height" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#202328"/>
      <stop offset="1" stop-color="#16181B"/>
    </linearGradient>
    <radialGradient id="glowViolet" cx="300" cy="120" r="420" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#C084FC" stop-opacity="0.26"/>
      <stop offset="1" stop-color="#C084FC" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="glowBlue" cx="1020" cy="280" r="460" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#3B82F6" stop-opacity="0.24"/>
      <stop offset="1" stop-color="#3B82F6" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="logo" x1="0" y1="23" x2="0" y2="233" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#C084FC"/>
      <stop offset="1" stop-color="#3B82F6"/>
    </linearGradient>
  </defs>$($wave.Style)
  <rect width="$width" height="$height" rx="24" fill="url(#ground)"/>
  <rect width="$width" height="$height" rx="24" fill="url(#glowViolet)"/>
  <rect width="$width" height="$height" rx="24" fill="url(#glowBlue)"/>
  <g transform="translate(458 46) scale(0.42)">
    <rect x="30" y="88" width="28" height="80" rx="14" fill="url(#logo)"/>
    <rect x="72" y="53" width="28" height="150" rx="14" fill="url(#logo)"/>
    <rect x="114" y="23" width="28" height="210" rx="14" fill="url(#logo)"/>
    <rect x="156" y="58" width="28" height="140" rx="14" fill="url(#logo)"/>
    <rect x="198" y="93" width="28" height="70" rx="14" fill="url(#logo)"/>
  </g>
  <text x="580" y="122" font-family="$font" font-size="68" font-weight="700" fill="#E6E8EB">Voicer</text>
  <text x="640" y="168" text-anchor="middle" font-family="$font" font-size="24" fill="#A0A6AE">Speech to text and text to speech, on your own computer</text>$($wave.Body)
</svg>

"@
Write-Svg (Join-Path $root 'Docs/images/banner.svg') $banner

# The wave of the project page: no ground, it lies on the page.
$width = 1200
$height = 260
$wave = Get-Wave $width 30 200 64
$page = @"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 $width $height" width="$width" height="$height" role="img" aria-label="A sound wave beating like a heart">$(Get-Definitions $width)$($wave.Style)$($wave.Body)
</svg>

"@
Write-Svg (Join-Path $root 'Sources/Apps/Voicer.Pages/assets/wave.svg') $page
