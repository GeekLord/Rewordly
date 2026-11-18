#!/usr/bin/env bash
# Packages published Rewordly binaries into release archives in dist/
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PUB_DIR="${REPO_ROOT}/pub"
DIST_DIR="${REPO_ROOT}/dist"
ASSETS_DIR="${REPO_ROOT}/src/Rewordly.Desktop/Assets"
PACKAGING_DIR="${REPO_ROOT}/packaging"
README_FILE="${REPO_ROOT}/README.md"
LICENSE_FILE="${REPO_ROOT}/LICENSE"

# Extract version from csproj or fallback to 2.0.0
VERSION="${1:-${VERSION:-}}"
if [ -z "${VERSION}" ]; then
  CSPROJ="${REPO_ROOT}/src/Rewordly.Desktop/Rewordly.Desktop.csproj"
  VERSION=$(grep -oPm1 "(?<=<Version>)[^<]+" "${CSPROJ}" 2>/dev/null || echo "2.0.0")
fi

mkdir -p "${DIST_DIR}"

echo "Packaging Rewordly version ${VERSION}..."

# Windows targets
for r in win-x64 win-arm64; do
  if [ -d "${PUB_DIR}/${r}" ]; then
    echo "==> Packaging ${r}..."
    TMP_DIR=$(mktemp -d)
    mkdir -p "${TMP_DIR}/Rewordly"
    cp "${PUB_DIR}/${r}/Rewordly.exe" "${TMP_DIR}/Rewordly/"
    cp "${README_FILE}" "${TMP_DIR}/Rewordly/"
    [ -f "${LICENSE_FILE}" ] && cp "${LICENSE_FILE}" "${TMP_DIR}/Rewordly/"
    (cd "${TMP_DIR}" && zip -qr "${DIST_DIR}/Rewordly-${VERSION}-${r}.zip" Rewordly)
    rm -rf "${TMP_DIR}"
    echo "Created ${DIST_DIR}/Rewordly-${VERSION}-${r}.zip"
  fi
done

# macOS targets
for r in osx-x64 osx-arm64; do
  if [ -d "${PUB_DIR}/${r}" ]; then
    echo "==> Packaging ${r}..."
    TMP_DIR=$(mktemp -d)
    APP_DIR="${TMP_DIR}/Rewordly.app"
    mkdir -p "${APP_DIR}/Contents/MacOS" "${APP_DIR}/Contents/Resources"
    cp "${PUB_DIR}/${r}/Rewordly" "${APP_DIR}/Contents/MacOS/"
    # Copy any native dylibs if present
    shopt -s nullglob
    for f in "${PUB_DIR}/${r}/"*.dylib; do
      cp "$f" "${APP_DIR}/Contents/MacOS/"
    done
    shopt -u nullglob
    chmod 755 "${APP_DIR}/Contents/MacOS/Rewordly"

    cp "${ASSETS_DIR}/Rewordly.icns" "${APP_DIR}/Contents/Resources/"
    sed -E "/CFBundle(Short)?Version/s#<string>[^<]*</string>#<string>${VERSION}</string>#" "${PACKAGING_DIR}/Info.plist" > "${APP_DIR}/Contents/Info.plist"

    # Ad-hoc sign macOS bundle
    if command -v codesign >/dev/null 2>&1; then
      codesign --force --deep -s - "${APP_DIR}"
    elif command -v rcodesign >/dev/null 2>&1; then
      rcodesign sign "${APP_DIR}"
    elif [ -n "${RCODESIGN:-}" ] && [ -x "${RCODESIGN}" ]; then
      "${RCODESIGN}" sign "${APP_DIR}"
    else
      echo "WARNING: Neither codesign nor rcodesign found. ${r} app bundle is not signed."
    fi

    cp "${README_FILE}" "${TMP_DIR}/README.md"
    [ -f "${LICENSE_FILE}" ] && cp "${LICENSE_FILE}" "${TMP_DIR}/LICENSE"
    (cd "${TMP_DIR}" && zip -qry "${DIST_DIR}/Rewordly-${VERSION}-${r}.zip" Rewordly.app README.md $([ -f LICENSE ] && echo "LICENSE"))
    rm -rf "${TMP_DIR}"
    echo "Created ${DIST_DIR}/Rewordly-${VERSION}-${r}.zip"
  fi
done

# Linux targets
for r in linux-x64 linux-arm64; do
  if [ -d "${PUB_DIR}/${r}" ]; then
    echo "==> Packaging ${r}..."
    TMP_DIR=$(mktemp -d)
    ARCHIVE_DIR="Rewordly-${VERSION}-${r}"
    TARGET_DIR="${TMP_DIR}/${ARCHIVE_DIR}"
    mkdir -p "${TARGET_DIR}"
    cp "${PUB_DIR}/${r}/Rewordly" "${TARGET_DIR}/"
    chmod 755 "${TARGET_DIR}/Rewordly"
    cp "${ASSETS_DIR}/icon.png" "${TARGET_DIR}/rewordly.png"
    cp "${PACKAGING_DIR}/rewordly.desktop" "${PACKAGING_DIR}/install-linux.sh" "${README_FILE}" "${TARGET_DIR}/"
    [ -f "${LICENSE_FILE}" ] && cp "${LICENSE_FILE}" "${TARGET_DIR}/"
    tar -C "${TMP_DIR}" -czf "${DIST_DIR}/${ARCHIVE_DIR}.tar.gz" "${ARCHIVE_DIR}"
    rm -rf "${TMP_DIR}"
    echo "Created ${DIST_DIR}/${ARCHIVE_DIR}.tar.gz"
  fi
done

# Generate SHA256 checksums if any archives were created
if compgen -G "${DIST_DIR}/*.zip" >/dev/null || compgen -G "${DIST_DIR}/*.tar.gz" >/dev/null; then
  echo "==> Generating SHA256 checksums..."
  (cd "${DIST_DIR}" && sha256sum Rewordly-* > SHA256SUMS.txt 2>/dev/null || shasum -a 256 Rewordly-* > SHA256SUMS.txt)
  echo "Created ${DIST_DIR}/SHA256SUMS.txt"
fi

echo "Packaging complete. Output in ${DIST_DIR}:"
ls -la "${DIST_DIR}"
