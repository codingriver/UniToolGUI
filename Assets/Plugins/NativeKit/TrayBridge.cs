using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class TrayMenuButton
{
    [Tooltip("菜单项显示文字")] public string Label = "操作";
    [Tooltip("是否先显示主窗口")] public bool ShowWindowFirst = false;
    [Tooltip("是否为分割线")] public bool IsSeparator = false;
    [Tooltip("点击回调")]
    public UnityEvent OnClick = new UnityEvent();
}

public class TrayBridge : MonoBehaviour
{
    [Header("基础配置")] [Tooltip("托盘图标悬停提示文字")]
    public string Tooltip = "Unity App";
    [Tooltip("最小化时自动隐藏到托盘")] public bool MinimizeToTray = true;
    [Tooltip("在 Editor 中也启用真实托盘")] public bool SimulateInEditor = true;

    [Header("图标")] [Tooltip("Windows: StreamingAssets 下的 .ico 文件名")]
    public string WindowsIcoFileName = "app.ico";
    [Tooltip("macOS: StreamingAssets 下的 .png 图标文件名")] public string MacPngFileName = "app.png";

    [Header("内置菜单项")]
    public bool AddShowWindowItem = true;
    public bool AddStartupItem = true;
    public bool AddRestartItem = true;
    public bool AddQuitItem = true;

    [Header("自定义菜单按鈕")]
    public TrayMenuButton[] CustomButtons = new TrayMenuButton[0];

    [Header("事件")]
    public UnityEvent OnShowMainWindow = new UnityEvent();
    public UnityEvent OnTrayQuit = new UnityEvent();
    public UnityEvent OnTrayReady = new UnityEvent();

    public static event Action ShowMainWindowRequested;
    public static event Action QuitRequested;
    public static event Action TrayReady;

    public static bool IsInitialized { get; private set; }
    private bool _initialized;
    private SynchronizationContext _mainCtx;
    private readonly List<TrayMenuItem> _extraItems = new List<TrayMenuItem>();
    private TrayMenuItem _menuStartup;

    private void Start()
    {
        if (Application.isEditor && !SimulateInEditor)
        {
            Debug.Log("[TrayBridge] Editor 模式下跳过托盘初始化");
            return;
        }
        _mainCtx = SynchronizationContext.Current ?? new SynchronizationContext();
        WindowsStartup.OnStartupChanged += OnStartupChangedExternal;
        InitTray();
    }

    private void OnDestroy()
    {
        WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
        ShutdownTray();
    }

    private void OnApplicationQuit()
    {
        WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
        ShutdownTray();
    }

    private void OnStartupChangedExternal(bool enabled)
    {
        if (_menuStartup == null) return;
        _menuStartup.Checked = enabled;
        if (_initialized) NativePlatform.Tray.RefreshMenu();
    }

    public void RegisterExtraMenuItems(TrayMenuItem[] items)
    {
        _extraItems.Clear();
        if (items != null)
            foreach (var item in items)
                _extraItems.Add(item);
        if (_initialized) RebuildMenu();
    }

    public void RefreshMenu()
    {
        if (!_initialized) return;
        NativePlatform.Tray.RefreshMenu();
    }

    public void ShowBalloonTip(string title, string message, uint iconType = 1, uint timeoutMs = 4000)
    {
        if (!_initialized) return;
        NativePlatform.Tray.ShowBalloonTip(title, message, iconType, timeoutMs);
    }

    public void SetTooltip(string tooltip)
    {
        Tooltip = tooltip;
        if (_initialized) NativePlatform.Tray.SetTooltip(tooltip);
    }

