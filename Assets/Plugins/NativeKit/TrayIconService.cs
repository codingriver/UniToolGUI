using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class TrayIconService : ITrayService
{
    private static readonly object _lock = new object();
    private static TrayIconService _instance;
    public static TrayIconService Instance
    {
        get { if (_instance==null) lock(_lock) if (_instance==null) _instance=new TrayIconService(); return _instance; }
    }

    public event Action OnTrayIconCreated;
    public event Action OnTrayIconDestroyed;
    public event Action<int> OnHotkeyPressed;
    /// <summary>右键菜单弹出前触发，订阅者可在此更新菜单文字后调用 RefreshMenu()</summary>
    public event Action OnBeforeMenuOpen;

    private readonly List<TrayMenuItem> _menuItems = new List<TrayMenuItem>();
    private string _tooltip = "Unity App";
    private bool   _initialized;
    private TrayIconService() { }

    public void Initialize()   { if (_initialized) return; PlatformInitialize(); }
    public void Shutdown()     { if (!_initialized) return; PlatformShutdown(); }
    public void SetTooltip(string t) { _tooltip = t ?? "Unity App"; if (_initialized) PlatformSetTooltip(_tooltip); }
    public void RegisterMenuItems(IEnumerable<TrayMenuItem> items)   { foreach (var i in items) _menuItems.Add(i); if (_initialized) PlatformRebuildMenu(); }
    public void UnregisterMenuItems(IEnumerable<TrayMenuItem> items) { foreach (var i in items) _menuItems.Remove(i); if (_initialized) PlatformRebuildMenu(); }
    public void ClearMenuItems()  { _menuItems.Clear(); if (_initialized) PlatformRebuildMenu(); }
    public void RefreshMenu()     { if (_initialized) PlatformRebuildMenu(); }
    public void ShowMainWindow()  { if (_initialized) PlatformShowMainWindow(); }
    public void ShowBalloonTip(string title, string message, uint iconType=1, uint timeoutMs=5000)
        { if (_initialized) PlatformShowBalloon(title, message, iconType, timeoutMs); }

    /// <summary>
    /// 设置托盘图标。Win: 传入 .ico 文件路径；Mac: 传入 .png/.icns 路径（18x18 建议）。
    /// 必须在 Initialize() 之后调用。
    /// </summary>
    public void SetIcon(string iconPath)
        { if (_initialized) PlatformSetIcon(iconPath); }

    /// <summary>
    /// 从 Texture2D 内存数据设置托盘图标（仅 Mac 支持；Win 请使用 SetIcon(string)）。
    /// 用法：SetIcon(myTexture2D.EncodeToPNG());
    /// </summary>
    public void SetIcon(byte[] pngData)
        { if (_initialized) PlatformSetIconFromData(pngData); }

    /// <summary>从 Texture2D 直接设置（仅 Mac 支持）。</summary>
    public void SetIcon(UnityEngine.Texture2D texture)
    {
        if (texture == null || !_initialized) return;
        PlatformSetIconFromData(texture.EncodeToPNG());
    }

    void FireCreated()      => OnTrayIconCreated?.Invoke();
    void FireDestroyed()    => OnTrayIconDestroyed?.Invoke();
    void FireHotkey(int id) => OnHotkeyPressed?.Invoke(id);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // ---- Win32 fields ----
    private readonly Dictionary<uint,TrayMenuItem> _menuItemById = new Dictionary<uint,TrayMenuItem>();
    private uint   _nextId = 1000;
    private IntPtr _hwnd, _oldWndProc, _hMenu;
    // static 防止 GC 回收 delegate，导致鼠标 hover 时 WndProc 函数指针失效崩溃
    private static WndProcDelegate _newWndProc;
    private SynchronizationContext _mainCtx;
    private bool _trayAdded;
    private IntPtr _hCustomIcon;
    private bool _menuOpen;          // 右键菜单弹出期间为 true，禁止重建菜单
    private bool _rebuildPending;    // 菜单关闭后需要重建
    const uint TRAY_ID = 100;

    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr FindWindow(string c,string t);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int n);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr h,int i,IntPtr v);
    [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr p,IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
    [DllImport("shell32.dll",CharSet=CharSet.Auto)] static extern bool Shell_NotifyIcon(uint d,ref NOTIFYICONDATA n);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr LoadIcon(IntPtr h,IntPtr n);
    [DllImport("user32.dll",CharSet=CharSet.Auto,SetLastError=true)] static extern IntPtr LoadImage(IntPtr hInst,string name,uint type,int cx,int cy,uint fuLoad);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("shell32.dll",CharSet=CharSet.Auto)] static extern IntPtr ExtractIcon(IntPtr hInst,string exeFileName,int nIconIndex);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool AppendMenu(IntPtr h,uint f,uint id,string s);
    [DllImport("user32.dll")] static extern bool TrackPopupMenu(IntPtr h,uint f,int x,int y,int r,IntPtr w,IntPtr p);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr h);

    const int  GWL_WNDPROC=-4;
    const int  SW_HIDE=0,SW_SHOW=5,SW_RESTORE=9;
    const uint WM_SIZE=5,WM_CLOSE=0x10,WM_COMMAND=0x111,WM_HOTKEY=0x312;
    const uint WM_TRAYICON=0x500,WM_LBUTTONDBLCLK=0x203,WM_RBUTTONUP=0x205,WM_NULL=0;
    const uint SIZE_MIN=1,NIM_ADD=0,NIM_DEL=2,NIM_MOD=1;
    const uint NIF_MSG=1,NIF_ICO=2,NIF_TIP=4,NIF_INFO=0x10;
    const uint MF_STR=0,MF_SEP=0x800,MF_CHK=8,TPM_RB=2;
    const uint IMAGE_ICON=1,LR_LOADFROMFILE=0x10,LR_DEFAULTSIZE=0x40;
    static readonly IntPtr IDI_APP = new IntPtr(32512);

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    struct NOTIFYICONDATA {
        public uint cbSize; public IntPtr hWnd; public uint uID;
        public uint uFlags; public uint uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string szTip;
        public uint dwState,dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x,y; }
    delegate IntPtr WndProcDelegate(IntPtr h,uint m,IntPtr w,IntPtr l);

    void PlatformInitialize() {
        _mainCtx = SynchronizationContext.Current ?? new SynchronizationContext();
        _hwnd = GetUnityHwnd();
        Debug.Log(string.Format("[Tray][Init] HWND = 0x{0:X}", _hwnd.ToInt64()));
        if (_hwnd==IntPtr.Zero) { Debug.LogError("[Tray][Init] 未找到 Unity 窗口句柄，托盘初始化失败"); return; }
        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        var n=MakeNid(); n.uFlags=NIF_MSG|NIF_ICO|NIF_TIP; n.uCallbackMessage=WM_TRAYICON;
        // 使用系统默认图标作为初始占位，TrySetIcon() 会在 Initialize() 完成后用自定义图标替换
        IntPtr hExeIcon = LoadIcon(IntPtr.Zero, IDI_APP);
        Debug.Log(string.Format("[Tray][Init] 加载默认系统图标句柄: 0x{0:X}", hExeIcon.ToInt64()));
        n.hIcon = hExeIcon; n.szTip=_tooltip;
        bool addResult = Shell_NotifyIcon(NIM_ADD, ref n);
        if (addResult)
        {
            _trayAdded=true;
            Debug.Log("[Tray][Init] Shell_NotifyIcon(NIM_ADD) 成功，托盘图标已添加（默认系统图标）");
            RebuildWin();
            FireCreated();
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            Debug.LogError(string.Format("[Tray][Init] Shell_NotifyIcon(NIM_ADD) 失败，Win32 错误码: {0}", err));
        }
        _initialized=true;
        Debug.Log("[Tray][Init] PlatformInitialize 完成，_initialized = true");
    }
    void PlatformShutdown() {
        if (_trayAdded) { var n=MakeNid(); Shell_NotifyIcon(NIM_DEL,ref n); _trayAdded=false; }
        if (_hMenu!=IntPtr.Zero) { DestroyMenu(_hMenu); _hMenu=IntPtr.Zero; }
        if (_oldWndProc!=IntPtr.Zero) SetWindowLongPtr(_hwnd,GWL_WNDPROC,_oldWndProc);
        _initialized=false; FireDestroyed();
    }
    void PlatformSetTooltip(string tip) { var n=MakeNid(); n.uFlags=NIF_TIP; n.szTip=tip; Shell_NotifyIcon(NIM_MOD,ref n); }
    void PlatformRebuildMenu()    => RebuildWin();
    void PlatformShowMainWindow() { ShowWindow(_hwnd,SW_SHOW); ShowWindow(_hwnd,SW_RESTORE); SetForegroundWindow(_hwnd); }
    void PlatformShowBalloon(string title,string msg,uint iconType,uint ms) {
        var n=MakeNid(); n.uFlags=NIF_INFO; n.szInfoTitle=title??""; n.szInfo=msg??""; n.dwInfoFlags=iconType; n.uTimeout=ms;
        Shell_NotifyIcon(NIM_MOD,ref n);
    }
    void PlatformSetIcon(string iconPath) {
        // ── [Tray][Icon] 加载前日志 ──
        Debug.Log(string.Format("[Tray][Icon] PlatformSetIcon 开始，路径: {0}", iconPath));
        if (string.IsNullOrEmpty(iconPath))
        {
            Debug.LogWarning("[Tray][Icon] iconPath 为空，取消设置");
            return;
        }
        if (!System.IO.File.Exists(iconPath))
        {
            Debug.LogWarning(string.Format("[Tray][Icon] 图标文件不存在（Win32 LoadImage 前检查）: {0}", iconPath));
            return;
        }
        // Win32 LoadImage 要求纯反斜杠路径，统一替换正斜杠
        iconPath = iconPath.Replace('/', '\\');
        Debug.Log(string.Format("[Tray][Icon] 规范化后路径: {0}", iconPath));
        Debug.Log(string.Format("[Tray][Icon] 调用 LoadImage(LR_LOADFROMFILE|LR_DEFAULTSIZE)，路径: {0}", iconPath));
        var hNew = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE|LR_DEFAULTSIZE);
        // ── [Tray][Icon] 加载后日志 ──
        if (hNew == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            Debug.LogError(string.Format("[Tray][Icon] LoadImage 失败！Win32 错误码: {0}，路径: {1}" +
                "\n  常见原因: (1) 文件不是有效的 .ico 格式；(2) 路径含中文/空格（尝试短路径）；(3) 文件被锁定。", err, iconPath));
            return;
        }
        Debug.Log(string.Format("[Tray][Icon] LoadImage 成功，HICON = 0x{0:X}", hNew.ToInt64()));
        if (_hCustomIcon != IntPtr.Zero)
        {
            Debug.Log("[Tray][Icon] 释放旧的自定义图标句柄");
            DestroyIcon(_hCustomIcon);
        }
        _hCustomIcon = hNew;
        var n = MakeNid(); n.uFlags = NIF_ICO; n.hIcon = _hCustomIcon;
        bool modResult = Shell_NotifyIcon(NIM_MOD, ref n);
        if (modResult)
            Debug.Log(string.Format("[Tray][Icon] Shell_NotifyIcon(NIM_MOD) 成功，托盘图标已更新为自定义图标，路径: {0}", iconPath));
        else
        {
            int err = Marshal.GetLastWin32Error();
            Debug.LogError(string.Format("[Tray][Icon] Shell_NotifyIcon(NIM_MOD) 失败，Win32 错误码: {0}", err));
        }
    }
    void PlatformSetIconFromData(byte[] pngData) {
        // Windows 不支持直接从内存 PNG 加载，降级为忽略（需传 .ico 文件路径）
        Debug.LogWarning("[Tray] Windows 不支持从内存数据设置托盘图标，请使用 SetIcon(string .ico 路径)");
    }
    static IntPtr GetUnityHwnd() {
        try { var h=System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; if(h!=IntPtr.Zero) return h; } catch {}
        var w=FindWindow("UnityWndClass",null); return w!=IntPtr.Zero?w:FindWindow("UnityContainerWndClass",null);
    }
    NOTIFYICONDATA MakeNid() { var n=new NOTIFYICONDATA(); n.cbSize=(uint)Marshal.SizeOf(n); n.hWnd=_hwnd; n.uID=TRAY_ID; return n; }
    void RebuildWin() {
        // 菜单弹出期间不重建，避免 TrackPopupMenu 持有的旧 HMENU 变成野指针
        // 标记 pending，菜单关闭后（TrackPopupMenu 返回）立即补建
        if (_menuOpen) { _rebuildPending = true; return; }
        if (_hMenu!=IntPtr.Zero) DestroyMenu(_hMenu);
        _hMenu=CreatePopupMenu(); _menuItemById.Clear();
        foreach (var item in _menuItems) {
            if (item.IsSeparator) { AppendMenu(_hMenu,MF_SEP,0,null); }
            else { uint id=_nextId++; _menuItemById[id]=item;
                   uint f=MF_STR; if(item.IsToggle&&item.Checked) f|=MF_CHK;
                   AppendMenu(_hMenu,f,id,item.Text); }
        }
    }
    IntPtr WndProc(IntPtr hWnd,uint msg,IntPtr wParam,IntPtr lParam) {
        switch(msg) {
            case WM_SIZE: if((uint)wParam==SIZE_MIN){ShowWindow(_hwnd,SW_HIDE);return IntPtr.Zero;} break;
            case WM_CLOSE: ShowWindow(_hwnd,SW_HIDE); return IntPtr.Zero;
            case WM_TRAYICON:
                if((uint)lParam==WM_RBUTTONUP) {
                    if(_hMenu!=IntPtr.Zero) {
                        // 弹出前通知订阅者刷新数据（协程被阻塞无法自动更新）
                        OnBeforeMenuOpen?.Invoke();
                        GetCursorPos(out POINT pt);
                        SetForegroundWindow(_hwnd);
                        _menuOpen = true;
                        TrackPopupMenu(_hMenu,TPM_RB,pt.x,pt.y,0,_hwnd,IntPtr.Zero);
                        _menuOpen = false;
                        if (_rebuildPending) { _rebuildPending = false; RebuildWin(); }
                        PostMessage(_hwnd,WM_NULL,IntPtr.Zero,IntPtr.Zero);
                    }
                }
                else if((uint)lParam==WM_LBUTTONDBLCLK) PlatformShowMainWindow();
                break;
            case WM_HOTKEY: FireHotkey((int)wParam); return IntPtr.Zero;
            case WM_COMMAND:
                uint cmd=(uint)wParam&0xFFFF;
                if (_menuItemById.TryGetValue(cmd,out var mi)) {
                    if (mi.IsToggle) { mi.Checked=!mi.Checked; RebuildWin(); }
                    _mainCtx.Post(_=>mi.Callback?.Invoke(),null);
                } break;
        }
        return CallWindowProc(_oldWndProc,hWnd,msg,wParam,lParam);
    }

