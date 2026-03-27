#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
BUILD_SCRIPT="${SCRIPT_DIR}/build.sh"
PACKAGE_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package"
OUTPUT_DIR="${ROOT_DIR}/MacRootHelper/Build"
STAMP="$(date +"%Y%m%d-%H%M%S")"
ZIP_NAME="UniToolRootHelperPackage-${STAMP}.zip"
ZIP_PATH="${OUTPUT_DIR}/${ZIP_NAME}"

mkdir -p "${OUTPUT_DIR}"

echo "[package] building helper + bridge..."
"${BUILD_SCRIPT}" --all

if [[ ! -d "${PACKAGE_DIR}" ]]; then
  echo "[package] package dir not found: ${PACKAGE_DIR}" >&2
  exit 1
fi

if command -v zip >/dev/null 2>&1; then
  echo "[package] creating zip: ${ZIP_PATH}"
  (cd "${PACKAGE_DIR}" && zip -r -q "${ZIP_PATH}" .)
else
  echo "[package] zip not found, creating tar.gz instead"
  ZIP_PATH="${OUTPUT_DIR}/UniToolRootHelperPackage-${STAMP}.tar.gz"
  (cd "${PACKAGE_DIR}" && tar -czf "${ZIP_PATH}" .)
fi

echo "[package] done: ${ZIP_PATH}"