    private void InitTray()
    {
        if (_initialized) return;
        try
        {
            var tray = NativePlatform.Tray;
            tray.Initialize();
            tray.SetTooltip(Tooltip);
            TrySetIcon();
            RebuildMenu();
            tray.OnTrayIconCreated += HandleTrayCreated;
            HandleTrayCreated();
            _initialized = true;
            IsInitialized = true;
            Debug.Log("[TrayBridge] 托盘初始化完成");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TrayBridge] 托盘初始化失败，托盘功能已禁用: " + ex.Message);
            _initialized = false;
            IsInitialized = false;
        }
    }

    private void ShutdownTray()
    {
        if (!_initialized) return;
        NativePlatform.Tray.OnTrayIconCreated -= HandleTrayCreated;
        NativePlatform.Tray.Shutdown();
        _initialized = false;
        IsInitialized = false;
        Debug.Log("[TrayBridge] 托盘已关闭");
    }

    private void HandleTrayCreated()
    {
        OnTrayReady?.Invoke();
        TrayReady?.Invoke();
    }

    private void RebuildMenu()
    {
        NativePlatform.Tray.ClearMenuItems();
        var items = new List<TrayMenuItem>();

        if (AddShowWindowItem)
        {
            items.Add(new TrayMenuItem
            {
                Text = "显示主窗口",
                Callback = () =>
                {
                    NativePlatform.Tray.ShowMainWindow();
                    OnShowMainWindow?.Invoke();
                    ShowMainWindowRequested?.Invoke();
                }
            });
        }

        if (CustomButtons != null && CustomButtons.Length > 0)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            foreach (var btn in CustomButtons)
            {
                if (btn == null) continue;
                TrayMenuButton captured = btn;
                if (btn.IsSeparator) { items.Add(new TrayMenuItem { IsSeparator = true }); continue; }
                if (string.IsNullOrEmpty(btn.Label)) continue;
                items.Add(new TrayMenuItem
                {
                    Text = captured.Label.Trim(),
                    Callback = () => _mainCtx.Post(_ =>
                    {
                        try
                        {
                            if (captured.ShowWindowFirst) NativePlatform.Tray.ShowMainWindow();
                            captured.OnClick?.Invoke();
                        }
                        catch (Exception ex) { Debug.LogWarning("[TrayBridge] " + captured.Label + ": " + ex.Message); }
                    }, null)
                });
            }
        }

        if (_extraItems.Count > 0)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            items.AddRange(_extraItems);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX
        if (AddStartupItem)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            bool startupEnabled = false;
            try { startupEnabled = WindowsStartup.IsStartupEnabled(); }
            catch (Exception ex) { Debug.LogWarning("[TrayBridge] 开机自启检查失败: " + ex.Message); }

            _menuStartup = new TrayMenuItem
            {
                Text = "开机自启",
                IsToggle = true,
                Checked = startupEnabled,
                Callback = () => _mainCtx.Post(_ =>
                {
                    try
                    {
                        bool currentEnabled = WindowsStartup.IsStartupEnabled();
                        bool targetEnabled = !currentEnabled;

                        bool ok;
                        if (targetEnabled)
                        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                            ok = WindowsStartup.EnableStartup(WindowsStartup.GetCurrentExePath());
#elif UNITY_STANDALONE_OSX
                            var exePath = WindowsStartup.GetCurrentExePath();
                            if (string.IsNullOrEmpty(exePath))
                                throw new InvalidOperationException("无法获取 macOS 可执行文件路径");
                            ok = WindowsStartup.EnableStartup(exePath);
#else
                            ok = false;
#endif
                        }
                        else
                        {
                            ok = WindowsStartup.DisableStartup();
                        }

                        if (!ok)
                            throw new InvalidOperationException("开机自启切换失败");

                        _menuStartup.Checked = WindowsStartup.IsStartupEnabled();
                        NativePlatform.Tray.RefreshMenu();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TrayBridge] 开机自启切换失败: " + ex.Message);
                        _menuStartup.Checked = WindowsStartup.IsStartupEnabled();
                        NativePlatform.Tray.RefreshMenu();
                    }
                }, null)
            };
            items.Add(_menuStartup);
        }
