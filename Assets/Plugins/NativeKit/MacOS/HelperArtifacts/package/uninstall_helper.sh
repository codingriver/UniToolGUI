#!/bin/zsh
set -euo pipefail

HELPER_DST="/Library/PrivilegedHelperTools/com.unitool.roothelper"
PLIST_DST="/Library/LaunchDaemons/com.unitool.roothelper.plist"
STATE_DIR="/Users/Shared/UniTool/helper"

launchctl bootout system/com.unitool.roothelper >/dev/null 2>&1 || true
rm -f "${PLIST_DST}" "${HELPER_DST}"
rm -rf "${STATE_DIR}"
echo "helper uninstalled"
