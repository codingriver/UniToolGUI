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
    private WndProcDelegate _newWndProc;
    private SynchronizationContext _mainCtx;
    private bool _trayAdded;
    private IntPtr _hCustomIcon;
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
        if (_hwnd==IntPtr.Zero) { Debug.LogError("[Tray] no window"); return; }
        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        var n=MakeNid(); n.uFlags=NIF_MSG|NIF_ICO|NIF_TIP; n.uCallbackMessage=WM_TRAYICON;
        n.hIcon=LoadIcon(IntPtr.Zero,IDI_APP); n.szTip=_tooltip;
        if (Shell_NotifyIcon(NIM_ADD,ref n)) { _trayAdded=true; RebuildWin(); FireCreated(); }
        else Debug.LogError("[Tray] Shell_NotifyIcon failed");
        _initialized=true;
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
        if (string.IsNullOrEmpty(iconPath)) return;
        var hNew = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE|LR_DEFAULTSIZE);
        if (hNew == IntPtr.Zero) { Debug.LogWarning("[Tray] LoadImage failed: " + iconPath); return; }
        if (_hCustomIcon != IntPtr.Zero) DestroyIcon(_hCustomIcon);
        _hCustomIcon = hNew;
        var n = MakeNid(); n.uFlags = NIF_ICO; n.hIcon = _hCustomIcon;
        Shell_NotifyIcon(NIM_MOD, ref n);
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
                if((uint)lParam==WM_RBUTTONUP) { if(_hMenu!=IntPtr.Zero){GetCursorPos(out POINT pt);SetForegroundWindow(_hwnd);TrackPopupMenu(_hMenu,TPM_RB,pt.x,pt.y,0,_hwnd,IntPtr.Zero);PostMessage(_hwnd,WM_NULL,IntPtr.Zero,IntPtr.Zero);} }
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
        if (!MacTrayPlugin.Init()) { Debug.LogError("[Tray] Mac init failed"); return; }
        MacTrayPlugin.SetTooltip(_tooltip); MacTrayPlugin.SetMenu(_menuItems);
        _initialized=true; FireCreated();
        Debug.Log("[TrayIconService] macOS 初始化完成");
    }
    void PlatformShutdown()       { MacTrayPlugin.Shutdown(); _initialized=false; FireDestroyed(); }
    void PlatformSetTooltip(string tip) => MacTrayPlugin.SetTooltip(tip);
    void PlatformRebuildMenu()    => MacTrayPlugin.SetMenu(_menuItems);
    void PlatformShowMainWindow() => MacTrayPlugin.ShowMainWindow();
    void PlatformShowBalloon(string title,string msg,uint _1,uint _2) => MacTrayPlugin.ShowBalloon(title??"",msg??"");
    void PlatformSetIcon(string iconPath) => MacTrayPlugin.SetIcon(iconPath);
    void PlatformSetIconFromData(byte[] pngData) => MacTrayPlugin.SetIconFromData(pngData);

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
