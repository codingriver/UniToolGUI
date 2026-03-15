# UniToolGUI Plugins - 完整技术文档

> 跨平台 API，支持 Windows / macOS / Linux。推荐入口：NativePlatform 门面类。

---

## 目录

- 0. NativePlatform 统一入口
- 1. TrayIconService 系统托盘
- 2. WindowsFileDialog 文件对话框
- 3. WindowsMessageBox 消息框
- 4. WindowsClipboard 剪贴板
- 5. WindowsShell Shell操作
- 6. WindowsSingleInstance 单实例
- 7. WindowsHotkey 全局热键
- 8. WindowsPower 电源控制
- 9. WindowsRecycleBin 回收站
- 10. WindowsWindow 窗口控制
- 11. WindowsStartup 开机自启
- 12. WindowsSystemInfo 系统信息
- 13. WindowsToast Toast通知
- 14. WindowsJumpList
- 15. WindowsAdmin 管理员检测
- 16. WindowsTheme 主题DPI
- 17. ProcessHelper 进程工具
- 18. 接口层与平台实现
- 平台支持矩阵
- 架构说明与优化日志

---

## 0. NativePlatform 统一入口

平台：Win / Mac / Linux（不支持功能返回空实现，不抛异常）

```csharp
bool NativePlatform.IsWindows / IsMacOS / IsLinux
IFileDialog     NativePlatform.FileDialog
IClipboard      NativePlatform.Clipboard
IToastService   NativePlatform.Toast
IMessageBox     NativePlatform.MessageBox
IShellService   NativePlatform.Shell
ISingleInstance NativePlatform.SingleInstance
ITrayService    NativePlatform.Tray
bool   NativePlatform.ShowToast(title, message, imagePath=null)
string NativePlatform.OpenFile(title, filter, ext=null, dir=null)
string NativePlatform.OpenFolder(title, dir=null)
bool   NativePlatform.OpenUrl(url)
string NativePlatform.GetClipboard()
bool   NativePlatform.SetClipboard(text)
```

```csharp
if (!NativePlatform.SingleInstance.TryAcquire("MyApp")) { Application.Quit(); return; }
NativePlatform.Tray.Initialize();
NativePlatform.Tray.SetTooltip("我的应用");
NativePlatform.ShowToast("启动", "就绪");
void OnApplicationQuit() { NativePlatform.SingleInstance.Release(); NativePlatform.Tray.Shutdown(); }
```

---

## 1. TrayIconService 系统托盘

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
- `SetIcon()` 必须在 `Initialize()` 之后调用

---

## 2. WindowsFileDialog 文件对话框

平台：Win / Mac / Linux | 接口：IFileDialog | 入口：NativePlatform.FileDialog
v2：macOS 改为 osascript stdin 内联执行，无临时文件；Linux 补全 SaveFilePanel/OpenFolderPanel。

```csharp
string   CreateFilter(params string[] filterPairs);
string   OpenFilePanel(string title, string filter, string ext=null, string dir=null);
string[] OpenFilePanelMulti(string title, string filter, string ext=null, string dir=null);
string   SaveFilePanel(string title, string filter, string ext=null, string dir=null, string name=null);
string   OpenFolderPanel(string title, string dir=null);
```

---

## 3. WindowsMessageBox 消息框

平台：Win / Mac / Linux | 接口：IMessageBox | 入口：NativePlatform.MessageBox

```csharp
void Info(string text, string caption="信息");
void Warning(string text, string caption="警告");
void Error(string text, string caption="错误");
bool Confirm(string text, string caption="确认");  // true=OK
bool YesNo(string text, string caption="请选择");  // true=Yes
int  Show(IntPtr hWnd, string text, string caption, int buttons=0, int icon=0);
```

---

## 4. WindowsClipboard 剪贴板

平台：Win / Mac / Linux | 接口：IClipboard | 入口：NativePlatform.Clipboard

```csharp
string GetText(); bool SetText(string text); bool HasText();
```

---

## 5. WindowsShell Shell操作

平台：Win / Mac / Linux | 接口：IShellService | 入口：NativePlatform.Shell

```csharp
bool OpenUrl(string url);
bool OpenFile(string filePath);
bool OpenFolder(string folderPath, string filePath=null);
bool Execute(string operation, string filePath, string parameters=null, string workingDir=null);
```

---

## 6. WindowsSingleInstance 单实例

平台：Win(Global Mutex) / Mac(~/Library/Caches) / Linux(~/.cache)
接口：ISingleInstance | 入口：NativePlatform.SingleInstance
v2：Mac/Linux 锁文件从 /tmp 改为用户私有缓存目录，避免系统清理和跨用户污染。

