# UniToolGUI Plugins API 使用文档

> 跨平台系统集成 API，支持 Windows / macOS / Linux。所有 API 均为静态调用，无需实例化。

---

## 目录

- [1. 系统托盘 (TrayIconService)](#1-系统托盘-trayiconservice)
- [2. 文件对话框 (WindowsFileDialog)](#2-文件对话框-windowsfiledialog)
- [3. 消息框 (WindowsMessageBox)](#3-消息框-windowsmessagebox)
- [4. 剪贴板 (WindowsClipboard)](#4-剪贴板-windowsclipboard)
- [5. Shell 操作 (WindowsShell)](#5-shell-操作-windowsshell)
- [6. 单实例 (WindowsSingleInstance)](#6-单实例-windowssingleinstance)
- [7. 全局热键 (WindowsHotkey)](#7-全局热键-windowshotkey)
- [8. 电源控制 (WindowsPower)](#8-电源控制-windowspower)
- [9. 回收站 (WindowsRecycleBin)](#9-回收站-windowsrecyclebin)
- [10. 窗口控制 (WindowsWindow)](#10-窗口控制-windowswindow)
- [11. 开机自启 (WindowsStartup)](#11-开机自启-windowsstartup)
- [12. 系统信息 (WindowsSystemInfo)](#12-系统信息-windowssysteminfo)
- [13. Toast 通知 (WindowsToast)](#13-toast-通知-windowstoast)
- [14. Jump List (WindowsJumpList)](#14-jump-list-windowsjumplist)
- [15. 管理员检测 (WindowsAdmin)](#15-管理员检测-windowsadmin)
- [16. 主题与DPI (WindowsTheme)](#16-主题与dpi-windowstheme)
- [17. 进程工具 (ProcessHelper)](#17-进程工具-processhelper)
- [平台支持矩阵](#平台支持矩阵)

---

## 1. 系统托盘 (TrayIconService)

**平台**：Win ✅ | Mac ✅ | Linux ❌

线程安全单例，管理系统托盘图标、右键菜单、气泡通知。

### API

```csharp
// 获取单例
TrayIconService.Instance

// 生命周期
void Initialize()                           // 初始化托盘（必须在主线程调用）
void Shutdown()                             // 关闭托盘，释放资源

// 配置
void SetTooltip(string tooltip)             // 设置悬停提示
void RegisterMenuItems(IEnumerable<TrayMenuItem> items)   // 注册菜单项
void UnregisterMenuItems(IEnumerable<TrayMenuItem> items)  // 移除菜单项
void ClearMenuItems()                       // 清空所有菜单
void RefreshMenu()                          // 刷新菜单（动态文本更新后调用）

// 操作
void ShowMainWindow()                       // 显示并激活主窗口
void ShowBalloonTip(string title, string message, uint iconType = 1, uint timeoutMs = 5000)

// 事件
event Action OnTrayIconCreated
event Action OnTrayIconDestroyed
event Action<int> OnHotkeyPressed           // 仅 Windows
```

### TrayMenuItem 属性

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Text` | `string` | null | 菜单文本（分隔符时忽略） |
| `Callback` | `Action` | null | 点击回调（主线程执行） |
| `IsSeparator` | `bool` | false | 是否为分隔线 |
| `IsToggle` | `bool` | false | 是否可勾选 |
| `Checked` | `bool` | false | 当前勾选状态 |

### 示例

```csharp
// 初始化
TrayIconService.Instance.Initialize();
TrayIconService.Instance.SetTooltip("我的应用 v1.0");

// 注册菜单
TrayIconService.Instance.RegisterMenuItems(new[]
{
    new TrayMenuItem { Text = "显示窗口", Callback = () => TrayIconService.Instance.ShowMainWindow() },
    new TrayMenuItem { IsSeparator = true },
    new TrayMenuItem { Text = "开启功能", IsToggle = true, Checked = false,
        Callback = () => Debug.Log("功能已切换") },
    new TrayMenuItem { IsSeparator = true },
    new TrayMenuItem { Text = "退出", Callback = () => Application.Quit() }
});

// 气泡通知
TrayIconService.Instance.ShowBalloonTip("提醒", "后台任务已完成");

// 退出时清理
TrayIconService.Instance.Shutdown();
```

---

## 2. 文件对话框 (WindowsFileDialog)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
// 构造过滤器（成对：描述, 模式）
string CreateFilter(params string[] filterPairs)

// 单文件选择，返回路径或 null
string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)

// 多文件选择，返回路径数组或 null
string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)

// 保存文件对话框，返回路径或 null
string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)

// 文件夹选择，返回路径或 null
string OpenFolderPanel(string title, string initialDir = null)
```

### 示例

```csharp
// 构造过滤器
var filter = WindowsFileDialog.CreateFilter(
    "图片文件(*.png;*.jpg)", "*.png;*.jpg",
    "所有文件(*.*)", "*.*"
);

// 单文件
string file = WindowsFileDialog.OpenFilePanel("选择图片", filter, "png", @"C:\Pictures");
if (file != null) Debug.Log($"选中: {file}");

// 多文件
string[] files = WindowsFileDialog.OpenFilePanelMulti("选择多个文件", filter);
if (files != null) foreach (var f in files) Debug.Log(f);

// 保存
string savePath = WindowsFileDialog.SaveFilePanel("保存文件", filter, "png", null, "screenshot");
if (savePath != null) Debug.Log($"保存到: {savePath}");

// 文件夹
string folder = WindowsFileDialog.OpenFolderPanel("选择输出目录");
```

---

## 3. 消息框 (WindowsMessageBox)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
void Info(string text, string caption = "信息")
void Warning(string text, string caption = "警告")
void Error(string text, string caption = "错误")
bool Confirm(string text, string caption = "确认")       // true = OK
bool YesNo(string text, string caption = "请选择")        // true = Yes

// 完整版（可指定按钮+图标+父窗口）
int Show(IntPtr hWnd, string text, string caption, int buttons = 0, int icon = 0)
```

### 示例

```csharp
WindowsMessageBox.Info("操作完成！");
WindowsMessageBox.Warning("磁盘空间不足");
WindowsMessageBox.Error("无法连接服务器");

if (WindowsMessageBox.Confirm("确定要删除吗？"))
    Debug.Log("用户点击了确定");

if (WindowsMessageBox.YesNo("是否保存更改？"))
    SaveChanges();
```

---

## 4. 剪贴板 (WindowsClipboard)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
string GetText()              // 获取剪贴板文本，失败返回 null
bool SetText(string text)     // 设置剪贴板文本
bool HasText()                // 检查是否包含文本
```

### 示例

```csharp
WindowsClipboard.SetText("Hello World");
if (WindowsClipboard.HasText())
    Debug.Log(WindowsClipboard.GetText());
```

---

## 5. Shell 操作 (WindowsShell)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
bool OpenUrl(string url)                                    // 打开 URL
bool OpenFile(string filePath)                              // 用默认程序打开文件
bool OpenFolder(string folderPath, string filePath = null)  // 打开文件夹（可选中文件）
bool OpenFolderInExplorer(string folderPath)                // 打开文件夹
bool Execute(string operation, string filePath, string parameters = null, string workingDir = null)
```

### 示例

```csharp
WindowsShell.OpenUrl("https://unity.com");
WindowsShell.OpenFile(@"C:\report.pdf");
WindowsShell.OpenFolder(@"C:\Users", @"C:\Users\test.txt");  // 打开并选中
WindowsShell.Execute("runas", "cmd.exe", "/k echo hello");   // 仅 Win
```

---

## 6. 单实例 (WindowsSingleInstance)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
bool TryAcquire(string mutexName = null)              // 获取锁，false=已有实例
void Release()                                        // 释放锁
bool IsAnotherInstanceRunning(string mutexName = null) // 仅检测，不持锁
```

### 示例

```csharp
// 在 Awake 中检测
if (!WindowsSingleInstance.TryAcquire("MyUniqueApp"))
{
    Debug.LogWarning("已有实例在运行");
    Application.Quit();
    return;
}

// 退出时释放
void OnApplicationQuit()
{
    WindowsSingleInstance.Release();
}
```

---

## 7. 全局热键 (WindowsHotkey)

**平台**：Win ✅ | Mac ❌ | Linux ❌

### API

```csharp
bool Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey)
bool Unregister(IntPtr hwnd, int id)
IntPtr GetUnityWindowHandle()

// 修饰键枚举
enum ModifierKeys { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

// 常用虚拟键码
static class VK { F1..F12, Space, Escape }
```

### 示例

```csharp
var hwnd = WindowsHotkey.GetUnityWindowHandle();

// 注册 Ctrl+Shift+F1（id=1）
WindowsHotkey.Register(hwnd, 1,
    WindowsHotkey.ModifierKeys.Ctrl | WindowsHotkey.ModifierKeys.Shift,
    WindowsHotkey.VK.F1);

// 监听热键（在 TrayIconService 事件中）
TrayIconService.Instance.OnHotkeyPressed += (id) =>
{
    if (id == 1) Debug.Log("Ctrl+Shift+F1 按下！");
};

// 退出时注销
WindowsHotkey.Unregister(hwnd, 1);
```

---

## 8. 电源控制 (WindowsPower)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
void PreventSleep()            // 防止休眠（显示器可关）
void PreventSleepAndDisplay()  // 防止休眠 + 保持显示器
void AllowSleep()              // 恢复默认
void ResetIdleTimer()          // 临时重置空闲计时器
```

### 示例

```csharp
// 长任务期间防止休眠
WindowsPower.PreventSleep();
await LongRunningTask();
WindowsPower.AllowSleep();

// 视频播放期间保持屏幕
WindowsPower.PreventSleepAndDisplay();
```

---

## 9. 回收站 (WindowsRecycleBin)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
int MoveToRecycleBin(string path, bool showConfirm = true)  // 0=成功
int MoveToRecycleBinSilent(string path)                      // 静默模式
```

> 支持多路径：路径用 `\0` 分隔（如 `"file1.txt\0file2.txt"`）

### 示例

```csharp
int result = WindowsRecycleBin.MoveToRecycleBin(@"C:\temp\old.log");
if (result == 0) Debug.Log("已移至回收站");

// 多文件静默删除
WindowsRecycleBin.MoveToRecycleBinSilent(@"C:\a.txt" + "\0" + @"C:\b.txt");
```

---

## 10. 窗口控制 (WindowsWindow)

**平台**：Win ✅ | Mac ✅（部分） | Linux ❌

### API

```csharp
// 需要窗口句柄的方法（Win）
IntPtr GetUnityWindowHandle()
bool SetTopMost(IntPtr hwnd, bool topMost)
bool SetPositionAndSize(IntPtr hwnd, int x, int y, int width, int height)
bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int width, out int height)
bool Minimize(IntPtr hwnd)                    // 仅 Win
bool Maximize(IntPtr hwnd)                    // 仅 Win
bool Restore(IntPtr hwnd)                     // 仅 Win
bool IsMinimized(IntPtr hwnd)                 // 仅 Win
bool IsMaximized(IntPtr hwnd)                 // 仅 Win
bool SetOpacity(IntPtr hwnd, float opacity)   // 仅 Win（0.0~1.0）

// Unity 主窗口便捷方法（无需句柄）
bool SetUnityWindowTopMost(bool topMost)
bool GetUnityWindowRect(out int x, out int y, out int width, out int height)
bool SetUnityWindowRect(int x, int y, int width, int height)
bool MinimizeUnityWindow()                    // 仅 Win
bool MaximizeUnityWindow()                    // 仅 Win
bool RestoreUnityWindow()                     // 仅 Win
bool SetUnityWindowOpacity(float opacity)     // 仅 Win
```

### 示例

```csharp
// 置顶
WindowsWindow.SetUnityWindowTopMost(true);

// 移动到左上角 100,100，大小 800x600
WindowsWindow.SetUnityWindowRect(100, 100, 800, 600);

// 读取位置
if (WindowsWindow.GetUnityWindowRect(out int x, out int y, out int w, out int h))
    Debug.Log($"窗口: {x},{y} {w}x{h}");

// 最小化/最大化/还原
WindowsWindow.MinimizeUnityWindow();
WindowsWindow.MaximizeUnityWindow();
WindowsWindow.RestoreUnityWindow();

// 半透明
WindowsWindow.SetUnityWindowOpacity(0.8f);
```

---

## 11. 开机自启 (WindowsStartup)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
string GetStartupKeyName()                                    // 获取启动项名
bool IsStartupEnabled()                                       // 是否已设为自启
bool EnableStartup(string exePath = null, string keyName = null)  // 添加自启
bool DisableStartup(string keyName = null)                    // 移除自启
bool ToggleStartup(string exePath = null)                     // 切换状态
```

### 示例

```csharp
// 检查状态
bool enabled = WindowsStartup.IsStartupEnabled();

// 一键切换
WindowsStartup.ToggleStartup();

// 指定路径
WindowsStartup.EnableStartup(@"C:\MyApp\app.exe");
```

---

## 12. 系统信息 (WindowsSystemInfo)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
(int width, int height) GetPrimaryScreenSize()                      // 主屏分辨率
(int width, int height) GetVirtualScreenSize()                      // 虚拟屏幕（多显示器）
(bool onAC, int percent, int remainingSeconds) GetBatteryInfo()     // 电池信息
uint GetIdleTimeSeconds()                                           // 空闲秒数
string GetComputerName()                                            // 计算机名
string GetUserName()                                                // 用户名
```

### 示例

```csharp
var (w, h) = WindowsSystemInfo.GetPrimaryScreenSize();
Debug.Log($"屏幕: {w}x{h}");

var (onAC, percent, remaining) = WindowsSystemInfo.GetBatteryInfo();
Debug.Log($"电源: {(onAC ? "AC" : "电池")} {percent}%");

uint idle = WindowsSystemInfo.GetIdleTimeSeconds();
if (idle > 300) Debug.Log("用户已离开 5 分钟");
```

---

## 13. Toast 通知 (WindowsToast)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
bool Show(string title, string message, string imagePath = null)
```

> Linux 支持 `imagePath`（通过 `notify-send -i`），Mac 暂不支持图片。

### 示例

```csharp
WindowsToast.Show("下载完成", "文件已保存到桌面");
WindowsToast.Show("截图", "已保存截图", @"C:\screenshot.png");  // Win/Linux 带图
```

---

## 14. Jump List (WindowsJumpList)

**平台**：Win ✅ | Mac ❌ | Linux ❌

### API

```csharp
void AddToRecentDocs(string filePath)   // 添加到最近文档
void ClearRecentDocs()                  // 清除最近文档（全局）
```

### 示例

```csharp
WindowsJumpList.AddToRecentDocs(@"C:\Documents\report.docx");
```

---

## 15. 管理员检测 (WindowsAdmin)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
bool IsRunningAsAdmin()              // 是否管理员/root
bool RestartAsAdmin(string args = null)  // 提升权限重启（Win: UAC, Linux: pkexec）
```

### 示例

```csharp
if (!WindowsAdmin.IsRunningAsAdmin())
{
    Debug.Log("需要管理员权限");
    WindowsAdmin.RestartAsAdmin("--elevated");
}
```

---

## 16. 主题与DPI (WindowsTheme)

**平台**：Win ✅ | Mac ✅ | Linux ✅

### API

```csharp
bool IsDarkMode()                          // 是否深色模式
float GetDpiScale()                        // DPI 缩放（1.0=100%）
float GetDpiScaleForWindow(IntPtr hwnd)    // 指定窗口的 DPI 缩放（仅 Win）
Color32 GetAccentColor()                   // 系统强调色
```

### 示例

```csharp
// 适配深色模式
if (WindowsTheme.IsDarkMode())
    ApplyDarkTheme();
else
    ApplyLightTheme();

// DPI 感知布局
float scale = WindowsTheme.GetDpiScale();
Debug.Log($"DPI 缩放: {scale * 100}%");

// 读取系统强调色
Color32 accent = WindowsTheme.GetAccentColor();
myButton.color = accent;
```

---

## 17. 进程工具 (ProcessHelper)

**平台**：Win ✅ | Mac ✅ | Linux ✅

供内部使用，也可直接调用。

### API

```csharp
int Run(string fileName, string arguments, int timeoutMs = 5000)       // 运行并等待，返回退出码
string RunAndRead(string fileName, string arguments, int timeoutMs = 30000)  // 运行并读取输出
Process StartBackground(string fileName, string arguments)              // 后台启动
string Quote(string arg)                                                // 转义并加引号
```

### 示例

```csharp
// 执行命令
int exitCode = ProcessHelper.Run("git", "status", 10000);

// 读取输出
string result = ProcessHelper.RunAndRead("python", "--version");
Debug.Log(result);  // "Python 3.11.0"

// 后台运行
var proc = ProcessHelper.StartBackground("ffmpeg", "-i input.mp4 output.avi");
```

---

## 平台支持矩阵

| 模块 | Win | Mac | Linux |
|------|:---:|:---:|:-----:|
| TrayIconService | ✅ | ✅ | ❌ |
| WindowsFileDialog | ✅ | ✅ | ✅ |
| WindowsMessageBox | ✅ | ✅ | ✅ |
| WindowsClipboard | ✅ | ✅ | ✅ |
| WindowsShell | ✅ | ✅ | ✅ |
| WindowsSingleInstance | ✅ | ✅ | ✅ |
| WindowsHotkey | ✅ | ❌ | ❌ |
| WindowsPower | ✅ | ✅ | ✅ |
| WindowsRecycleBin | ✅ | ✅ | ✅ |
| WindowsWindow（置顶/位置） | ✅ | ✅ | ❌ |
| WindowsWindow（最小化/最大化/透明度） | ✅ | ❌ | ❌ |
| WindowsStartup | ✅ | ✅ | ✅ |
| WindowsSystemInfo | ✅ | ✅ | ✅ |
| WindowsToast | ✅ | ✅ | ✅ |
| WindowsJumpList | ✅ | ❌ | ❌ |
| WindowsAdmin | ✅ | ✅ | ✅ |
| WindowsTheme | ✅ | ✅ | ✅ |
| ProcessHelper | ✅ | ✅ | ✅ |

---

## 注意事项

1. **主线程调用**：`TrayIconService.Initialize()` 必须在 Unity 主线程调用
2. **Mac 原生插件**：托盘和窗口功能依赖 `MacTray.bundle`，需在 Mac 上编译
3. **Linux 依赖**：`zenity`、`notify-send`、`xdg-open`、`gio`、`systemd-inhibit` 需确保已安装
4. **文件对话框阻塞**：`OpenFilePanel`/`SaveFilePanel` 等会阻塞主线程直到用户操作完成
5. **非支持平台**：不支持的平台不会抛异常，而是返回默认值（false/null/0）或输出 `Debug.LogWarning`
