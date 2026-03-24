#!/bin/zsh
# MacTray.bundle 编译脚本
# 依赖: Xcode Command Line Tools -> xcode-select --install

set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

SRC="MacTray.m"
BUNDLE_MACOS="../MacTray.bundle/Contents/MacOS"
OUT="${BUNDLE_MACOS}/MacTray"
TMP="/tmp/MacTray_build_$$"

mkdir -p "$BUNDLE_MACOS"

echo "[build] Compiling $SRC ..."
clang \
    -arch arm64 -arch x86_64 \
    -framework AppKit \
    -framework Foundation \
    -framework UserNotifications \
    -fobjc-arc \
    -bundle \
    -o "$TMP" \
    "$SRC"

echo "[build] Compiled ok, installing to $OUT"
# 若目标被占用则先删除再移动
if ! cp -f "$TMP" "$OUT" 2>/dev/null; then
    echo "[build] cp failed (file locked?), trying rm+mv"
    rm -f "$OUT"
    mv "$TMP" "$OUT"
else
    rm -f "$TMP"
fi

chmod 755 "$OUT"
echo "[build] SUCCESS"
ls -lh "$OUT"
