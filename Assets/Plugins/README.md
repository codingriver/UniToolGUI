# Plugins 功能文档

本目录提供 Unity 应用的跨平台系统集成能力（Win/Mac/Linux），包括系统托盘、文件对话框、消息框、剪贴板、Shell 操作、单实例、热键、电源控制、回收站、窗口控制、开机自启、系统信息、Toast 通知、Jump List、管理员检测、主题/DPI 检测等。

---

## 一、模块概览

| 文件 | 功能 | Win | Mac | Linux |
|------|------|:---:|:---:|:-----:|
| `TrayIconService.cs` | 系统托盘（含气泡提示） | ✅ | ✅ | - |
| `TrayMenuItem` | 托盘菜单项数据模型 | ✅ | ✅ | ✅ |
| `TrayIconTest.cs` | 托盘演示组件 | ✅ | ✅ | - |
| `MacTrayPlugin.cs` | Mac 托盘原生封装 | - | ✅ | - |
| `MacWindowPlugin.cs` | Mac 窗口控制原生封装 | - | ✅ | - |
| `WindowsFileDialog.cs` | 文件/文件夹选择 | ✅ | ✅ | ✅ |
| `WindowsMessageBox.cs` | 原生消息框 | ✅ | ✅ | ✅ |
| `WindowsClipboard.cs` | 剪贴板 | ✅ | ✅ | ✅ |
| `WindowsShell.cs` | 打开 URL/文件/文件夹 | ✅ | ✅ | ✅ |
| `WindowsSingleInstance.cs` | 单实例检测 | ✅ | ✅ | ✅ |
| `WindowsHotkey.cs` | 全局热键 | ✅ | - | - |
| `WindowsPower.cs` | 防止休眠 | ✅ | ✅ | ✅ |
| `WindowsRecycleBin.cs` | 移至回收站 | ✅ | ✅ | ✅ |
| `WindowsWindow.cs` | 窗口置顶/位置/大小/最小化/最大化/透明度 | ✅ | ✅ | - |
| `WindowsStartup.cs` | 开机自启 | ✅ | ✅ | ✅ |
| `WindowsSystemInfo.cs` | 系统信息（屏幕/电池/空闲时间） | ✅ | ✅ | ✅ |
| `WindowsToast.cs` | 系统通知 | ✅ | ✅ | ✅ |
| `WindowsJumpList.cs` | Jump List 最近文档 | ✅ | - | - |
| `WindowsAdmin.cs` | 管理员/root 检测 | ✅ | ✅ | ✅ |
| `WindowsTheme.cs` | 深色模式/DPI/强调色检测 | ✅ | ✅ | ✅ |
| `ProcessHelper.cs` | 跨平台进程调用工具类 | ✅ | ✅ | ✅ |

---

## 二、功能与平台支持对照表

| 功能 | Windows | Mac | Linux | 说明 |
|------|:-------:|:---:|:-----:|------|
| 系统托盘 | ✅ | ✅ | - | Win: Shell_NotifyIcon；Mac: MacTray.bundle (NSStatusItem) |
| 托盘气泡提示 | ✅ | ✅ | - | Win: NOTIFYICONDATA；Mac: UNUserNotificationCenter (10.14+) / NSUserNotification |
| 文件选择 | ✅ | ✅ | ✅ | Win: GetOpenFileName；Mac: osascript choose file；Linux: zenity |
| 多文件选择 | ✅ | ✅ | ✅ | OFN_ALLOWMULTISELECT / choose file multiple / zenity --multiple |
| 保存文件 | ✅ | ✅ | ✅ | Win: GetSaveFileName；Mac: choose file name；Linux: zenity --save |
| 文件夹选择 | ✅ | ✅ | ✅ | Win: SHBrowseForFolder；Mac: choose folder；Linux: zenity --directory |
| 原生消息框 | ✅ | ✅ | ✅ | Win: MessageBox；Mac: osascript(临时文件)；Linux: zenity |
| 剪贴板 | ✅ | ✅ | ✅ | Win: user32；Mac/Linux: GUIUtility.systemCopyBuffer |
| Shell 打开 | ✅ | ✅ | ✅ | Win: ShellExecute；Mac: open；Linux: xdg-open |
| 单实例 | ✅ | ✅ | ✅ | Win: Mutex+CloseHandle；Mac/Linux: 文件锁 |
| 全局热键 | ✅ | - | - | Win: RegisterHotKey |
| 防止休眠 | ✅ | ✅ | ✅ | Win: SetThreadExecutionState；Mac: caffeinate；Linux: systemd-inhibit |
| 回收站删除 | ✅ | ✅ | ✅ | Win: SHFileOperation；Mac: ~/.Trash；Linux: gio trash（支持多路径） |
| 窗口置顶 | ✅ | ✅ | - | Win: SetWindowPos；Mac: NSWindow.setLevel |
| 窗口位置/大小 | ✅ | ✅ | - | Win: GetWindowRect/SetWindowPos；Mac: NSWindow.frame |
| 最小化/最大化/还原 | ✅ | - | - | Win: ShowWindow |
| 窗口透明度 | ✅ | - | - | Win: SetLayeredWindowAttributes |
| 开机自启 | ✅ | ✅ | ✅ | Win: 注册表；Mac: LaunchAgents；Linux: autostart |
| 系统信息 | ✅ | ✅ | ✅ | Win: GetSystemMetrics；Mac: pmset/ioreg；Linux: /sys/class + xprintidle |
| Toast 通知 | ✅ | ✅ | ✅ | Win: PowerShell+WinRT；Mac: osascript；Linux: notify-send（支持图片） |
| Jump List | ✅ | - | - | Win: SHAddToRecentDocs |
| 管理员检测 | ✅ | ✅ | ✅ | Win: IsUserAnAdmin；Mac/Linux: getuid() |
| 深色模式检测 | ✅ | ✅ | ✅ | Win: 注册表；Mac: defaults；Linux: gsettings |
| DPI 缩放 | ✅ | ✅ | ✅ | Win: GetDpiForSystem；Mac: Screen.dpi；Linux: gsettings |
| 强调色 | ✅ | ✅ | ✅ | Win: DWM 注册表；Mac: AppleAccentColor；Linux: 固定默认值 |