#endif

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX
        if (AddRestartItem)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });

            items.Add(new TrayMenuItem
            {
                Text = "重启程序",
                Callback = () => _mainCtx.Post(_ =>
                {
                    try
                    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                        var exe = WindowsStartup.GetCurrentExePath();
                        if (!string.IsNullOrEmpty(exe))
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = true });
#elif UNITY_STANDALONE_OSX
                        var appPath = GetMacAppBundlePath();
                        if (!string.IsNullOrEmpty(appPath))
                        {
                            // 先释放单实例文件锁，再用 bash 延迟 1 秒启动新实例，最后退出。
                            // 若先 Quit 再启动，文件锁在进程退出时由 OS 回收，但时序不确定；
                            // 若先 open 再 Quit，新实例可能在旧锁释放前启动并因 TryAcquire 失败被 kill。
                            // 正确做法：主动 Release 锁 → 后台延迟启动 → 当前进程 Quit。
                            NativePlatform.SingleInstance.Release();
                            // 用绝对路径 /usr/bin/open，避免打包后 $PATH 不完整导致找不到 open
                            string escapedPath = appPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
                            ProcessHelper.StartBackground("/bin/bash",
                                "-c \"/bin/sleep 1 && /usr/bin/open \\\"" + escapedPath + "\\\"\"");
                        }
                        else
                            Debug.LogWarning("[TrayBridge] 未找到 .app 包路径，无法重启 macOS 应用");
#endif
                        ShutdownTray();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    }
                    catch (Exception ex) { Debug.LogWarning("[TrayBridge] 重启失败: " + ex.Message); }
                }, null)
            });
        }
#endif

        if (AddQuitItem)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            items.Add(new TrayMenuItem
            {
                Text = "退出",
                Callback = () =>
                {
                    Debug.Log("[Tray] 点击退出");
                    var confirm = WindowsMessageBox.Confirm("确定要退出程序吗？", "确认退出");
                    if (confirm)
                    {
                        OnTrayQuit?.Invoke();
                        QuitRequested?.Invoke();
                        ShutdownTray();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    }
                }
            });
        }

        if (items.Count > 0)
            NativePlatform.Tray.RegisterMenuItems(items);
    }

    private string GetMacAppBundlePath()
    {
#if UNITY_STANDALONE_OSX
        try
        {
            // Application.dataPath = .../Foo.app/Contents/Resources/Data
            // 需要向上 3 级才到 .app
            var dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath)) return null;

            // 向上逐级查找以 .app 结尾的目录
            var dir = dataPath;
            for (int i = 0; i < 5; i++)
            {
                dir = System.IO.Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir)) break;
                if (dir.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("[TrayBridge] .app 路径: " + dir);
                    return dir;
                }
            }
            Debug.LogWarning("[TrayBridge] 未能从 dataPath 向上找到 .app 目录，dataPath=" + dataPath);
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TrayBridge] 获取 macOS .app 路径失败: " + ex.Message);
            return null;
        }
#else
        return null;