```csharp
bool TryAcquire(string mutexName=null);  // false=已有实例
void Release();
bool IsAnotherInstanceRunning(string mutexName=null);
```

---

## 7. WindowsHotkey 全局热键

平台：Win only（其他平台同签名空实现，编译不报错）
v2：补全完整 VK 常量表。

```csharp
bool   Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey);
bool   Unregister(IntPtr hwnd, int id);
IntPtr GetUnityWindowHandle();
// ModifierKeys: None|Alt|Ctrl|Shift|Win|NoRepeat
// VK.*: F1-F24, A-Z, D0-D9, Numpad0-9, Left/Up/Right/Down,
//       Home/End/PageUp/PageDown/Insert/Delete, Space/Enter/Escape/Tab/BackSpace,
//       Media*(NextTrack/PrevTrack/Stop/PlayPause), Volume*(Up/Down/Mute),
//       Browser*(Back/Forward/Refresh/Stop/Search/Home), Oem*(Semicolon/Plus/Comma/...)
```

---

## 8. WindowsPower 电源控制

平台：Win / Mac / Linux

```csharp
void PreventSleep(); void PreventSleepAndDisplay(); void AllowSleep(); void ResetIdleTimer();
```

---

## 9. WindowsRecycleBin 回收站

平台：Win / Mac / Linux

```csharp
int MoveToRecycleBin(string path, bool showConfirm=true);   // 0=成功
int MoveToRecycleBinSilent(string path);  // 多文件：路径以 \0 分隔
```
---

## 10. WindowsWindow 窗口控制

平台：Win / Mac(委托 MacWindowPlugin) / Linux X
v2：Win/Mac 均实现 Minimize/Maximize/Restore/SetOpacity。Mac 通过 NSWindow(dispatch_sync)。

```csharp
IntPtr GetUnityWindowHandle();
bool SetTopMost(IntPtr hwnd, bool topMost);
bool SetPositionAndSize(IntPtr hwnd, int x, int y, int w, int h);
bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int w, out int h);
bool Minimize(IntPtr hwnd);   // Mac: NSWindow.miniaturize
bool Maximize(IntPtr hwnd);   // Mac: NSWindow.zoom
bool Restore(IntPtr hwnd);    // Mac: deminiaturize or zoom toggle
bool IsMinimized(IntPtr hwnd); bool IsMaximized(IntPtr hwnd);
bool SetOpacity(IntPtr hwnd, float opacity);  // 0.0~1.0; Mac: alphaValue+setOpaque
// Unity 便捷方法（无需句柄）：SetUnityWindowTopMost/Rect/Opacity,
// Minimize/Maximize/Restore/IsMinimized/IsMaximized UnityWindow
```

```csharp
WindowsWindow.SetUnityWindowTopMost(true);
WindowsWindow.SetUnityWindowRect(100, 100, 1280, 720);
WindowsWindow.SetUnityWindowOpacity(0.9f);
WindowsWindow.MinimizeUnityWindow();
```

---

## 11. WindowsStartup 开机自启

平台：Win / Mac / Linux

```csharp
bool IsStartupEnabled();
bool EnableStartup(string exePath=null, string keyName=null);
bool DisableStartup(string keyName=null);
bool ToggleStartup(string exePath=null);
```

---

## 12. WindowsSystemInfo 系统信息

平台：Win / Mac / Linux

```csharp
(int w, int h)                   GetPrimaryScreenSize();
(int w, int h)                   GetVirtualScreenSize();
(bool onAC, int pct, int remSec) GetBatteryInfo();
uint GetIdleTimeSeconds(); string GetComputerName(); string GetUserName();
```

---

## 13. WindowsToast Toast通知

平台：Win / Mac / Linux | 接口：IToastService | 入口：NativePlatform.Toast
v2：Windows 改为内联 PowerShell -Command，无临时 .ps1 文件。

```csharp
bool Show(string title, string message, string imagePath=null);
// Win: inline PS/WinRT  Mac: osascript  Linux: notify-send
```

---

## 14. WindowsJumpList

平台：Win only

```csharp
void AddToRecentDocs(string filePath); void ClearRecentDocs();
```

---

## 15. WindowsAdmin 管理员检测

平台：Win / Mac / Linux

```csharp
bool IsRunningAsAdmin();
bool RestartAsAdmin(string args=null);  // Win: UAC; Linux: pkexec
```

---

## 16. WindowsTheme 主题DPI

平台：Win / Mac / Linux

```csharp
bool    IsDarkMode();
float   GetDpiScale();                      // 1.0=100%
float   GetDpiScaleForWindow(IntPtr hwnd);  // Win only
Color32 GetAccentColor();
```

---

## 17. ProcessHelper 进程工具