**图例**：✅ 已实现 | - 未实现

---

## 三、TrayIconService（托盘图标服务）

### 3.1 功能描述

线程安全单例服务，负责在 **Windows** 和 **Mac** 系统托盘区域创建/管理图标，支持：

- **托盘图标**：显示在系统托盘，悬停显示提示文本
- **右键菜单**：可配置的弹出菜单（支持 Toggle 勾选项）
- **窗口行为**：最小化时隐藏窗口、点击关闭时隐藏而非退出
- **双击托盘图标**：恢复并显示主窗口
- **气泡通知**：Win 用 NOTIFYICONDATA，Mac 用 UNUserNotificationCenter

### 3.2 核心 API

| 方法 | 说明 |
|------|------|
| `Initialize()` | 初始化托盘，需在调用其他方法前执行（主线程） |
| `Shutdown()` | 关闭托盘并清理资源 |
| `SetTooltip(string)` | 设置托盘图标悬停提示文本 |
| `RegisterMenuItems(IEnumerable<TrayMenuItem>)` | 注册菜单项（支持批量） |
| `UnregisterMenuItems(IEnumerable<TrayMenuItem>)` | 取消注册菜单项 |
| `ClearMenuItems()` | 清空所有菜单项 |
| `ShowMainWindow()` | 显示并激活主窗口 |
| `RefreshMenu()` | 刷新菜单（用于动态更新菜单文本） |
| `ShowBalloonTip(title, message, iconType, timeoutMs)` | 显示托盘气泡提示 |

### 3.3 事件

| 事件 | 说明 |
|------|------|
| `OnTrayIconCreated` | 托盘图标创建完成时触发 |
| `OnTrayIconDestroyed` | 托盘图标销毁时触发 |
| `OnHotkeyPressed` | 全局热键按下时触发，参数为热键 ID（仅 Windows） |

### 3.4 使用流程

```csharp
// 1. 初始化（必须在主线程）
TrayIconService.Instance.Initialize();
TrayIconService.Instance.SetTooltip("我的应用");

// 2. 注册菜单
TrayIconService.Instance.RegisterMenuItems(new TrayMenuItem[]
{
    new TrayMenuItem { Text = "显示窗口", Callback = () => TrayIconService.Instance.ShowMainWindow() },
    new TrayMenuItem { IsSeparator = true },
    new TrayMenuItem { Text = "退出", Callback = () => Application.Quit() }
});

// 3. 退出时清理
TrayIconService.Instance.Shutdown();
```

---

## 四、TrayMenuItem（托盘菜单项）

| 属性 | 类型 | 说明 |
|------|------|------|
| `Text` | string | 菜单显示文本（分隔符时忽略） |
| `Callback` | Action | 点击回调，在主线程执行 |
| `IsSeparator` | bool | 是否为分隔线 |
| `IsToggle` | bool | 是否为可勾选菜单项 |
| `Checked` | bool | 勾选状态（仅当 IsToggle 为 true 时有效） |

---

## 五、WindowsFileDialog（文件/文件夹选择）

| 方法 | 说明 |
|------|------|
| `OpenFilePanel(title, filter, defaultExt, initialDir)` | 打开文件选择对话框 |
| `OpenFilePanelMulti(title, filter, defaultExt, initialDir)` | 打开多文件选择，返回 `string[]` |
| `SaveFilePanel(title, filter, defaultExt, initialDir, defaultFileName)` | 打开保存文件对话框 |
| `OpenFolderPanel(title, initialDir)` | 打开文件夹选择对话框 |
| `CreateFilter(params string[])` | 构造过滤器字符串（成对：描述\|模式） |

