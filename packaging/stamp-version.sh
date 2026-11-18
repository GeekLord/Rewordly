#!/usr/bin/env bash
# Writes a released version into the repo: README download names and commands, both csproj
# <Version>s and Info.plist get the new version, and CHANGELOG [Unreleased] becomes [X.Y.Z] - date.
# Usage: stamp-version.sh <X.Y.Z>   (run by the Release workflow after a release is published)
set -euo pipefail

NEW="${1:?usage: stamp-version.sh X.Y.Z}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OLD="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${ROOT}/src/Rewordly.Desktop/Rewordly.Desktop.csproj" | head -n1)"
TODAY="$(date -u +%Y-%m-%d)"

if [ "${OLD}" != "${NEW}" ]; then
  OLD_RE="$(printf '%s' "${OLD}" | sed 's/\./\\./g')"
  # Whole-version matches only, so 12.0.0 or 2.0.01 are left alone.
  sed -i -E "s/(^|[^0-9.])${OLD_RE}([^0-9]|$)/\1${NEW}\2/g" "${ROOT}/README.md"
  sed -i -E "s#<Version>[^<]*</Version>#<Version>${NEW}</Version>#" \
    "${ROOT}/src/Rewordly.Desktop/Rewordly.Desktop.csproj" "${ROOT}/src/Rewordly.Core/Rewordly.Core.csproj"
  sed -i -E "/CFBundle(Short)?Version/s#<string>[^<]*</string>#<string>${NEW}</string>#" "${ROOT}/packaging/Info.plist"
fi

if ! grep -q "^## \[${NEW}\]" "${ROOT}/CHANGELOG.md"; then
  sed -i "0,/^## \[Unreleased\]/s//## [Unreleased]\n\n## [${NEW}] - ${TODAY}/" "${ROOT}/CHANGELOG.md"
fi
echo "Stamped ${OLD} -> ${NEW}"
