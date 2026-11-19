<#
.SYNOPSIS
    Publishes Voicer as a self-contained Release build for Windows, creates the Voicer.exe launcher in the artifact root
    and packs a clean copy into Voicer-<version>-<runtime>.zip.

.DESCRIPTION
    Layout of artifacts/publish/<runtime> and of the archive (under Voicer/):
      Voicer.exe         the application host with the Voicer icon, pointing at ui\Voicer.UI.dll
      ui/                the program; it is also its own worker process (the speech engine runs there)
      voicer.portable    marks the root of the package: the data (models, tools, settings) goes to Data\ next to it
      README.txt         first start notes in English, README.<code>.txt in the other languages (Notes/)
      LICENSE.txt        the GPL-3.0 text; THIRD-PARTY-NOTICES.txt lists the components and their licenses

    Voicer.exe holds no absolute paths, so the folder can be moved or copied as a whole. The Data folder of a copy tried
    out from artifacts/publish/<runtime> (gigabytes of models) stays where it is and never goes into the archive.
    Whisper.net brings the native libraries of every platform; only those of this runtime are kept.
    App/build-installer.ps1 packs the same folder into a setup program.

.PARAMETER Runtime
    The runtime identifier. Default: win-x64.

.PARAMETER Configuration
    The build configuration. Default: Release.

.PARAMETER Sign
    Signs the executables of the package with the certificate named in the environment (see Shared/sign-file.ps1), so
    the .zip package and the installer built from it carry signed programs.

.PARAMETER Output
    The output root. The program is published to <Output>/<Runtime>/ui. Default: <repository>/artifacts/publish.

.EXAMPLE
    ./Scripts/App/publish-win.ps1
    ./Scripts/App/publish-win.ps1 -Runtime win-arm64
#>
[CmdletBinding()]
param(
    [string] $Runtime = 'win-x64',
    [string] $Configuration = 'Release',
    [string] $Output,
    [switch] $Sign
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults, so paths are resolved here.
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
if (-not $Output) {
    $Output = Join-Path $root 'artifacts\publish'
}

if ($Runtime -notmatch '^win-(x64|arm64)$') {
    throw "Unsupported runtime '$Runtime'. Use win-x64 or win-arm64."
}

$ui = Join-Path $root 'Sources\Apps\Voicer.UI\Voicer.UI.csproj'
$version = (& dotnet msbuild $ui -nologo -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) {
    throw 'Cannot read the version from the project.'
}

$stage = Join-Path $Output $Runtime
if (Test-Path $stage) {
    # A running copy of this folder holds its files open; publishing over it would fail half way.
    $stageFull = (Resolve-Path $stage).Path.TrimEnd('\') + '\'
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($stageFull, [StringComparison]::OrdinalIgnoreCase) })
    if ($running.Count -gt 0) {
        throw "Voicer is running from $stage (process $($running[0].Id), $($running[0].ProcessName)). Close it and publish again, or use -Output."
    }
}

# The program is rebuilt from scratch; the Data folder of a tried copy stays.
$target = Join-Path $stage 'ui'
if (Test-Path $target) {
    Remove-Item -LiteralPath $target -Recurse -Force
}

# The launcher is created while the UI is published (see the VoicerRootLauncher target of Voicer.UI.csproj).
$launcher = [System.IO.Path]::GetFullPath((Join-Path $stage 'Voicer.exe'))
Write-Host "==> ui ($Runtime, $Configuration) -> $target"
dotnet publish $ui -c $Configuration -r $Runtime --self-contained true -o $target -nologo -p:DebugType=none "-p:VoicerRootLauncherPath=$launcher"
if ($LASTEXITCODE -ne 0) {
    throw "Publishing failed with exit code $LASTEXITCODE."
}

$runtimeRoot = (Resolve-Path $stage).Path
if (-not (Test-Path (Join-Path $runtimeRoot 'Voicer.exe'))) {
    throw 'The root launcher Voicer.exe was not created.'
}

# Only the native libraries of Whisper.net for this platform stay (Whisper.net names the platform win-x64, win-arm64).
. (Join-Path $scriptDir '..\Shared\package-notes.ps1')
Remove-ForeignWhisperRuntimes -Program $target -Platform $Runtime

# The root of the package: the data goes to Data\ next to this marker.
$marker = Join-Path $runtimeRoot 'voicer.portable'
if (-not (Test-Path $marker)) {
    Set-Content -Path $marker -Encoding ascii -Value 'Voicer package root: models, tools and settings are kept in the Data folder next to this file.'
}

# The note of the package in every language, from the templates in Notes.
Write-PackageNotes -Notes (Join-Path $scriptDir 'Notes') -Target $runtimeRoot -Platform win -Version $version -Runtime $Runtime
Write-PackageLicenses -Target $runtimeRoot

if ($Sign) {
    foreach ($exe in Get-ChildItem $runtimeRoot -Recurse -Filter '*.exe' -File) {
        & (Join-Path $scriptDir '..\Shared\sign-file.ps1') -File $exe.FullName
        if (-not $?) {
            throw "Signing '$($exe.FullName)' failed."
        }
    }
}

# A clean package: without the Data folder (downloaded models, tools and settings of the copy tried here).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$package = Join-Path (Resolve-Path $Output).Path "Voicer-$version-$Runtime.zip"
if (Test-Path $package) {
    [System.IO.File]::Delete($package)
}

$zip = [System.IO.Compression.ZipFile]::Open($package, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $runtimeRoot -Recurse -File) {
        $relative = $file.FullName.Substring($runtimeRoot.Length).TrimStart([char[]]@('\', '/'))
        $top = $relative.Split([char[]]@('\', '/'))[0]
        if ($top -eq 'Data') {
            continue
        }

        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, 'Voicer/' + $relative.Replace('\', '/'), [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Package (no models, tools or settings): $package"
Write-Host "Done. Artifacts: $runtimeRoot"
