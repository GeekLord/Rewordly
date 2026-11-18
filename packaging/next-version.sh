#!/usr/bin/env bash
# Prints the next release version (no "v").
# Usage: next-version.sh <major|minor|patch> [explicit-version]
# An explicit version (with or without "v") wins. Otherwise the latest vX.Y.Z tag is bumped;
# with no tags yet, the csproj <Version> is the first release.
set -euo pipefail

BUMP="${1:-minor}"
EXPLICIT="${2:-}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -n "${EXPLICIT}" ]; then
  V="${EXPLICIT#v}"
else
  LAST="$(git -C "${REPO_ROOT}" tag --list 'v[0-9]*.[0-9]*.[0-9]*' --sort=-v:refname | head -n1)"
  if [ -z "${LAST}" ]; then
    V="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "${REPO_ROOT}/src/Rewordly.Desktop/Rewordly.Desktop.csproj" | head -n1)"
  else
    IFS=. read -r MA MI PA <<< "${LAST#v}"
    PA="${PA%%[!0-9]*}"
    case "${BUMP}" in
      major) V="$((MA + 1)).0.0" ;;
      minor) V="${MA}.$((MI + 1)).0" ;;
      patch) V="${MA}.${MI}.$((PA + 1))" ;;
      *) echo "Unknown bump '${BUMP}' (use major, minor or patch)" >&2; exit 1 ;;
    esac
  fi
fi

if ! [[ "${V}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid version '${V}' (expected X.Y.Z or X.Y.Z-suffix)" >&2
  exit 1
fi
if git -C "${REPO_ROOT}" rev-parse -q --verify "refs/tags/v${V}" >/dev/null; then
  echo "Tag v${V} already exists" >&2
  exit 1
fi
echo "${V}"
