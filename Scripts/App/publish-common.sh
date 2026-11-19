#!/usr/bin/env bash
# Shared publish logic for publish-linux.sh and publish-macos.sh.
# Publishes Voicer as a self-contained Release build into <output>/<runtime>/ui, keeps only the native libraries of
# that platform, adds the launchers and the notes, and packs a clean copy (no Data/) into
# Voicer-<version>-<runtime>.tar.gz. On Linux, where dpkg-deb is available, build-deb.sh makes
# Voicer-<version>-<runtime>.deb from the same folder.
# Usage: publish-common.sh <runtime> [configuration=Release] [output=artifacts/publish]
set -euo pipefail

runtime="${1:?runtime identifier required}"
configuration="${2:-Release}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$script_dir/../.." && pwd)"
output="${3:-$root/artifacts/publish}"

case "$runtime" in
  linux-x64|linux-arm64) platform=linux ;;
  osx-x64|osx-arm64) platform=macos ;;
  *) echo "Unsupported runtime '$runtime'. Use linux-x64, linux-arm64, osx-x64 or osx-arm64." >&2; exit 2 ;;
esac

ui_project="$root/Sources/Apps/Voicer.UI/Voicer.UI.csproj"
version="$(dotnet msbuild "$ui_project" -nologo -getProperty:Version | tr -d '[:space:]')"
if [ -z "$version" ]; then
  echo "Cannot read the version from the project." >&2
  exit 1
fi

mkdir -p "$output"
output="$(cd "$output" && pwd)"
stage="$output/$runtime"

# The program is rebuilt; the Data folder of a copy tried out from this folder stays.
rm -rf "${stage:?}/ui"
echo "==> ui ($runtime, $configuration) -> $stage/ui"
dotnet publish "$ui_project" -c "$configuration" -r "$runtime" --self-contained true -o "$stage/ui" -nologo -p:DebugType=none

# shellcheck source=../Shared/package-notes.sh
source "$script_dir/../Shared/package-notes.sh"
remove_foreign_whisper_runtimes "$stage/ui" "$runtime"

chmod +x "$stage/ui/Voicer.UI"
cp "$root/Sources/Apps/Voicer.UI/Assets/voicer-512.png" "$stage/voicer.png"
chmod 644 "$stage/voicer.png"

# The root of the package: the data (models, tools, settings) goes to Data/ next to this marker.
if [ ! -f "$stage/voicer.portable" ]; then
  echo "Voicer package root: models, tools and settings are kept in the Data folder next to this file." > "$stage/voicer.portable"
fi

# The launcher that works from any location.
cat > "$stage/Voicer.sh" <<'LAUNCHER'
#!/bin/bash
# Starts Voicer from any location.
cd "$(dirname "$0")/ui" && exec ./Voicer.UI "$@"
LAUNCHER
chmod +x "$stage/Voicer.sh"

case "$platform" in
  linux)
    cat > "$stage/install-desktop-entry.sh" <<'DESKTOP'
#!/bin/bash
# Registers Voicer in the application menu of the current user (paths are absolute: run again after moving the folder).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$HOME/.local/share/applications"
cat > "$HOME/.local/share/applications/voicer.desktop" <<ENTRY
[Desktop Entry]
Type=Application
Name=Voicer
Comment=Turn the speech of audio and video files into text
Comment[ru]=Превращает речь из аудио- и видеофайлов в текст
Exec="$here/ui/Voicer.UI"
Path=$here/ui
Icon=$here/voicer.png
Terminal=false
Categories=AudioVideo;Audio;Utility;
ENTRY
if command -v update-desktop-database >/dev/null; then
  update-desktop-database "$HOME/.local/share/applications" || true
fi
echo "Voicer added to the application menu."
DESKTOP
    chmod +x "$stage/install-desktop-entry.sh"
    ;;
  macos)
    cat > "$stage/Voicer.command" <<'LAUNCHER'
#!/bin/bash
# Starts Voicer with a double click in Finder.
cd "$(dirname "$0")/ui" && exec ./Voicer.UI
LAUNCHER
    chmod +x "$stage/Voicer.command"
    if command -v codesign >/dev/null; then
      # Ad-hoc signature: required for arm64 binaries; not a Developer ID signature.
      find "$stage" -type f \( -perm -u+x -o -name '*.dylib' \) -exec codesign --force --sign - {} \; 2>/dev/null ||
        echo "warning: ad-hoc signing failed" >&2
    fi
    ;;
esac

# The note of the package in every language, from the templates in Notes.
write_package_notes "$script_dir/Notes" "$stage" "$platform" "$version" "$runtime"
write_package_licenses "$stage"

# A clean package: without Data/ (models, tools and settings of the copy tried here), under Voicer/.
package="$output/Voicer-$version-$runtime.tar.gz"
if tar --version 2>/dev/null | grep -q 'GNU tar'; then
  rename=(--transform "s,^$runtime,Voicer,")
else
  rename=(-s ",^$runtime,Voicer,")
fi
tar -czf "$package" -C "$output" "${rename[@]}" --exclude="$runtime/Data" "$runtime"
echo "Package (no models, tools or settings): $package"

# The installer of Debian, Ubuntu and their relatives, where dpkg-deb is at hand (any of them, WSL included).
if [ "$platform" = linux ]; then
  if command -v dpkg-deb >/dev/null; then
    bash "$script_dir/build-deb.sh" "$runtime" "$stage" "$version" "$output"
  else
    echo "dpkg-deb is missing: no .deb package (it comes with Debian and Ubuntu, WSL included)." >&2
  fi
fi

echo "Done. Artifacts: $stage"
