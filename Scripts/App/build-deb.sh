#!/usr/bin/env bash
# Builds the Debian/Ubuntu installer of Voicer from a published Linux package folder.
# Called by publish-common.sh for linux-* runtimes when dpkg-deb is available; can also be run by hand.
# Usage: build-deb.sh <runtime: linux-x64|linux-arm64> <published folder> <version> <output folder>
#
# Layout of the installed package:
#   /opt/voicer/ui                           the published program, without the voicer.portable marker: /opt is not
#                                            writable for the users, so every user keeps the models, tools and
#                                            settings in ~/.local/share/Voicer/Data
#   /usr/bin/voicer                          the application
#   /usr/share/applications/voicer.desktop   the menu entry
#   /usr/share/icons/hicolor/{scalable,512x512,256x256}/apps/voicer.{svg,png}   the icon in every size a desktop asks
#   /usr/share/metainfo/io.github.depthrel.Voicer.metainfo.xml   what software centres show about the application
#   /usr/share/doc/voicer/copyright          where the license and the notices of the components are
# FFmpeg is a dependency: Voicer reads the sound of media files with it. The Vulkan loader lets the speech engine use
# the video card.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
assets="$script_dir/../../Sources/Apps/Voicer.UI/Assets"

runtime="${1:?runtime identifier required}"
published="${2:?published folder required}"
version="${3:?version required}"
output="${4:?output folder required}"

case "$runtime" in
  linux-x64) architecture=amd64 ;;
  linux-arm64) architecture=arm64 ;;
  *) echo "build-deb.sh: no Debian architecture for $runtime" >&2; exit 2 ;;
esac

# The maintainer written into the package; set VOICER_DEB_MAINTAINER to "Name <address>" to change it.
maintainer="${VOICER_DEB_MAINTAINER:-Voicer project <voicer@users.noreply.github.com>}"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
root="$work/voicer_${version}_$architecture"

# The program only: not the data of a tried copy, not the marker, not the launchers and notes of the archive.
mkdir -p "$root/opt/voicer"
cp -R "$published/ui" "$root/opt/voicer/ui"
cp "$published/LICENSE.txt" "$published/THIRD-PARTY-NOTICES.txt" "$root/opt/voicer/"

mkdir -p "$root/usr/share/doc/voicer"
cat > "$root/usr/share/doc/voicer/copyright" <<'COPYRIGHT'
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: Voicer
Source: https://github.com/DepthRel/Voicer

Files: *
Copyright: Voicer contributors
License: GPL-3.0-or-later
 The full text is in /opt/voicer/LICENSE.txt. The components Voicer contains, their licenses and where to get their
 source are listed in /opt/voicer/THIRD-PARTY-NOTICES.txt.
COPYRIGHT

mkdir -p "$root/usr/bin"
cat > "$root/usr/bin/voicer" <<'LAUNCHER'
#!/bin/sh
# Voicer installed from the Debian package.
cd /opt/voicer/ui && exec ./Voicer.UI "$@"
LAUNCHER

mkdir -p "$root/usr/share/applications"
cat > "$root/usr/share/applications/voicer.desktop" <<'ENTRY'
[Desktop Entry]
Type=Application
Name=Voicer
GenericName=Speech to text
Comment=Turn the speech of audio and video files into text
Comment[de]=Verwandelt die Sprache in Audio- und Videodateien in Text
Comment[es]=Convierte en texto el habla de archivos de audio y vídeo
Comment[fr]=Transforme en texte la parole des fichiers audio et vidéo
Comment[pt]=Transforma em texto a fala de ficheiros de áudio e vídeo
Comment[ru]=Превращает речь из аудио- и видеофайлов в текст
Exec=voicer
Icon=voicer
Terminal=false
Categories=AudioVideo;Audio;Utility;
Keywords=speech;transcription;whisper;subtitles;voice;
ENTRY

icons="$root/usr/share/icons/hicolor"
mkdir -p "$icons/scalable/apps" "$icons/512x512/apps" "$icons/256x256/apps"
cp "$assets/voicer.svg" "$icons/scalable/apps/voicer.svg"
cp "$assets/voicer-512.png" "$icons/512x512/apps/voicer.png"
cp "$assets/voicer-256.png" "$icons/256x256/apps/voicer.png"

mkdir -p "$root/usr/share/metainfo"
cat > "$root/usr/share/metainfo/io.github.depthrel.Voicer.metainfo.xml" <<METAINFO
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>io.github.depthrel.Voicer</id>
  <name>Voicer</name>
  <summary>Turn the speech of audio and video files into text</summary>
  <summary xml:lang="de">Verwandelt die Sprache in Audio- und Videodateien in Text</summary>
  <summary xml:lang="es">Convierte en texto el habla de archivos de audio y vídeo</summary>
  <summary xml:lang="fr">Transforme en texte la parole des fichiers audio et vidéo</summary>
  <summary xml:lang="pt">Transforma em texto a fala de ficheiros de áudio e vídeo</summary>
  <summary xml:lang="ru">Превращает речь из аудио- и видеофайлов в текст</summary>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>GPL-3.0-or-later</project_license>
  <developer id="io.github.depthrel">
    <name>DepthRel</name>
  </developer>
  <description>
    <p>Voicer recognizes the speech of any audio or video file with the Whisper model, on the video card or the
    processor of this computer, and exports the text to Markdown. Nothing leaves the computer.</p>
  </description>
  <launchable type="desktop-id">voicer.desktop</launchable>
  <icon type="stock">voicer</icon>
  <url type="homepage">https://github.com/DepthRel/Voicer</url>
  <url type="vcs-browser">https://github.com/DepthRel/Voicer</url>
  <categories>
    <category>AudioVideo</category>
  </categories>
  <releases>
    <release version="$version" date="$(date -u +%Y-%m-%d)" />
  </releases>
</component>
METAINFO

# Directories and files are readable by all; only programs (ELF executables, not shared libraries) and the launcher
# may run. A build on a Windows file system marks every file executable, so the bits are set here, not kept.
find "$root" -type d -exec chmod 755 {} +
find "$root" -type f -exec chmod 644 {} +
while IFS= read -r -d '' file; do
  if [ "$(head -c 4 "$file" | od -An -tx1 | tr -d ' ')" = 7f454c46 ] && [[ "$file" != *.so* ]]; then
    chmod 755 "$file"
  fi
done < <(find "$root/opt" -type f -print0)
chmod 755 "$root/usr/bin/voicer"

installed_size=$(du -sk --exclude=DEBIAN "$root" | cut -f1)
mkdir -p "$root/DEBIAN"
cat > "$root/DEBIAN/control" <<CONTROL
Package: voicer
Version: $version
Architecture: $architecture
Maintainer: $maintainer
Installed-Size: $installed_size
Depends: ffmpeg, libicu76 | libicu74 | libicu72 | libicu70, libssl3t64 | libssl3, libfontconfig1, libx11-6, libice6, libsm6
Recommends: libvulkan1
Section: sound
Priority: optional
Homepage: https://github.com/DepthRel/Voicer
Description: Turn the speech of audio and video files into text
 Voicer recognizes the speech of any audio or video file with the Whisper model, on the video card or the processor
 of this computer, and exports the text to Markdown. Nothing leaves the computer.
CONTROL

mkdir -p "$output"
package="$(cd "$output" && pwd)/Voicer-$version-$runtime.deb"
dpkg-deb --root-owner-group --build "$root" "$package" >/dev/null
echo "Package: $package"
