#!/bin/zsh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
HELPER_SRC="${ROOT_DIR}/MacRootHelper/Sources/RootHelper/root_helper_main.m"
BRIDGE_SRC="${ROOT_DIR}/MacRootHelper/Sources/Bridge/xpc_bridge.m"
PLIST_SRC="${ROOT_DIR}/MacRootHelper/Resources/plists/com.unitool.roothelper.plist"

HELPER_OUT_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/HelperArtifacts/roothelper"
PACKAGE_OUT_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package"
BRIDGE_BUNDLE_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle"
BRIDGE_BIN_DIR="${BRIDGE_BUNDLE_DIR}/Contents/MacOS"
BRIDGE_INFO_DIR="${BRIDGE_BUNDLE_DIR}/Contents"

mkdir -p "${HELPER_OUT_DIR}" "${PACKAGE_OUT_DIR}" "${BRIDGE_BIN_DIR}"

MODE="${1:-all}"

build_helper() {
  echo "[build] compiling root helper..."
  clang \
    -arch arm64 -arch x86_64 \
    -fobjc-arc \
    -framework Foundation \
    -lproc \
    -o "${HELPER_OUT_DIR}/com.unitool.roothelper" \
    "${HELPER_SRC}"

  chmod 755 "${HELPER_OUT_DIR}/com.unitool.roothelper"
}

build_bridge() {
  echo "[build] compiling xpc bridge bundle..."
  clang \
    -arch arm64 -arch x86_64 \
    -fobjc-arc \
    -framework Foundation \
    -bundle \
    -o "${BRIDGE_BIN_DIR}/UniToolXpcBridge" \
    "${BRIDGE_SRC}"

  cat > "${BRIDGE_INFO_DIR}/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.unitool.xpcbridge</string>
    <key>CFBundleExecutable</key>
    <string>UniToolXpcBridge</string>
    <key>CFBundlePackageType</key>
    <string>BNDL</string>
</dict>
</plist>
EOF
}

case "${MODE}" in
  --helper-only)
    build_helper
    ;;
  --bridge-only)
    build_bridge
    ;;
  all|--all|"")
    build_helper
    build_bridge
    ;;
  *)
    echo "[build] unknown mode: ${MODE}" >&2
    exit 1
    ;;
esac

cp "${PLIST_SRC}" "${PACKAGE_OUT_DIR}/com.unitool.roothelper.plist"
cp "${SCRIPT_DIR}/install_helper.sh" "${PACKAGE_OUT_DIR}/install_helper.sh"
cp "${SCRIPT_DIR}/uninstall_helper.sh" "${PACKAGE_OUT_DIR}/uninstall_helper.sh"
cp "${SCRIPT_DIR}/refresh_trust.sh" "${PACKAGE_OUT_DIR}/refresh_trust.sh"
if [[ -f "${HELPER_OUT_DIR}/com.unitool.roothelper" ]]; then
  cp "${HELPER_OUT_DIR}/com.unitool.roothelper" "${PACKAGE_OUT_DIR}/com.unitool.roothelper"
fi

chmod 755 "${PACKAGE_OUT_DIR}/install_helper.sh" "${PACKAGE_OUT_DIR}/uninstall_helper.sh" "${PACKAGE_OUT_DIR}/refresh_trust.sh"
echo "[build] success"
