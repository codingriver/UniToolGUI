#!/bin/zsh
set -euo pipefail

TOKEN=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --token) TOKEN="$2"; shift 2 ;;
    *) echo "unknown arg: $1"; exit 1 ;;
  esac
done

STATE_DIR="/Users/Shared/UniTool/helper"
TRUST_FILE="${STATE_DIR}/trust.json"
mkdir -p "${STATE_DIR}"
if [[ -z "${TOKEN}" ]]; then
  TOKEN="unitool-default-token"
fi
cat > "${TRUST_FILE}" <<EOF
{"token":"${TOKEN}"}
EOF
chmod 644 "${TRUST_FILE}"
echo "trust file written: ${TRUST_FILE}"
echo "trust refreshed"
