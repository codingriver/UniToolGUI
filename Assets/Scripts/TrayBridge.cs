using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

// ---------------------------------------------------------------------------
// 可序列化的托盘菜单按钮配置，支持 Inspector 拖拽方法
// ---------------------------------------------------------------------------

[Serializable]
public class TrayMenuButton
{
    [Tooltip("菜单项显示文字")] public string Label = "操作";

    [Tooltip("是否在执行前先显示主窗口")] public bool ShowWindowFirst = false;
    [Tooltip("是否为分割线")] public bool IsSeparator = false;

    [Tooltip(
        "点击时执行的方法。\n" +
        "TrayBridge 会自动切换到 Unity 主线程执行，无需手动 Dispatcher。\n" +
        "直接在 Inspector 中拖拽 GameObject 并选择方法即可。")]
    public UnityEvent OnClick = new UnityEvent();
}

/// <summary>
/// 通用系统托盘桥接组件。
/// 不依赖任何业务逻辑，可挂载到任意项目的任意 GameObject。
/// </summary>
public class TrayBridge : MonoBehaviour
{
    [Header("基础配置")] [Tooltip("托盘图标悬停提示文字")]
    public string Tooltip = "Unity App";

    [Tooltip("最小化时自动隐藏到托盘")] public bool MinimizeToTray = true;

    [Tooltip("在 Editor 中也启用真实托盘（需要 Windows/Mac）")]
    public bool SimulateInEditor = true;

    [Header("图标（可选）")] [Tooltip("Windows：StreamingAssets 下的 .ico 文件名，留空使用系统默认图标")]
    public string WindowsIcoFileName = "app.ico";

    [Tooltip("macOS：StreamingAssets 下的 .png 图标文件名（建议 18x18 px），留空使用系统默认图标")]
    public string MacPngFileName = "app.png";

    [Header("内置菜单项")] [Tooltip("自动添加 [显示主窗口] 菜单项")]
    public bool AddShowWindowItem = true;

    [Tooltip("自动添加 [开机自动启动] 菜单项（仅 Windows）")]
    public bool AddStartupItem = true;

    [Tooltip("自动添加 [退出] 菜单项")] public bool AddQuitItem = true;

    // ---------------------------------------------------------------------------
    // 可配置自定义菜单按钮
    // ---------------------------------------------------------------------------
    [Header("自定义菜单按钮（Inspector 拖拽配置）")]
    [Tooltip(
        "在此列表中添加自定义托盘菜单按钮。\n" +
        "每项可配置显示文字和点击回调（UnityEvent）。\n" +
        "回调方法会自动在 Unity 主线程执行，无需手动 Dispatcher。\n" +
        "列表顺序即菜单显示顺序，插入在内置项之后、[开机自启] 之前。")]
    public TrayMenuButton[] CustomButtons = new TrayMenuButton[0];

    [Header("事件 — Inspector 中拖拽连接")] [Tooltip("托盘双击图标或点击 [显示主窗口] 时触发")]
    public UnityEvent OnShowMainWindow = new UnityEvent();

    [Tooltip("托盘点击 [退出] 时触发，触发后默认执行 Application.Quit")]
    public UnityEvent OnTrayQuit = new UnityEvent();

    [Tooltip("托盘图标初始化完成时触发")] public UnityEvent OnTrayReady = new UnityEvent();

    public static event Action ShowMainWindowRequested;
    public static event Action QuitRequested;
    public static event Action TrayReady;

    public static bool IsInitialized { get; private set; }
    private bool _initialized;

    // 主线程上下文，确保 Callback 安全回调
    private SynchronizationContext _mainCtx;

    // 代码注册的业务菜单项（由外部调用 RegisterExtraMenuItems 设置，插在 CustomButtons 之后）
    private readonly List<TrayMenuItem> _extraItems = new List<TrayMenuItem>();

    // 开机自启菜单项引用
    private TrayMenuItem _menuStartup;

    private void Start()
    {
        if (Application.isEditor && !SimulateInEditor)
        {
            Debug.Log("[TrayBridge] Editor 模式下跳过托盘初始化（SimulateInEditor=false）");
            return;
        }

        _mainCtx = SynchronizationContext.Current ?? new SynchronizationContext();
        InitTray();
    }

    private void OnDestroy()
    {
        ShutdownTray();
    }

    private void OnApplicationQuit()
    {
        ShutdownTray();
    }

