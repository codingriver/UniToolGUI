using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// 托盘图标服务（单例），负责管理托盘图标的创建、销毁及消息处理。
/// 业务逻辑通过注册 TrayMenuItem 来定义菜单内容和响应。
/// 支持 Windows 和 Mac 平台。
/// </summary>
public class TrayIconService
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // ---------- 单例访问（线程安全） ----------
    private static readonly object _singletonLock = new object();
    private static TrayIconService _instance;
    public static TrayIconService Instance
    {
        get
        {
            if (_instance == null)
                lock (_singletonLock)
                    if (_instance == null)
                        _instance = new TrayIconService();
            return _instance;
        }
    }

    // ---------- 菜单项定义 ----------
    private readonly List<TrayMenuItem> _menuItems = new List<TrayMenuItem>();
    private readonly Dictionary<uint, TrayMenuItem> _menuItemById = new Dictionary<uint, TrayMenuItem>();
    private uint _nextMenuItemId = 1000; // 起始ID，避免与系统消息冲突

    private string _tooltip = "Unity App";
    private bool _initialized = false;
    private bool _trayIconAdded = false;

    // Win32 相关成员
    private IntPtr _hwnd;
    private IntPtr _oldWndProc;
    private WndProcDelegate _newWndProc;
    private const uint TrayIconID = 100;
    private IntPtr _hMenu;

    // 主线程同步上下文（用于回调封送）
    private SynchronizationContext _mainThreadContext;

    // ---------- 事件：可选的全局通知 ----------
    public event Action OnTrayIconCreated;
    public event Action OnTrayIconDestroyed;
    /// <summary> 全局热键按下时触发，参数为热键 ID </summary>
    public event Action<int> OnHotkeyPressed;

    // ---------- 私有构造（确保单例） ----------
    private TrayIconService() { }

    // ---------- 初始化与销毁 ----------
    public void Initialize()
    {
        if (_initialized) return;
        _mainThreadContext = SynchronizationContext.Current ?? new SynchronizationContext();

        FindUnityWindow();
        if (_hwnd == IntPtr.Zero)
        {
            Debug.LogError("[TrayIconService] 无法获取Unity主窗口句柄");
            return;
        }

        SubclassWindow();
        CreateTrayIcon();
        _initialized = true;
        Debug.Log("[TrayIconService] 初始化完成");
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        RemoveTrayIcon();
        RestoreWindowProc();
        _initialized = false;
        Debug.Log("[TrayIconService] 已关闭");
    }

    // ---------- 公共配置接口 ----------
    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        if (_trayIconAdded) UpdateTrayIcon();
    }

    public void RegisterMenuItems(IEnumerable<TrayMenuItem> items)
    {
        foreach (var item in items)
        {
            _menuItems.Add(item);
            // 稍后在重建菜单时统一分配ID
        }
        if (_trayIconAdded) RebuildMenu();
    }

    public void UnregisterMenuItems(IEnumerable<TrayMenuItem> items)
    {
        foreach (var item in items) _menuItems.Remove(item);
        if (_trayIconAdded) RebuildMenu();
    }

    public void ClearMenuItems()
    {
        _menuItems.Clear();
        if (_trayIconAdded) RebuildMenu();
    }

    // ---------- 内部实现 ----------
    #region Win32 核心

    // ... 所有 Win32 API 导入和常量（同之前提供的一致，此处省略节省篇幅，实际使用时需完整粘贴）...

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    private const int GWLP_WNDPROC = -4;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    private const uint WM_SIZE = 0x0005;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_COMMAND = 0x0111;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_TRAYICON = 0x400 + 100;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_NULL = 0x0000;
    private const uint MF_CHECKED=0x00000008;
    private const uint SIZE_MINIMIZED = 1;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIIF_NONE = 0;
    private const uint NIIF_INFO = 1;
    private const uint NIIF_WARNING = 2;
    private const uint NIIF_ERROR = 3;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private static readonly IntPtr IDI_APPLICATION = new IntPtr(32512);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion

    #region 窗口与托盘管理

    private void FindUnityWindow()
    {
        try
        {
            _hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (_hwnd != IntPtr.Zero) return;
        }
        catch { }

        _hwnd = FindWindow("UnityWndClass", null);
        if (_hwnd == IntPtr.Zero)
            _hwnd = FindWindow("UnityContainerWndClass", null);
    }

    private void SubclassWindow()
    {
        _newWndProc = WndProc;
        IntPtr ptrNew = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, ptrNew);
        if (_oldWndProc == IntPtr.Zero)
        {
            Debug.LogError("[TrayIconService] 子类化窗口失败");
        }
    }

    private void RestoreWindowProc()
    {
        if (_oldWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
        {
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_SIZE:
                if ((uint)wParam == SIZE_MINIMIZED)
                {
                    ShowWindow(_hwnd, SW_HIDE);
                    return IntPtr.Zero;
                }
                break;

            case WM_CLOSE:
                ShowWindow(_hwnd, SW_HIDE);
                return IntPtr.Zero;

            case WM_TRAYICON:
                uint trayMsg = (uint)lParam;
                if (trayMsg == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                }
                else if (trayMsg == WM_LBUTTONDBLCLK)
                {
                    ShowMainWindow();
                }
                break;

            case WM_HOTKEY:
                OnHotkeyPressed?.Invoke((int)wParam);
                return IntPtr.Zero;

            case WM_COMMAND:
                uint cmd = (uint)wParam & 0xFFFF;
                if (_menuItemById.TryGetValue(cmd, out var item))
                {
                    if (item.IsToggle)
                    {
                        item.Checked = !item.Checked; // 切换勾选状态
                        RebuildMenu(); // 立即刷新菜单显示
                    }

                    // 将回调封送到主线程执行
                    _mainThreadContext.Post(_ => item.Callback?.Invoke(), null);
                }

                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void CreateTrayIcon()
    {
        NOTIFYICONDATA nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
        nid.hWnd = _hwnd;
        nid.uID = TrayIconID;
        nid.uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
        nid.szTip = _tooltip;

        if (Shell_NotifyIcon(NIM_ADD, ref nid))
        {
            _trayIconAdded = true;
            OnTrayIconCreated?.Invoke();
            RebuildMenu(); // 图标创建后立即构建菜单
        }
        else
        {
            Debug.LogError("[TrayIconService] 添加托盘图标失败");
        }
    }

    private void UpdateTrayIcon()
    {
        if (!_trayIconAdded) return;
        NOTIFYICONDATA nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
        nid.hWnd = _hwnd;
        nid.uID = TrayIconID;
        nid.uFlags = NIF_TIP;
        nid.szTip = _tooltip;
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    private void RemoveTrayIcon()
    {
        if (_trayIconAdded)
        {
            NOTIFYICONDATA nid = new NOTIFYICONDATA();
            nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
            nid.hWnd = _hwnd;
            nid.uID = TrayIconID;
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _trayIconAdded = false;
            OnTrayIconDestroyed?.Invoke();
        }
        if (_hMenu != IntPtr.Zero)
        {
            DestroyMenu(_hMenu);
            _hMenu = IntPtr.Zero;
        }
    }

    public void ShowMainWindow()
    {
        ShowWindow(_hwnd, SW_SHOW);
        ShowWindow(_hwnd, SW_RESTORE);
        SetForegroundWindow(_hwnd);
    }

    private void ShowContextMenu()
    {
        if (_hMenu == IntPtr.Zero) return;
        GetCursorPos(out POINT pt);
        SetForegroundWindow(_hwnd);
        TrackPopupMenu(_hMenu, TPM_RIGHTBUTTON, pt.x, pt.y, 0, _hwnd, IntPtr.Zero);
        PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
    }
    
    /// <summary>
    /// 显示托盘气泡提示（需先 Initialize）
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">内容</param>
    /// <param name="iconType">0=无, 1=信息, 2=警告, 3=错误</param>
    /// <param name="timeoutMs">显示时长毫秒，0 表示系统默认</param>
    public void ShowBalloonTip(string title, string message, uint iconType = NIIF_INFO, uint timeoutMs = 5000)
    {
        if (!_trayIconAdded) return;
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
        nid.hWnd = _hwnd;
        nid.uID = TrayIconID;
        nid.uFlags = NIF_INFO;
        nid.szInfoTitle = title ?? "";
        nid.szInfo = message ?? "";
        nid.dwInfoFlags = iconType;
        nid.uTimeout = timeoutMs;
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    // 提供刷新菜单的方法（供动态更新文本调用）
    public void RefreshMenu()
    {
        if (_trayIconAdded)
            RebuildMenu();
    }
    private void RebuildMenu()
    {
        if (_hMenu != IntPtr.Zero)
            DestroyMenu(_hMenu);

        _hMenu = CreatePopupMenu();
        _menuItemById.Clear();

        foreach (var item in _menuItems)
        {
            if (item.IsSeparator)
            {
                AppendMenu(_hMenu, MF_SEPARATOR, 0, null);
            }
            else
            {
                uint id = _nextMenuItemId++;
                _menuItemById[id] = item;
                uint flags = MF_STRING;
                if (item.IsToggle && item.Checked)
                    flags |= MF_CHECKED;   // 添加勾选标记
                // 可选：若需要禁用项，可增加 MF_GRAYED 判断                
                AppendMenu(_hMenu, flags , id, item.Text);
            }
        }
    }

    #endregion

#elif UNITY_STANDALONE_OSX
    // ---------- Mac 平台：通过 MacTray.bundle 原生插件 ----------
    private static readonly object _singletonLock = new object();
    private static TrayIconService _instance;
    public static TrayIconService Instance
    {
        get
        {
            if (_instance == null)
                lock (_singletonLock)
                    if (_instance == null)
                        _instance = new TrayIconService();
            return _instance;
        }
    }

    private readonly List<TrayMenuItem> _menuItems = new List<TrayMenuItem>();
    private string _tooltip = "Unity App";
    private bool _initialized = false;

    public event Action OnTrayIconCreated;
    public event Action OnTrayIconDestroyed;
    public event Action<int> OnHotkeyPressed;

    private TrayIconService() { }

    public void Initialize()
    {
        if (_initialized) return;
        if (MacTrayPlugin.Init())
        {
            _initialized = true;
            MacTrayPlugin.SetTooltip(_tooltip);
            RebuildMenu();
            OnTrayIconCreated?.Invoke();
            Debug.Log("[TrayIconService] Mac 托盘初始化完成");
        }
        else
        {
            Debug.LogError("[TrayIconService] Mac 托盘初始化失败，请确保 MacTray.bundle 已编译并放入 Plugins 目录");
        }
    }

    public void Shutdown()
    {
        if (!_initialized) return;
        MacTrayPlugin.Shutdown();
        _initialized = false;
        OnTrayIconDestroyed?.Invoke();
    }

    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip ?? "Unity App";
        if (_initialized) MacTrayPlugin.SetTooltip(_tooltip);
    }

    public void RegisterMenuItems(IEnumerable<TrayMenuItem> items)
    {
        foreach (var item in items) _menuItems.Add(item);
        if (_initialized) RebuildMenu();
    }

    public void UnregisterMenuItems(IEnumerable<TrayMenuItem> items)
    {
        foreach (var item in items) _menuItems.Remove(item);
        if (_initialized) RebuildMenu();
    }

    public void ClearMenuItems()
    {
        _menuItems.Clear();
        if (_initialized) RebuildMenu();
    }

    public void ShowMainWindow()
    {
        if (_initialized) MacTrayPlugin.ShowMainWindow();
    }

    public void RefreshMenu()
    {
        if (_initialized) RebuildMenu();
    }

    public void ShowBalloonTip(string title, string message, uint iconType = 1, uint timeoutMs = 5000)
    {
        if (_initialized) MacTrayPlugin.ShowBalloon(title ?? "", message ?? "");
    }

    private void RebuildMenu()
    {
        MacTrayPlugin.SetMenu(_menuItems);
    }

#else
    // ---------- 非 Win/Mac 平台：空实现 ----------
    private static readonly object _singletonLock = new object();
    private static TrayIconService _instance;
    public static TrayIconService Instance
    {
        get
        {
            if (_instance == null)
                lock (_singletonLock)
                    if (_instance == null)
                        _instance = new TrayIconService();
            return _instance;
        }
    }

    public event Action OnTrayIconCreated;
    public event Action OnTrayIconDestroyed;
    public event Action<int> OnHotkeyPressed;

    private TrayIconService() { }

    public void Initialize()
    {
        Debug.LogWarning("[TrayIconService] 托盘功能仅在 Windows 和 Mac 平台可用");
    }

    public void Shutdown() { }

    public void SetTooltip(string tooltip) { }

    public void RegisterMenuItems(IEnumerable<TrayMenuItem> items) { }

    public void UnregisterMenuItems(IEnumerable<TrayMenuItem> items) { }

    public void ClearMenuItems() { }

    public void ShowMainWindow() { }

    public void RefreshMenu() { }

    public void ShowBalloonTip(string title, string message, uint iconType = 1, uint timeoutMs = 5000) { }
#endif
}
/// <summary>
/// 托盘菜单项，可被业务脚本注册到 TrayIconService
/// </summary>
public class TrayMenuItem
{
    public string Text { get; set; }                // 菜单显示文本（分隔符时忽略）
    public Action Callback { get; set; }             // 点击回调（会在主线程执行）
    public bool IsSeparator { get; set; } = false;   // 是否为分隔符
    // 新增：Toggle 相关
    public bool IsToggle { get; set; } = false;   // 是否为可勾选菜单项
    public bool Checked { get; set; } = false;    // 当前是否勾选
}