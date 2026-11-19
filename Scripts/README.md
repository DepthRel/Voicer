# Scripts

Every script finds the repository by itself, so it can be started from any folder. The packages go to
`artifacts/publish`; the folder of a runtime there (`artifacts/publish/win-x64`, …) is the published program itself
and may hold the `Data` folder of a copy tried out from it (gigabytes of models), which never goes into a package.

## The release bundle

`build-release.ps1` (Windows) builds every package of a release, one script after another: first the programs for
every system, then the installer.

| Step | Script | Makes |
|---|---|---|
| Windows | `App/publish-win.ps1` | `Voicer-<version>-win-x64.zip` |
| Linux | `App/publish-linux.ps1` (WSL) | `Voicer-<version>-linux-x64.tar.gz` and `Voicer-<version>-linux-x64.deb` |
| MacOS | `App/publish-macos.ps1` (WSL) | `Voicer-<version>-osx-arm64.tar.gz` (Apple silicon) |
| MacOSIntel | `App/publish-macos.ps1 -Runtime osx-x64` (WSL) | `Voicer-<version>-osx-x64.tar.gz` |
| WindowsSetup | `App/build-installer.ps1 -SkipPublish` | `Voicer-<version>-setup.exe` from the Windows folder of the first step |

Each runs in a PowerShell process of its own; the first failure stops the bundle (`-ContinueOnError` runs the rest,
`-Skip MacOS,MacOSIntel` leaves steps out, `-Sign` signs the Windows programs and the installer). At the end it lists
the packages of the version with their size and SHA-256 and writes `artifacts/publish/SHA256SUMS-<version>.txt`.

The machine needs the .NET SDK, WSL with Debian or Ubuntu (the first run installs the .NET SDK into `~/.dotnet` there,
without `sudo`) and Inno Setup 6 or newer. The macOS packages built on Linux are not signed: their `README.txt` says how
to start them on a Mac.

## App — the Voicer application

| Script | Runs on | Makes |
|---|---|---|
| `App/publish-win.ps1` | Windows | `artifacts/publish/<runtime>` with `Voicer.exe` and `ui/`, and `Voicer-<version>-<runtime>.zip` |
| `App/build-installer.ps1` | Windows | the same, plus `Voicer-<version>-setup.exe` (Inno Setup) from `App/voicer.iss` |
| `App/publish-linux.ps1`, `App/publish-macos.ps1` | Windows (WSL) | the Linux and macOS packages, built inside WSL by `App/publish-in-wsl.sh` (through `App/publish-in-wsl.ps1`) |
| `App/publish-linux.sh` | Linux | `Voicer-<version>-<runtime>.tar.gz` and, where `dpkg-deb` is, the `.deb` (`App/build-deb.sh`) |
| `App/publish-macos.sh` | macOS, Linux | `Voicer-<version>-<runtime>.tar.gz`; on macOS with an ad-hoc signature |

`App/publish-common.sh` does the work of both bash scripts. Whisper.net brings the native libraries of every platform;
a package keeps only those of its own (`Remove-ForeignWhisperRuntimes`, `remove_foreign_whisper_runtimes`).

The packages:

| Package | Installs into | Data (models, tools, settings) |
|---|---|---|
| `Voicer-<version>-setup.exe`, for the current user (the default) | `%LOCALAPPDATA%\Programs\Voicer`, no administrator rights | `Data` inside the installation (the `voicer.portable` marker) |
| `Voicer-<version>-setup.exe`, for all users | `Program Files`, with administrator rights | `%LOCALAPPDATA%\Voicer\Data` of each user (no marker) |
| `Voicer-<version>-<runtime>.zip`, `.tar.gz` | wherever it is unpacked | `Data` next to the program |
| `Voicer-<version>-<runtime>.deb` | `/opt/voicer`, with `voicer` in `/usr/bin`, the menu entry, the icons and the AppStream data; depends on `ffmpeg` | `~/.local/share/Voicer/Data` of each user |

## Assets — pictures made from sources

| Script | Makes |
|---|---|
| `Assets/generate-icon.ps1` | the icon of the application (`voicer.ico`, PNG 256/512), the same picture as `voicer.svg` |
| `Assets/generate-flags.ps1` | the flags of the language selector |
| `Assets/generate-wave.ps1` | the animated sound wave: the banner of the README (`Docs/images/banner.svg`) and the wave of the project page |
| `Assets/generate-installer-art.ps1` | the pictures of the setup wizard in `artifacts/installer-art` (the build script runs it) |

## Licenses

Every package carries `LICENSE.txt` (GPL-3.0-or-later) and `THIRD-PARTY-NOTICES.txt` from the root of the repository,
next to the program (`Write-PackageLicenses` and `write_package_licenses` in `Shared/package-notes.*`); the `.deb`
adds `/usr/share/doc/voicer/copyright`. The setup wizard shows a license page in every language: `Write-InstallerLicenses`
writes `artifacts/installer-license/LICENSE.<code>.txt` from `Shared/License/intro.<code>.txt` and the English GPL.

## Package notes

The `README.txt` of every package comes from templates, one per language: `App/Notes/README.<code>.txt`.
`README.en.txt` becomes `README.txt`, the others keep their code; `{version}` and `{runtime}` are filled in. A line
tagged `[win]`, `[linux]`, `[macos]` or `[unix]` (Linux and macOS) goes only into the packages of that system.

## Shared — helpers of the other scripts

| Script | Used by |
|---|---|
| `Shared/package-notes.ps1` | `App/publish-win.ps1` and `App/build-installer.ps1` (dot-sourced) |
| `Shared/package-notes.sh` | `App/publish-common.sh` (sourced) |
| `Shared/sign-file.ps1` | `App/publish-win.ps1` and the installer with `-Sign`; the certificate comes from `VOICER_SIGN_THUMBPRINT` or `VOICER_SIGN_PFX` + `VOICER_SIGN_PFX_PASSWORD` |

More: [Docs/En/Development.md](../Docs/En/Development.md).
