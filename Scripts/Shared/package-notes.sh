#!/usr/bin/env bash
# Writes the note of a package in every language from the templates of a Notes folder (source this file).
# A template is README.<code>.txt: README.en.txt becomes README.txt, the others keep their code. {version} and
# {runtime} are filled in. A line that starts with a tag ([win], [linux], [macos], or [unix] for both Linux and macOS)
# is kept only for that system, without the tag; a line without a tag is for every system. package-notes.ps1 does
# the same for the PowerShell scripts.
# Usage: write_package_notes <notes folder> <target folder> <linux|macos> <version> <runtime>
write_package_notes() {
  local notes="$1" target="$2" platform="$3" version="$4" runtime="$5" template code name
  for template in "$notes"/README.*.txt; do
    code="${template##*/README.}"
    code="${code%.txt}"
    if [ "$code" = en ]; then
      name=README.txt
    else
      name="README.$code.txt"
    fi

    # A checkout on Windows has CRLF line ends; the notes of a Linux or macOS package have LF.
    tr -d '\r' < "$template" | awk -v platform="$platform" -v version="$version" -v runtime="$runtime" '
      {
        line = $0
        if (match(line, /^\[[a-z]+\] /)) {
          tag = substr(line, 2, RLENGTH - 3)
          if (tag != platform && !(tag == "unix" && platform != "win")) {
            next
          }
          line = substr(line, RLENGTH + 1)
        }
        gsub(/[{]version[}]/, version, line)
        gsub(/[{]runtime[}]/, runtime, line)
        print line
      }' > "$target/$name"
  done
}

# Puts the license of Voicer (LICENSE.txt) and the notices of its components (THIRD-PARTY-NOTICES.txt) from the root of
# the repository into a package; the GPL asks for both next to the program. Line ends become LF.
# Usage: write_package_licenses <target folder>
write_package_licenses() {
  local target="$1" root name
  root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  for name in LICENSE.txt THIRD-PARTY-NOTICES.txt; do
    tr -d '\r' < "$root/$name" > "$target/$name"
  done
}

# Removes the native libraries of Whisper.net that belong to other platforms (runtimes/<platform> and
# runtimes/<kind>/<platform>); a package keeps the folders of its own platform only. Whisper.net names macOS "macos".
# Usage: remove_foreign_whisper_runtimes <program folder> <runtime identifier>
remove_foreign_whisper_runtimes() {
  local program="$1" own="${2/osx-/macos-}" folder
  local folders=()
  [ -d "$program/runtimes" ] || return 0
  # The folders are listed first and removed afterwards, so the search never walks into a removed one. macOS has bash
  # 3.2: no mapfile, and an empty array under "set -u" is an unbound variable, hence the loop and the ${...+...} form.
  while IFS= read -r -d '' folder; do
    folders+=("$folder")
  done < <(find "$program/runtimes" -mindepth 1 -type d -print0)
  for folder in ${folders[@]+"${folders[@]}"}; do
    if [ -d "$folder" ] && [ "$(basename "$folder")" != "$own" ] && find "$folder" -maxdepth 1 -type f | grep -q .; then
      rm -rf "$folder"
    fi
  done
  find "$program/runtimes" -mindepth 1 -type d -empty -delete
}