#elif UNITY_STANDALONE_OSX
    void PlatformInitialize() {
        try
        {
            if (!MacTrayPlugin.Init()) { UnityEngine.Debug.LogWarning("[Tray] Mac tray init failed (MacTray.bundle may be missing)"); return; }
            MacTrayPlugin.SetTooltip(_tooltip); MacTrayPlugin.SetMenu(_menuItems);
            _initialized=true; FireCreated();
            UnityEngine.Debug.Log("[TrayIconService] macOS 初始化完成");
        }
        catch (DllNotFoundException ex)
        {
            UnityEngine.Debug.LogWarning("[Tray] MacTray.bundle 未找到，托盘功能已禁用。请编译并放置 MacTray.bundle 到 Assets/Plugins/NativeKit/。\n" + ex.Message);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("[Tray] macOS 托盘初始化异常: " + ex.Message);
        }
    }
    void PlatformShutdown()       { try { MacTrayPlugin.Shutdown(); } catch {} _initialized=false; FireDestroyed(); }
    void PlatformSetTooltip(string tip) { try { MacTrayPlugin.SetTooltip(tip); } catch {} }
    void PlatformRebuildMenu()    { try { MacTrayPlugin.SetMenu(_menuItems); } catch {} }
    void PlatformShowMainWindow() { try { MacTrayPlugin.ShowMainWindow(); } catch {} }
    void PlatformShowBalloon(string title,string msg,uint _1,uint _2) { try { MacTrayPlugin.ShowBalloon(title??"",msg??""); } catch {} }
    void PlatformSetIcon(string iconPath) { try { MacTrayPlugin.SetIcon(iconPath); } catch {} }
    void PlatformSetIconFromData(byte[] pngData) { try { MacTrayPlugin.SetIconFromData(pngData); } catch {} }

#else
    void PlatformInitialize() { Debug.LogWarning("[TrayIconService] 托盘功能仅支持 Windows 和 macOS"); }
    void PlatformShutdown() { }
    void PlatformSetTooltip(string t) { }
    void PlatformRebuildMenu() { }
    void PlatformShowMainWindow() { }
    void PlatformShowBalloon(string a,string b,uint c,uint d) { }
    void PlatformSetIcon(string iconPath) { }
    void PlatformSetIconFromData(byte[] pngData) { }
#endif
}

/// <summary>托盘菜单项</summary>
public class TrayMenuItem
{
    public string Text      { get; set; }
    public Action Callback  { get; set; }
    public bool IsSeparator { get; set; }
    public bool IsToggle    { get; set; }
    public bool Checked     { get; set; }
}