平台：Win / Mac / Linux
v2：RunAndRead 改为 BeginOutputReadLine+BeginErrorReadLine 异步事件，消除管道死锁。

```csharp
int     Run(string fileName, string arguments, int timeoutMs=5000);
string  RunAndRead(string fileName, string arguments, int timeoutMs=30000);
Process StartBackground(string fileName, string arguments);
string  Quote(string arg);
```

---

## 18. 接口层与平台实现

### 接口层 (Assets/Plugins/NativeKit/Interfaces/)

ITrayService / IFileDialog / IClipboard / IToastService / IMessageBox /
IShellService / IStartupService / IThemeService / ISingleInstance / ISystemInfo

### 平台实现层 (Assets/Plugins/NativeKit/Platform/)

| 类 | 说明 |
|----|------|
| NativeFileDialog / NullFileDialog | Win/Mac/Linux 委托 WindowsFileDialog |
| NativeClipboard / NullClipboard | Win=WindowsClipboard; 其他=GUIUtility |
| NativeToastService / NullToastService | 委托 WindowsToast |
| NativeMessageBox / NullMessageBox | 委托 WindowsMessageBox |
| NativeShellService / NullShellService | explorer / open / xdg-open |
| NativeSingleInstance / NullSingleInstance | 委托 WindowsSingleInstance |

---

## 平台支持矩阵

| 模块 | Win | Mac | Linux |
|------|:---:|:---:|:-----:|
| NativePlatform | OK | OK | OK |
| TrayIconService | OK | OK | - |
| FileDialog | OK | OK | OK |
| MessageBox | OK | OK | OK |
| Clipboard | OK | OK | OK |
| Shell | OK | OK | OK |
| SingleInstance | OK | OK | OK |
| Hotkey | OK | - | - |
| Power | OK | OK | OK |
| RecycleBin | OK | OK | OK |
| Window 位置/置顶 | OK | OK | - |
| Window 最小化/最大化/透明度 | OK | OK | - |
| Startup | OK | OK | OK |
| SystemInfo | OK | OK | OK |
| Toast | OK | OK | OK |
| JumpList | OK | - | - |
| Admin | OK | OK | OK |
| Theme | OK | OK | OK |
| ProcessHelper | OK | OK | OK |

---

## 架构说明与优化日志

### 目录结构

```
Assets/Plugins/NativeKit/
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
    ├── MacTray.h              <- ObjC 头 (Minimize/Maximize/SetAlpha/SetIcon)
    └── MacTray.m              <- ObjC 实现 (dispatch_sync 主线程安全)

# 其他 SDK 可并列放在 Plugins/ 根目录下：
# Assets/Plugins/FirebaseSDK/
# Assets/Plugins/AdjustSDK/
# Assets/Plugins/YourOtherLib/
```

### v2 优化日志

| 文件 | 问题 | 修复方案 |
|------|------|----------|
| ProcessHelper.cs | RunAndRead 管道缓冲满时死锁 | BeginOutputReadLine + BeginErrorReadLine 异步事件 |
| TrayIconService.cs | 三份重复代码；缺少自定义图标 | 单文件 #if；新增 SetIcon(path/bytes/Texture2D) |
| MacTray.h/m | 缺 Minimize/Maximize/SetAlpha；缺自定义图标 | 新增 6 窗口函数 + MacTray_SetIcon/SetIconFromData |
| MacWindowPlugin.cs | 缺对应绑定 | 新增 DllImport + 包装 |
| WindowsWindow.cs | Mac 缺窗口状态 API | 委托 MacWindowPlugin |
| WindowsToast.cs | Win 写临时 .ps1 | 改内联 -Command |
| WindowsFileDialog.cs | macOS 写临时 .scpt | 改 osascript stdin |
| WindowsSingleInstance.cs | 锁文件在 /tmp | Mac: ~/Library/Caches; Linux: ~/.cache |
| WindowsHotkey.cs | VK 常量不完整 | 补全 F1-F24/字母/数字/方向/媒体/浏览器/符号 |

### 设计原则

1. 接口驱动：业务代码依赖 I* 接口，便于测试和替换
2. 空对象模式：不支持平台返回 Null*，不抛异常
3. 门面模式：NativePlatform 统一入口，屏蔽平台差异
4. 零死锁：ProcessHelper.RunAndRead 异步事件消费 stdout/stderr
5. 无临时文件：Toast/FileDialog 均内联命令执行

### 注意事项

1. TrayIconService.Initialize() 必须在 Unity 主线程调用
2. Mac 托盘和窗口功能依赖编译好的 MacTray.bundle
3. Linux 依赖：zenity / notify-send / xdg-open / gio / systemd-inhibit
4. 文件对话框阻塞主线程直到用户操作完成
5. 不支持平台返回 false/null/0 并输出 LogWarning



