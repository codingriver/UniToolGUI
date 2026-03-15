# -*- coding: utf-8 -*-
append_text = """

---

## 1. 系统托盘 (TrayIconService)

**平台**：Win OK | Mac OK | Linux NO  
**接口**：`ITrayService` | **入口**：`NativePlatform.Tray`

```csharp
void Initialize();  // 必须在主线程调用
void Shutdown();
void SetTooltip(string tooltip);
void RegisterMenuItems(IEnumerable<TrayMenuItem> items);
void UnregisterMenuItems(IEnumerable<TrayMenuItem> items);
void ClearMenuItems(); void RefreshMenu(); void ShowMainWindow();
void ShowBalloonTip(string title, string message, uint iconType=1, uint timeoutMs=5000);
event Action OnTrayIconCreated;
event Action OnTrayIconDestroyed;
event Action<int> OnHotkeyPressed; // Win only
```

TrayMenuItem: Text, Callback, IsSeparator, IsToggle, Checked

---

## 2. 文件对话框 (WindowsFileDialog)

**平台**：Win OK | Mac OK(osascript stdin) | Linux OK(zenity)  
**接口**：`IFileDialog` | **入口**：`NativePlatform.FileDialog`

```csharp
string   CreateFilter(params string[] filterPairs);
string   OpenFilePanel(string title, string filter, string ext=null, string dir=null);
string[] OpenFilePanelMulti(string title, string filter, string ext=null, string dir=null);
string   SaveFilePanel(string title, string filter, string ext=null, string dir=null, string name=null);
string   OpenFolderPanel(string title, string dir=null);
```

---

## 3. 消息框 (WindowsMessageBox)

**平台**：Win OK | Mac OK | Linux OK  
**接口**：`IMessageBox` | **入口**：`NativePlatform.MessageBox`

```csharp
void Info(string text, string caption);
void Warning(string text, string caption);
void Error(string text, string caption);
bool Confirm(string text, string caption); // true=OK
bool YesNo(string text, string caption);   // true=Yes
```

---

## 4. 剪贴板 (WindowsClipboard)

**平台**：Win OK | Mac OK | Linux OK  
**接口**：`IClipboard` | **入口**：`NativePlatform.Clipboard`

```csharp
string GetText();
bool   SetText(string text);
bool   HasText();
// 快捷：NativePlatform.GetClipboard() / SetClipboard(text)
```

---

## 5. Shell 操作 (WindowsShell)

**平台**：Win OK | Mac OK | Linux OK  
**接口**：`IShellService` | **入口**：`NativePlatform.Shell`

```csharp
bool OpenUrl(string url);
bool OpenFile(string filePath);
bool OpenFolder(string folderPath, string filePath=null);
bool Execute(string op, string path, string args=null, string dir=null); // WindowsShell only
```

---

## 6. 单实例 (WindowsSingleInstance)

**平台**：Win OK(Global Mutex) | Mac OK(~/Library/Caches) | Linux OK(~/.cache)  
**接口**：`ISingleInstance` | **入口**：`NativePlatform.SingleInstance`

```csharp
bool TryAcquire(string mutexName=null); // false=already running
void Release();
bool IsAnotherInstanceRunning(string mutexName=null);
```

---

## 7. 全局热键 (WindowsHotkey)

**平台**：Win OK | Mac NO | Linux NO

```csharp
bool   Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey);
bool   Unregister(IntPtr hwnd, int id);
IntPtr GetUnityWindowHandle();
// ModifierKeys: None Alt Ctrl Shift Win NoRepeat
// VK.*: F1-F24, A-Z, D0-D9, Numpad0-9,
//       Left/Up/Right/Down, Home/End/PageUp/PageDown/Insert/Delete,
//       Space/Enter/Escape/Tab/BackSpace,
//       Media*, Volume*, Browser*, Oem*
```

---

## 8. 电源控制 (WindowsPower)

**平台**：Win OK | Mac OK | Linux OK

```csharp
void PreventSleep();            // 防止休眠
void PreventSleepAndDisplay();  // 防止休眠+保持屏幕
void AllowSleep();              // 恢复
void ResetIdleTimer();          // 重置空闲计时
```

---

## 9. 回收站 (WindowsRecycleBin)

**平台**：Win OK | Mac OK | Linux OK

```csharp
int MoveToRecycleBin(string path, bool showConfirm=true); // 0=success
int MoveToRecycleBinSilent(string path); // multi-file: separate by \0
```

---

## 10. 窗口控制 (WindowsWindow)

**平台**：Win OK | Mac OK(via MacWindowPlugin) | Linux NO

```csharp
IntPtr GetUnityWindowHandle();
bool SetTopMost(IntPtr hwnd, bool topMost);
bool SetPositionAndSize(IntPtr hwnd, int x, int y, int w, int h);
bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int w, out int h);
bool Minimize(IntPtr hwnd);   // Mac: NSWindow.miniaturize
bool Maximize(IntPtr hwnd);   // Mac: NSWindow.zoom
bool Restore(IntPtr hwnd);    // Mac: deminiaturize or zoom toggle
bool IsMinimized(IntPtr hwnd);
bool IsMaximized(IntPtr hwnd);
bool SetOpacity(IntPtr hwnd, float opacity); // 0.0~1.0; Mac: NSWindow.alphaValue
// Unity convenience (no handle needed):
bool SetUnityWindowTopMost(bool);
bool SetUnityWindowRect(int x, int y, int w, int h);
bool GetUnityWindowRect(out int x, out int y, out int w, out int h);
bool MinimizeUnityWindow() / MaximizeUnityWindow() / RestoreUnityWindow();
bool IsUnityWindowMinimized() / IsUnityWindowMaximized();
bool SetUnityWindowOpacity(float); // 0.0~1.0
```

---

## 11-17. 其他模块

| 模块 | 平台 | 主要 API |
|------|------|----------|
| WindowsStartup | Win/Mac/Linux | IsStartupEnabled, EnableStartup, DisableStartup, ToggleStartup |
| WindowsSystemInfo | Win/Mac/Linux | GetPrimaryScreenSize, GetBatteryInfo, GetIdleTimeSeconds, GetComputerName, GetUserName |
| WindowsToast | Win/Mac/Linux | Show(title, message, imagePath=null) |
| WindowsJumpList | Win only | AddToRecentDocs, ClearRecentDocs |
| WindowsAdmin | Win/Mac/Linux | IsRunningAsAdmin, RestartAsAdmin |
| WindowsTheme | Win/Mac/Linux | IsDarkMode, GetDpiScale, GetAccentColor |
| ProcessHelper | Win/Mac/Linux | Run, RunAndRead(async no-deadlock), StartBackground, Quote |

---

## 18. 接口层与平台实现

**Interfaces/** (ITrayService IFileDialog IClipboard IToastService IMessageBox
IShellService IStartupService IThemeService ISingleInstance ISystemInfo)

**Platform/** (Native*/Null* pairs: FileDialogImpl, ClipboardImpl, ToastImpl,
MessageBoxImpl, ShellImpl, SingleInstanceImpl)

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
| Window (position/topmost) | OK | OK | - |
| Window (min/max/opacity) | OK | OK | - |
| Startup | OK | OK | OK |
| SystemInfo | OK | OK | OK |
| Toast | OK | OK | OK |
| JumpList | OK | - | - |
| Admin | OK | OK | OK |
| Theme | OK | OK | OK |
| ProcessHelper | OK | OK | OK |

---

## 架构说明

```
Assets/Plugins/
├── NativePlatform.cs          <- unified facade (recommended entry)
├── Interfaces/                <- platform-agnostic interfaces
│   └── ITrayService, IFileDialog, IClipboard, IToastService,
│       IMessageBox, IShellService, IStartupService,
│       IThemeService, ISingleInstance, ISystemInfo
├── Platform/                  <- Native* + Null* implementation pairs
│   └── FileDialogImpl, ClipboardImpl, ToastImpl,
│       MessageBoxImpl, ShellImpl, SingleInstanceImpl
├── ProcessHelper.cs           <- async stdout/stderr, no deadlock
├── TrayIconService.cs         <- tray (Win/Mac #if)
├── WindowsFileDialog.cs       <- dialogs (Win/Mac stdin/Linux zenity)
├── WindowsHotkey.cs           <- hotkey + full VK table
├── WindowsSingleInstance.cs   <- mutex / lockfile
├── WindowsToast.cs            <- inline PS/osascript/notify-send
├── WindowsWindow.cs           <- window control (Win/Mac)
├── MacWindowPlugin.cs         <- Mac plugin wrapper
└── MacOS/MacTray/
    ├── MacTray.h              <- ObjC header (Minimize/Maximize/SetAlpha)
    └── MacTray.m              <- ObjC implementation
```

### 设计原则
1. **接口驱动**：业务代码依赖 `I*` 接口，便于测试和替换
2. **空对象模式**：不支持平台返回 `Null*`，不抛异常
3. **门面模式**：`NativePlatform` 统一入口，屏蔽平台差异
4. **零死锁**：`ProcessHelper.RunAndRead` 异步事件读取 stdout/stderr
5. **无临时文件**：Toast/FileDialog 均内联命令执行

### 注意事项
1. `TrayIconService.Initialize()` 必须在 Unity 主线程调用
2. Mac 托盘和窗口功能依赖编译好的 `MacTray.bundle`
3. Linux 依赖：`zenity` `notify-send` `xdg-open` `gio` `systemd-inhibit`
4. 文件对话框会阻塞主线程直到用户操作完成
5. 不支持的平台返回默认值(false/null/0)并输出 LogWarning
"""

with open(r'd:\UniToolGUI\Assets\Plugins\API.md', 'a', encoding='utf-8') as f:
    f.write(append_text)
print('done, appended', len(append_text), 'chars')
