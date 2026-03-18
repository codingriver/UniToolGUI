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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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
                        if (_menuStartup.Checked)
                            WindowsStartup.EnableStartup(WindowsStartup.GetCurrentExePath());
                        else
                            WindowsStartup.DisableStartup();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TrayBridge] 开机自启切换失败: " + ex.Message);
                        _menuStartup.Checked = !_menuStartup.Checked;
                        NativePlatform.Tray.RefreshMenu();
                    }
                }, null)
            };
            items.Add(_menuStartup);
        }

        if (AddRestartItem)
        {
            if (items.Count > 0) items.Add(new TrayMenuItem { IsSeparator = true });
            bool isAdmin = false;
            try { isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }
            string level = isAdmin ? "管理员" : "普通用户";

            items.Add(new TrayMenuItem
            {
                Text = "重启程序（" + level + "）",
                Callback = () => _mainCtx.Post(_ =>
                {
                    try
                    {
                        var exe = WindowsStartup.GetCurrentExePath();
                        if (!string.IsNullOrEmpty(exe))
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = true });
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

            if (!isAdmin)
            {
                items.Add(new TrayMenuItem
                {
                    Text = "以管理员重启",
                    Callback = () => _mainCtx.Post(_ =>
                    {
                        bool ok = false;
                        try { ok = WindowsAdmin.RestartAsAdmin(); } catch { }
                        if (!ok)
                            NativePlatform.MessageBox.Warning(
                                "提升权限失败，请手动以管理员身份运行本程序。",
                                "权限不足");
                    }, null)
                });
            }
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

    private void TrySetIcon()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (string.IsNullOrEmpty(WindowsIcoFileName)) return;
        string icoPath = System.IO.Path.Combine(Application.streamingAssetsPath, WindowsIcoFileName);
        if (System.IO.File.Exists(icoPath)) { TrayIconService.Instance.SetIcon(icoPath); Debug.Log("[TrayBridge] 图标: " + icoPath); }
        else Debug.LogWarning("[TrayBridge] 未找到图标: " + icoPath);
#elif UNITY_STANDALONE_OSX
        if (string.IsNullOrEmpty(MacPngFileName)) return;
        string pngPath = System.IO.Path.Combine(Application.streamingAssetsPath, MacPngFileName);
        if (System.IO.File.Exists(pngPath)) { TrayIconService.Instance.SetIcon(pngPath); Debug.Log("[TrayBridge] macOS 图标: " + pngPath); }
        else Debug.LogWarning("[TrayBridge] macOS 未找到图标: " + pngPath);
#endif
    }
}
