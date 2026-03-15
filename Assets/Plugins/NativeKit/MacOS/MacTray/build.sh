#!/bin/bash
# 在 Mac 上编译 MacTray.bundle
# 需要 Xcode Command Line Tools: xcode-select --install

cd "$(dirname "$0")"
clang -framework AppKit -framework Foundation -bundle -o MacTray.bundle MacTray.m
if [ $? -eq 0 ]; then
    echo "Build done: MacTray.bundle"
    # 复制到 Plugins 根目录（可选）
    if [ -d "../../" ]; then
        cp -f MacTray.bundle ../../
        echo "Copied to Assets/Plugins/MacTray.bundle"
    fi
fi
