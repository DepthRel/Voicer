<#
.SYNOPSIS
    Builds the Windows installer of Voicer with Inno Setup.

.DESCRIPTION
    Steps:
      1. publish-win.ps1 prepares artifacts/publish/<runtime> and Voicer-<version>-<runtime>.zip (skip it with
         -SkipPublish);
      2. Assets/generate-installer-art.ps1 draws the pictures of the wizard in the colours of Voicer;
      3. ISCC compiles Scripts/App/voicer.iss into artifacts/publish/Voicer-<version>-setup.exe;
      4. the size and the SHA-256 of the installer are printed.

    A page of the wizard, right after the greeting, asks whom to install for. For the current user (the default, no
    administrator rights) it installs into %LOCALAPPDATA%\Programs\Voicer, and the models, tools and settings stay in the
    Data folder inside the installation, so an update never touches them. For everyone it starts itself again with
    administrator rights (/ALLUSERS) and installs into Program Files without the voicer.portable marker: a user cannot
    write there, so every user keeps the data in %LOCALAPPDATA%\Voicer\Data. The wizard first asks for its language,
    with the language of the operating system preselected.

.PARAMETER Runtime
    The runtime identifier. Default: win-x64.

.PARAMETER Configuration
    The build configuration. Default: Release.

.PARAMETER Sign
    Signs the executables, the installer and the uninstaller with the certificate named in the environment
    (VOICER_SIGN_THUMBPRINT, or VOICER_SIGN_PFX and VOICER_SIGN_PFX_PASSWORD; see Shared/sign-file.ps1). An unsigned
    installer makes Windows SmartScreen warn on every download.

.PARAMETER SkipPublish
    Uses the package already prepared in artifacts/publish/<runtime>.

.EXAMPLE
    ./Scripts/App/build-installer.ps1
    ./Scripts/App/build-installer.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    [string] $Runtime = 'win-x64',
    [string] $Configuration = 'Release',
    [switch] $SkipPublish,
    [switch] $Sign
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param() defaults, so paths are resolved here.
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$output = Join-Path $root 'artifacts\publish'
$stage = Join-Path $output $Runtime

if (-not $SkipPublish) {
    & (Join-Path $scriptDir 'publish-win.ps1') -Runtime $Runtime -Configuration $Configuration -Sign:$Sign
    if (-not $?) {
        throw 'publish-win.ps1 failed.'
    }
}

if (-not (Test-Path (Join-Path $stage 'Voicer.exe'))) {
    throw "The package is not prepared: $stage. Run publish-win.ps1 first."
}

$ui = Join-Path $root 'Sources\Apps\Voicer.UI\Voicer.UI.csproj'
$version = (& dotnet msbuild $ui -nologo -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) {
    throw 'Cannot read the version from the project.'
}

$projectUrl = (& dotnet msbuild $ui -nologo -getProperty:PackageProjectUrl).Trim()
if ($LASTEXITCODE -ne 0 -or -not $projectUrl) {
    throw 'Cannot read the address of the project page (PackageProjectUrl) from the project.'
}

$iscc = (Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    # Any installed version of Inno Setup (6, 7 and newer), including an installation into the user profile.
    $roots = @("${env:ProgramFiles(x86)}", "$env:ProgramFiles", "$env:LOCALAPPDATA\Programs") | Where-Object { $_ -and (Test-Path $_) }
    $iscc = $roots |
        ForEach-Object { Get-ChildItem -Path $_ -Filter 'Inno Setup*' -Directory -ErrorAction SilentlyContinue } |
        ForEach-Object { Join-Path $_.FullName 'ISCC.exe' } |
        Where-Object { Test-Path $_ } |
        Sort-Object -Descending |
        Select-Object -First 1
}

if (-not $iscc) {
    throw @'
Inno Setup was not found. Install it (free, https://jrsoftware.org/isdl.php) or add ISCC.exe to PATH, then run the
script again. The package is ready in artifacts/publish/<runtime>: it can be used without the installer.
'@
}

# The pictures of the wizard (the logo instead of the stock box) are drawn right before compiling.
$art = Join-Path $root 'artifacts\installer-art'
& (Join-Path $scriptDir '..\Assets\generate-installer-art.ps1') -OutputDirectory $art

# The license page of the wizard in every language: a short introduction and the English text of the GPL.
. (Join-Path $scriptDir '..\Shared\package-notes.ps1')
$license = Join-Path $root 'artifacts\installer-license'
Write-InstallerLicenses -Target $license

# Inno Setup calls this command for the installer and the uninstaller ($f is the file, in quotes).
[string[]] $signArguments = @()
if ($Sign) {
    $signScript = Join-Path $scriptDir '..\Shared\sign-file.ps1'
    $signArguments = @('/DVoicerSign=1', "/Svoicersign=powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$signScript`" -File `$f")
}

Write-Host "==> Inno Setup: $iscc"
& $iscc `
    "/DVoicerVersion=$version" `
    "/DVoicerUrl=$projectUrl" `
    "/DVoicerSource=$stage" `
    "/DVoicerOutputDir=$output" `
    "/DVoicerIcon=$(Join-Path $root 'Sources\Apps\Voicer.UI\Assets\voicer.ico')" `
    "/DInstallerArt=$art" `
    "/DInstallerLicense=$license" `
    @signArguments `
    (Join-Path $scriptDir 'voicer.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$setup = Join-Path $output "Voicer-$version-setup.exe"
if (-not (Test-Path $setup)) {
    throw "The installer was not created: $setup"
}

$hash = (Get-FileHash -Algorithm SHA256 $setup).Hash
$size = (Get-Item $setup).Length
Write-Host ''
Write-Host "Installer: $setup"
Write-Host ('Size: {0:N1} MB' -f ($size / 1MB))
Write-Host "SHA-256: $hash"
