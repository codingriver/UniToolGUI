#!/bin/zsh
set -euo pipefail

TOKEN=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --token) TOKEN="$2"; shift 2 ;;
    *) echo "unknown arg: $1"; exit 1 ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PACKAGE_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package"
HELPER_SRC="${SCRIPT_DIR}/com.unitool.roothelper"
PLIST_SRC="${SCRIPT_DIR}/com.unitool.roothelper.plist"
if [[ ! -f "${HELPER_SRC}" || ! -f "${PLIST_SRC}" ]]; then
  HELPER_SRC="${PACKAGE_DIR}/com.unitool.roothelper"
  PLIST_SRC="${PACKAGE_DIR}/com.unitool.roothelper.plist"
fi

HELPER_DST="/Library/PrivilegedHelperTools/com.unitool.roothelper"
PLIST_DST="/Library/LaunchDaemons/com.unitool.roothelper.plist"
STATE_DIR="/Users/Shared/UniTool/helper"
LOG_DIR="/Users/Shared/UniTool/logs"
TRUST_FILE="${STATE_DIR}/trust.json"

mkdir -p "/Library/PrivilegedHelperTools" "/Library/LaunchDaemons" "${STATE_DIR}" "${LOG_DIR}"
launchctl bootout system/com.unitool.roothelper >/dev/null 2>&1 || true

cp "${HELPER_SRC}" "${HELPER_DST}"
cp "${PLIST_SRC}" "${PLIST_DST}"
chown root:wheel "${HELPER_DST}" "${PLIST_DST}"
chmod 755 "${HELPER_DST}"
chmod 644 "${PLIST_DST}"

if [[ -z "${TOKEN}" ]]; then
  TOKEN="unitool-default-token"
fi
cat > "${TRUST_FILE}" <<EOF
{"token":"${TOKEN}"}
EOF
chmod 644 "${TRUST_FILE}"
echo "trust file written: ${TRUST_FILE}"

launchctl bootstrap system "${PLIST_DST}"
launchctl enable system/com.unitool.roothelper >/dev/null 2>&1 || true
echo "helper installed"
