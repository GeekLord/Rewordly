#!/usr/bin/env bash
# Publish self-contained single-file binaries for Rewordly
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/src/Rewordly.Desktop/Rewordly.Desktop.csproj"
PUB_DIR="${REPO_ROOT}/pub"

if [ -d "${HOME}/.dotnet" ] && [[ ":$PATH:" != *":${HOME}/.dotnet:"* ]]; then
  export DOTNET_ROOT="${HOME}/.dotnet"
  export PATH="${DOTNET_ROOT}:${PATH}"
fi

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

TARGETS=("linux-x64" "linux-arm64" "win-x64" "win-arm64" "osx-x64" "osx-arm64")

if [ "$#" -gt 0 ]; then
  TARGETS=("$@")
fi

mkdir -p "${PUB_DIR}"

for r in "${TARGETS[@]}"; do
  echo "==> Publishing for ${r}..."
  mkdir -p "${PUB_DIR}/${r}"
  dotnet publish "${PROJECT_PATH}" \
    -c Release \
    -r "${r}" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -p:EnableWindowsTargeting=true \
    -o "${PUB_DIR}/${r}"
  echo "==> Published ${r} successfully."
done

echo "==> All target builds finished."