    /// <summary>
    /// 代码注册业务菜单项，插入在 CustomButtons 之后、[开机自启] 之前。
    /// 可在 TrayReady 后任意时机调用，会立即重建菜单。
    /// </summary>
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
        if (_initialized)
            NativePlatform.Tray.SetTooltip(tooltip);
    }

    private void InitTray()
    {
        if (_initialized) return;

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

    /// <summary>
    /// 重建完整菜单，顺序固定：
    ///   [显示主窗口]
    ///   ─────────────（若有 CustomButtons）
    ///   [自定义按钮...]
    ///   ─────────────（若有代码注册项）
    ///   [代码注册项...]
    ///   ─────────────
    ///   [开机自动启动]（若启用）
    ///   ─────────────
    ///   [退出]
    /// </summary>
    private void RebuildMenu()
    {
        NativePlatform.Tray.ClearMenuItems();
        var items = new List<TrayMenuItem>();

        // 1. 显示主窗口
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

        // 2a. Inspector 自定义按钮（CustomButtons 列表，自动主线程调度）
        if (CustomButtons != null && CustomButtons.Length > 0)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            foreach (var btn in CustomButtons)
            {
                if (btn == null || string.IsNullOrEmpty(btn.Label)) continue;
                TrayMenuButton captured = btn; // 闭包捕获
                if (btn.IsSeparator)
                {
                    items.Add(new TrayMenuItem { IsSeparator = true });
                }
                else
                items.Add(new TrayMenuItem
                {
                    Text = captured.Label.Trim(),
                    Callback = () =>
                    {
                        // 自动切换到 Unity 主线程执行
                        _mainCtx.Post(_ =>
                        {
                            try
                            {
                                if (captured.ShowWindowFirst)
                                    NativePlatform.Tray.ShowMainWindow();
                                captured.OnClick?.Invoke();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[TrayBridge] 自定义按钮执行失败 '" + captured.Label + "': " + ex.Message);
                            }
                        }, null);
                    }
                });
            }
        }

        // 2b. 代码注册的业务菜单项（RegisterExtraMenuItems，如 CfstTrayManager）
        if (_extraItems.Count > 0)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            items.AddRange(_extraItems);
        }

        // 3. 开机自动启动（由 TrayBridge 统一管理）
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (AddStartupItem)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            bool startupEnabled = false;
            try
            {
                startupEnabled = WindowsStartup.IsStartupEnabled();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TrayBridge] 检查开机自启状态失败: " + ex.Message);
            }

            _menuStartup = new TrayMenuItem
            {
                Text = "开机自启",
                IsToggle = true,
                Checked = startupEnabled,
                Callback = () => _mainCtx.Post(_ =>
                {
                    try
                    {
                        string exePath = WindowsStartup.GetCurrentExePath();
                        bool now;
                        if (_menuStartup.Checked)
                        {
                            WindowsStartup.EnableStartup(exePath);
                            now = WindowsStartup.IsStartupEnabled();
                        }
                        else
                        {
                            WindowsStartup.DisableStartup();
                            now = false;
                        }

                        _menuStartup.Text = "开机自启";
                        _menuStartup.Checked = now;
                        NativePlatform.Tray.RefreshMenu();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TrayBridge] 开机自启切换失败: " + ex.Message);
                    }
                }, null)
            };
            items.Add(_menuStartup);
        }
#endif

        // 4. 退出
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

    private void TrySetIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (string.IsNullOrEmpty(WindowsIcoFileName)) return;
        string icoPath = System.IO.Path.Combine(
            Application.streamingAssetsPath, WindowsIcoFileName);
        if (System.IO.File.Exists(icoPath))
        {
            TrayIconService.Instance.SetIcon(icoPath);
            Debug.Log("[TrayBridge] 已加载托盘图标: " + icoPath);
        }
        else
        {
            Debug.LogWarning("[TrayBridge] 未找到图标: " + icoPath + "，使用系统默认");
        }
#elif UNITY_STANDALONE_OSX
        if (string.IsNullOrEmpty(MacPngFileName)) return;
        string pngPath = System.IO.Path.Combine(
            Application.streamingAssetsPath, MacPngFileName);
        if (System.IO.File.Exists(pngPath))
        {
            TrayIconService.Instance.SetIcon(pngPath);
            Debug.Log("[TrayBridge] macOS 已加载托盘图标: " + pngPath);
        }
        else
        {
            Debug.LogWarning("[TrayBridge] macOS 未找到图标: " + pngPath + "，使用系统默认（NSImageNameApplicationIcon）");
        }
#endif
    }
}