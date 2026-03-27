#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC="${SCRIPT_DIR}/xpc_test.m"
OUT="/tmp/unitool_xpc_test"
TOKEN="${1:-unitool-default-token}"

clang -fobjc-arc -framework Foundation -framework xpc -o "${OUT}" "${SRC}"
"${OUT}" "${TOKEN}"
