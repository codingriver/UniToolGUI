# -*- coding: utf-8 -*-
path = r'd:\UniToolGUI\Assets\Plugins\API.md'
lines = open(path, encoding='utf-8').readlines()
content = ''.join(lines)

# Find section 1 and replace it
old_section = '''## 1. TrayIconService 系统托盘

平台：Win / Mac | 接口：ITrayService | 入口：NativePlatform.Tray
v2：重构为单文件 #if 多平台，消除三份重复代码。

```csharp
void Initialize(); void Shutdown();
void SetTooltip(string tooltip);
void RegisterMenuItems(IEnumerable<TrayMenuItem> items);
void UnregisterMenuItems(...); void ClearMenuItems(); void RefreshMenu();
void ShowMainWindow();
void ShowBalloonTip(string title, string message, uint iconType=1, uint timeoutMs=5000);
event Action OnTrayIconCreated / OnTrayIconDestroyed;
event Action<int> OnHotkeyPressed;  // Win only
```
TrayMenuItem: Text, Callback, IsSeparator, IsToggle, Checked'''

new_section = '''## 1. TrayIconService 系统托盘

平台：Win / Mac | 接口：ITrayService | 入口：NativePlatform.Tray
v2：重构为单文件 #if 多平台，消除三份重复代码；新增自定义图标支持。

```csharp
void Initialize(); void Shutdown();
void SetTooltip(string tooltip);
void RegisterMenuItems(IEnumerable<TrayMenuItem> items);
void UnregisterMenuItems(...); void ClearMenuItems(); void RefreshMenu();
void ShowMainWindow();
void ShowBalloonTip(string title, string message, uint iconType=1, uint timeoutMs=5000);

// 自定义托盘图标（v2 新增）
void SetIcon(string iconPath);           // Win: .ico 路径；Mac: .png/.icns 路径（建议 18x18）
void SetIcon(byte[] pngData);            // 从内存 PNG 设置（仅 Mac 支持）
void SetIcon(Texture2D texture);         // 从 Texture2D 设置（仅 Mac，内部调用 EncodeToPNG）

event Action OnTrayIconCreated / OnTrayIconDestroyed;
event Action<int> OnHotkeyPressed;      // Win only
```

TrayMenuItem: Text, Callback, IsSeparator, IsToggle, Checked

**图标注意事项**：
- Windows：仅支持 `.ico` 文件路径；不支持从内存 PNG 直接设置
- macOS：支持 `.png`/`.icns` 文件路径，也支持内存 PNG 数据；图标会自动缩放至 18×18 并设为 template（自动适配深/浅色模式）
- `SetIcon()` 必须在 `Initialize()` 之后调用'''

if old_section in content:
    content = content.replace(old_section, new_section, 1)
    print('section replaced')
else:
    # Try to find and replace by line range
    print('exact match not found, appending note to section 1')
    # Find section 1 end (next ---)
    idx = content.find('## 1. TrayIconService')
    if idx != -1:
        end = content.find('\n---', idx)
        if end != -1:
            old_block = content[idx:end]
            # Check if SetIcon already documented
            if 'SetIcon' not in old_block:
                new_block = old_block.rstrip() + '''

// 自定义托盘图标（v2 新增）
void SetIcon(string iconPath);   // Win: .ico; Mac: .png/.icns
void SetIcon(byte[] pngData);    // 内存 PNG（仅 Mac）
void SetIcon(Texture2D texture); // Texture2D（仅 Mac）

**图标说明**：Win 仅支持 .ico 路径；Mac 支持 .png/.icns 路径和内存 PNG，自动缩放到 18x18，template=YES 适配深浅色。'''
                content = content[:idx] + new_block + content[end:]
                print('block patched')

# Update v2 log table
old_log = '| TrayIconService.cs | 三份重复代码 | 单文件 #if，公共 API 提顶层 |'
new_log = '| TrayIconService.cs | 三份重复代码；缺少自定义图标 | 单文件 #if；新增 SetIcon(path/bytes/Texture2D) |'
content = content.replace(old_log, new_log, 1)

# Update MacTray entries
old_mactray = '| MacTray.h/m | 缺 Minimize/Maximize/SetAlpha | 新增 6 函数，dispatch_sync 安全 |'
new_mactray = '| MacTray.h/m | 缺 Minimize/Maximize/SetAlpha；缺自定义图标 | 新增 6 窗口函数 + MacTray_SetIcon/SetIconFromData |'
content = content.replace(old_mactray, new_mactray, 1)

open(path, 'w', encoding='utf-8').write(content)
print('API.md updated, lines:', content.count('\n'))
