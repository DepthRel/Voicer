#!/usr/bin/env bash
# Publishes Voicer as a self-contained Release build for macOS: the .tar.gz archive.
# On macOS the programs get an ad-hoc signature; built elsewhere they are unsigned (see the notes of the package).
# Usage: ./Scripts/App/publish-macos.sh [runtime=osx-arm64] [configuration=Release] [output=artifacts/publish]
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$script_dir/publish-common.sh" "${1:-osx-arm64}" "${2:-Release}" ${3:+"$3"}
