#!/usr/bin/env bash
set -euo pipefail

RULESYNC_VERSION="7.10.0"

if [[ $# -ge 1 && -n "${1}" && "${1}" != "latest" ]]; then
  RULESYNC_VERSION="${1}"
fi

BASE_URL="https://github.com/dyoshikawa/rulesync/releases/download/v${RULESYNC_VERSION}"

OS=$(uname -s | tr '[:upper:]' '[:lower:]')
ARCH=$(uname -m)

case "${OS}" in
  linux) OS_NAME="linux" ;;
  darwin) OS_NAME="darwin" ;;
  msys*|mingw*|cygwin*) OS_NAME="windows" ;;
  *)
    echo "Unsupported OS: ${OS}" >&2
    exit 1
    ;;
esac

case "${ARCH}" in
  x86_64|amd64) ARCH_NAME="x64" ;;
  arm64|aarch64) ARCH_NAME="arm64" ;;
  *)
    echo "Unsupported architecture: ${ARCH}" >&2
    exit 1
    ;;
esac

if [[ "${OS_NAME}" == "windows" ]]; then
  ASSET="rulesync-windows-${ARCH_NAME}.exe"
  DEST="/usr/local/bin/rulesync.exe"
else
  ASSET="rulesync-${OS_NAME}-${ARCH_NAME}"
  DEST="/usr/local/bin/rulesync"
fi

echo "Installing RuleSync ${RULESYNC_VERSION} (${ASSET})..."
USER_ID=$(id -u)
if [[ "${USER_ID}" -ne 0 ]]; then
  sudo curl -fsSL "${BASE_URL}/${ASSET}" -o "${DEST}"
  sudo chmod +x "${DEST}"
else
  curl -fsSL "${BASE_URL}/${ASSET}" -o "${DEST}"
  chmod +x "${DEST}"
fi

echo "RuleSync installed at ${DEST}"