```csharp
var filter = WindowsFileDialog.CreateFilter("文本文件(*.txt)", "*.txt", "所有文件(*.*)", "*.*");
string path = WindowsFileDialog.OpenFilePanel("选择文件", filter, "txt", @"C:\");
string[] files = WindowsFileDialog.OpenFilePanelMulti("多选", filter);
string savePath = WindowsFileDialog.SaveFilePanel("保存", filter, "txt", null, "untitled");
```

---

## 六、WindowsMessageBox（原生消息框）

| 方法 | 说明 |
|------|------|
| `Info(text, caption)` | 信息框（OK） |
| `Warning(text, caption)` | 警告框（OK） |
| `Error(text, caption)` | 错误框（OK） |
| `Confirm(text, caption)` | 确认框（OK/Cancel），返回 bool |
| `YesNo(text, caption)` | 是/否框，返回 bool |

---

## 七、模块速查

| 模块 | 主要 API |
|------|----------|
| WindowsClipboard | `GetText()` / `SetText(string)` / `HasText()` |
| WindowsShell | `OpenUrl(url)` / `OpenFile(path)` / `OpenFolder(path, filePath)` / `OpenFolderInExplorer(path)` |
| WindowsSingleInstance | `TryAcquire(mutexName)` / `Release()` / `IsAnotherInstanceRunning(mutexName)` |
| WindowsHotkey | `Register(hwnd, id, modifiers, vk)` / `Unregister(hwnd, id)` |
| WindowsPower | `PreventSleep()` / `PreventSleepAndDisplay()` / `AllowSleep()` / `ResetIdleTimer()` |
| WindowsRecycleBin | `MoveToRecycleBin(path, showConfirm)` / `MoveToRecycleBinSilent(path)` |
| WindowsWindow | `SetTopMost` / `SetPositionAndSize` / `GetPositionAndSize` / `Minimize` / `Maximize` / `Restore` / `SetOpacity` |
| WindowsStartup | `IsStartupEnabled()` / `EnableStartup(exePath)` / `DisableStartup()` / `ToggleStartup()` |
| WindowsSystemInfo | `GetPrimaryScreenSize()` / `GetVirtualScreenSize()` / `GetBatteryInfo()` / `GetIdleTimeSeconds()` / `GetComputerName()` |
| WindowsToast | `Show(title, message, imagePath)` |
| WindowsJumpList | `AddToRecentDocs(filePath)` / `ClearRecentDocs()` |
| WindowsAdmin | `IsRunningAsAdmin()` / `RestartAsAdmin(args)` |
| WindowsTheme | `IsDarkMode()` / `GetDpiScale()` / `GetDpiScaleForWindow(hwnd)` / `GetAccentColor()` |
| ProcessHelper | `Run(cmd, args, timeout)` / `RunAndRead(cmd, args)` / `StartBackground(cmd, args)` / `Quote(arg)` |

---

## 八、平台与宏定义

| 宏 | 说明 |
|----|------|
| `UNITY_STANDALONE_WIN` | Windows 独立平台 |
| `UNITY_STANDALONE_OSX` | macOS 独立平台 |
| `UNITY_STANDALONE_LINUX` | Linux 独立平台 |
| `UNITY_EDITOR_WIN` | 在 Windows 上运行 Unity 编辑器 |

**Mac 原生插件**：托盘与窗口控制依赖 `MacTray.bundle`，需在 Mac 上执行 `Assets/Plugins/MacOS/MacTray/build.sh` 编译后放入 `Assets/Plugins/`。

**Mac/Linux 系统命令**：部分功能依赖系统命令，需确保已安装：
- **Mac**：`osascript`、`caffeinate`、`pmset`、`ioreg`（系统自带）
- **Linux**：`zenity`、`xdg-open`、`notify-send`、`gio`、`systemd-inhibit`、`xprintidle`（视发行版而定）

---

## 九、依赖关系

```
TrayIconTest ──依赖──> TrayIconService
                    └──> WindowsMessageBox

TrayIconService ──使用──> TrayMenuItem（数据模型）
                      ├──> Win32 API（user32.dll, shell32.dll）[Windows]
                      └──> MacTrayPlugin → MacTray.bundle [Mac]

WindowsWindow ──使用──> Win32 API（user32.dll）[Windows]
                    └──> MacWindowPlugin → MacTray.bundle [Mac]

WindowsMessageBox ─┐
WindowsFileDialog  ├──> ProcessHelper（Mac/Linux 进程调用）
WindowsToast       │
WindowsShell       │
WindowsRecycleBin  │
WindowsPower       │
WindowsSystemInfo  │
WindowsTheme       ┘
```