#endif
    }

    private void TrySetIcon()
    {
        // ════════════════════════════════════════════════════════════════════
        // 【托盘图标加载注意事项】
        //
        // ★ 文件格式要求（最重要）
        //   Windows 托盘图标必须是真正的 ICO 格式（BMP DIB 内嵌），
        //   不能将 PNG/JPG 直接改扩展名为 .ico。
        //   Win32 LoadImage 对格式严格校验：
        //     - PNG 改名为 .ico → LoadImage 返回 IntPtr.Zero，错误码 0
        //     - PNG 压缩内嵌 ICO 容器 → 部分 Windows 版本/API 调用不支持
        //     - BMP DIB 格式内嵌 ICO → 全平台兼容，推荐
        //
        // ★ 生成方式
        //   使用 D:\UniToolGUI\Assets\icon.png（2048×2048 原始图）
        //   通过 C# + System.Drawing 生成 BMP DIB 格式 ICO：
        //     - 包含 16×16 / 32×32 / 48×48 三个尺寸
        //     - 每个尺寸用 GetPixel 逐像素提取，写入 BITMAPINFOHEADER
        //     - 包含 XOR mask（32bpp BGRA）+ AND mask（1bpp 全零）
        //   生成后文件约 15086 bytes，Win32 LoadImage 验证通过。
        //
        // ★ 文件放置位置
        //   StreamingAssets/app.ico
        //   Editor 下路径为: Application.streamingAssetsPath/app.ico
        //   打包后路径为:    <ExeDir>/<AppName>_Data/StreamingAssets/app.ico
        //
        // ★ 路径格式
        //   Win32 LoadImage 要求纯反斜杠路径。
        //   Application.streamingAssetsPath 在 Windows Editor 下返回带
        //   正斜杠的路径（如 D:/UniToolGUI/...），Path.Combine 拼接后
        //   可能混用分隔符（D:/UniToolGUI/Assets/StreamingAssets\app.ico）。
        //   TrayIconService.PlatformSetIcon 已在内部调用
        //   iconPath.Replace('/', '\\') 统一替换，此处无需处理。
        //
        // ★ 错误码 0 的含义
        //   LoadImage 返回 IntPtr.Zero 且 GetLastError()=0，
        //   表示 Win32 系统调用本身无错误，但文件格式被拒绝。
        //   遇到此情况首先检查文件格式，而非路径或权限。
        //
        // ★ Inspector 字段说明
        //   WindowsIcoFileName: StreamingAssets 下的 .ico 文件名，默认 "app.ico"
        //   该字段仅填写文件名（不含路径），路径由代码自动拼接。
        // ════════════════════════════════════════════════════════════════════
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // ── [Tray][Icon] 加载前日志 ──
        string streamingPath = Application.streamingAssetsPath;
        Debug.Log(string.Format("[TrayBridge][Icon] StreamingAssetsPath = {0}", streamingPath));
        if (string.IsNullOrEmpty(WindowsIcoFileName))
        {
            Debug.LogWarning("[TrayBridge][Icon] WindowsIcoFileName 为空，跳过图标加载");
            return;
        }
        string icoPath = System.IO.Path.Combine(streamingPath, WindowsIcoFileName);
        Debug.Log(string.Format("[TrayBridge][Icon] 尝试加载 Windows 托盘图标，路径: {0}", icoPath));
        bool exists = System.IO.File.Exists(icoPath);
        Debug.Log(string.Format("[TrayBridge][Icon] 图标文件是否存在: {0}", exists));
        if (!exists)
        {
            Debug.LogWarning(string.Format("[TrayBridge][Icon] 图标文件不存在: {0}  请将 {1} 放入 StreamingAssets 文件夹", icoPath, WindowsIcoFileName));
            return;
        }
        // 打印文件大小，辅助排查文件损坏
        try
        {
            long fileSize = new System.IO.FileInfo(icoPath).Length;
            Debug.Log(string.Format("[TrayBridge][Icon] 图标文件大小: {0} 字节", fileSize));
            if (fileSize < 64)
                Debug.LogWarning("[TrayBridge][Icon] 警告：图标文件过小，可能不是有效的 .ico 文件");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TrayBridge][Icon] 无法读取文件大小: " + ex.Message);
        }
        // ── 调用设置图标 ──
        Debug.Log("[TrayBridge][Icon] 正在调用 SetIcon，传入 .ico 路径...");
        TrayIconService.Instance.SetIcon(icoPath);
        // ── [Tray][Icon] 加载后日志（成功与否由 TrayIconService 内部日志报告）──
        Debug.Log("[TrayBridge][Icon] SetIcon 调用完成");

#elif UNITY_STANDALONE_OSX
        string streamingPathMac = Application.streamingAssetsPath;
        Debug.Log(string.Format("[TrayBridge][Icon] StreamingAssetsPath = {0}", streamingPathMac));
        if (string.IsNullOrEmpty(MacPngFileName))
        {
            Debug.LogWarning("[TrayBridge][Icon] MacPngFileName 为空，跳过图标加载");
            return;
        }
        string pngPath = System.IO.Path.Combine(streamingPathMac, MacPngFileName);
        Debug.Log(string.Format("[TrayBridge][Icon] 尝试加载 macOS 托盘图标，路径: {0}", pngPath));
        bool pngExists = System.IO.File.Exists(pngPath);
        Debug.Log(string.Format("[TrayBridge][Icon] macOS 图标文件是否存在: {0}", pngExists));
        if (!pngExists)
        {
            Debug.LogWarning(string.Format("[TrayBridge][Icon] macOS 图标文件不存在: {0}", pngPath));
            return;
        }
        Debug.Log("[TrayBridge][Icon] 正在调用 macOS SetIcon...");
        TrayIconService.Instance.SetIcon(pngPath);
        Debug.Log("[TrayBridge][Icon] macOS SetIcon 调用完成");
#endif
    }
}
