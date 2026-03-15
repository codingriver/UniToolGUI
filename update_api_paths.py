# -*- coding: utf-8 -*-
path = r'd:\UniToolGUI\Assets\Plugins\NativeKit\API.md'
lines = open(path, encoding='utf-8').readlines()
content = ''.join(lines)

# Update all path references from Assets/Plugins/ to Assets/Plugins/NativeKit/
content = content.replace('Assets/Plugins/', 'Assets/Plugins/NativeKit/')

# Update directory structure block
old_struct = '''Assets/Plugins/
├── NativePlatform.cs          <- 统一门面，推荐入口
├── Interfaces/                <- 10 个平台无关接口
├── Platform/                  <- Native*/Null* 实现对
├── ProcessHelper.cs           <- 异步读取，无死锁
├── TrayIconService.cs         <- Win/Mac #if 单文件
├── WindowsFileDialog.cs       <- Win/Mac osascript-stdin/Linux zenity
├── WindowsHotkey.cs           <- 完整 VK 常量表
├── WindowsSingleInstance.cs   <- Win Mutex / Mac-Linux 用户缓存锁文件
├── WindowsToast.cs            <- 内联命令，无临时文件
├── WindowsWindow.cs           <- Win/Mac 窗口控制
├── MacWindowPlugin.cs         <- Mac 插件封装
└── MacOS/MacTray/
    ├── MacTray.h              <- ObjC 头 (新增 Minimize/Maximize/SetAlpha)
    └── MacTray.m              <- ObjC 实现 (dispatch_sync 主线程安全)'''

new_struct = '''Assets/Plugins/
└── NativeKit/                      <- 原生系统集成库（本套）
    ├── NativePlatform.cs           <- 统一门面，推荐入口
    ├── Interfaces/                 <- 10 个平台无关接口
    ├── Platform/                   <- Native*/Null* 实现对
    ├── ProcessHelper.cs            <- 异步读取，无死锁
    ├── TrayIconService.cs          <- Win/Mac #if 单文件
    ├── WindowsFileDialog.cs        <- Win/Mac osascript-stdin/Linux zenity
    ├── WindowsHotkey.cs            <- 完整 VK 常量表
    ├── WindowsSingleInstance.cs    <- Win Mutex / Mac-Linux 用户缓存锁文件
    ├── WindowsToast.cs             <- 内联命令，无临时文件
    ├── WindowsWindow.cs            <- Win/Mac 窗口控制
    ├── MacWindowPlugin.cs          <- Mac 插件封装
    └── MacOS/MacTray/
        ├── MacTray.h               <- ObjC 头 (Minimize/Maximize/SetAlpha/SetIcon)
        └── MacTray.m               <- ObjC 实现 (dispatch_sync 主线程安全)

    # 后续其他 SDK 可并列放在 Plugins/ 根目录下：
    # Plugins/FirebaseSDK/
    # Plugins/AdjustSDK/
    # Plugins/YourOtherLib/'''

if old_struct in content:
    content = content.replace(old_struct, new_struct, 1)
    print('struct block replaced')
else:
    print('struct block not found, skipping struct replace')

open(path, 'w', encoding='utf-8').write(content)
print('API.md updated, lines:', content.count('\n'))
