using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 窗口控制：置顶、位置/大小、最小化/最大化/还原、透明度（Win/Mac）。
/// </summary>
public static class WindowsWindow
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll",SetLastError=true)] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll",SetLastError=true)] static extern bool SetWindowPos(IntPtr h, IntPtr i, int x, int y, int cx, int cy, uint f);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr FindWindow(string c, string t);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll",SetLastError=true)] static extern int  GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll",SetLastError=true)] static extern int  SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint c, byte b, uint f);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    const uint SWP_NOMOVE    = 0x0002;
    const uint SWP_NOSIZE    = 0x0001;
    const uint SWP_NOACTIVATE= 0x0010;
    const int  SW_MINIMIZE   = 6;
    const int  SW_MAXIMIZE   = 3;
    const int  SW_RESTORE    = 9;
    const int  GWL_EXSTYLE   = -20;
    const int  WS_EX_LAYERED = 0x00080000;
    const uint LWA_ALPHA     = 0x2;

    public static IntPtr GetUnityWindowHandle()
    {
        try { var h = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle; if (h != IntPtr.Zero) return h; } catch { }
        var wnd = FindWindow("UnityWndClass", null);
        return wnd != IntPtr.Zero ? wnd : FindWindow("UnityContainerWndClass", null);
    }

    public static bool SetTopMost(IntPtr hwnd, bool topMost)
        => SetWindowPos(hwnd, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0,0,0,0, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE);

    public static bool SetPositionAndSize(IntPtr hwnd, int x, int y, int width, int height)
        => SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE);

    public static bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!GetWindowRect(hwnd, out RECT r)) return false;
        x = r.Left; y = r.Top; width = r.Right - r.Left; height = r.Bottom - r.Top; return true;
    }

    public static bool Minimize(IntPtr hwnd)    => ShowWindow(hwnd, SW_MINIMIZE);
    public static bool Maximize(IntPtr hwnd)    => ShowWindow(hwnd, SW_MAXIMIZE);
    public static bool Restore(IntPtr hwnd)     => ShowWindow(hwnd, SW_RESTORE);
    public static bool IsMinimized(IntPtr hwnd) => IsIconic(hwnd);
    public static bool IsMaximized(IntPtr hwnd) => IsZoomed(hwnd);

    public static bool SetOpacity(IntPtr hwnd, float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
        return SetLayeredWindowAttributes(hwnd, 0, (byte)(opacity * 255), LWA_ALPHA);
    }

    // Unity 主窗口便捷方法
    public static bool SetUnityWindowTopMost(bool t)                              => SetTopMost(GetUnityWindowHandle(), t);
    public static bool GetUnityWindowRect(out int x,out int y,out int w,out int h) => GetPositionAndSize(GetUnityWindowHandle(),out x,out y,out w,out h);
    public static bool SetUnityWindowRect(int x,int y,int w,int h)               => SetPositionAndSize(GetUnityWindowHandle(),x,y,w,h);
    public static bool MinimizeUnityWindow()   => Minimize(GetUnityWindowHandle());
    public static bool MaximizeUnityWindow()   => Maximize(GetUnityWindowHandle());
    public static bool RestoreUnityWindow()    => Restore(GetUnityWindowHandle());
    public static bool IsUnityWindowMinimized()=> IsMinimized(GetUnityWindowHandle());
    public static bool IsUnityWindowMaximized()=> IsMaximized(GetUnityWindowHandle());
    public static bool SetUnityWindowOpacity(float o) => SetOpacity(GetUnityWindowHandle(), o);

#elif UNITY_STANDALONE_OSX
    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;
    public static bool SetTopMost(IntPtr h, bool t)                              => MacWindowPlugin.SetTopMost(t);
    public static bool SetPositionAndSize(IntPtr h, int x, int y, int w, int ht) => MacWindowPlugin.SetFrame(x,y,w,ht);
    public static bool GetPositionAndSize(IntPtr h, out int x, out int y, out int w, out int ht) => MacWindowPlugin.GetFrame(out x,out y,out w,out ht);
    public static bool Minimize(IntPtr h)    => MacWindowPlugin.Minimize();
    public static bool Maximize(IntPtr h)    => MacWindowPlugin.Maximize();
    public static bool Restore(IntPtr h)     => MacWindowPlugin.Restore();
    public static bool IsMinimized(IntPtr h) => MacWindowPlugin.IsMinimized();
    public static bool IsMaximized(IntPtr h) => MacWindowPlugin.IsMaximized();
    public static bool SetOpacity(IntPtr h, float o) => MacWindowPlugin.SetOpacity(o);
    public static bool SetUnityWindowTopMost(bool t)                              => MacWindowPlugin.SetTopMost(t);
    public static bool GetUnityWindowRect(out int x,out int y,out int w,out int h) => MacWindowPlugin.GetFrame(out x,out y,out w,out h);
    public static bool SetUnityWindowRect(int x,int y,int w,int h)               => MacWindowPlugin.SetFrame(x,y,w,h);
    public static bool MinimizeUnityWindow()   => MacWindowPlugin.Minimize();
    public static bool MaximizeUnityWindow()   => MacWindowPlugin.Maximize();
    public static bool RestoreUnityWindow()    => MacWindowPlugin.Restore();
    public static bool IsUnityWindowMinimized()=> MacWindowPlugin.IsMinimized();
    public static bool IsUnityWindowMaximized()=> MacWindowPlugin.IsMaximized();
    public static bool SetUnityWindowOpacity(float o) => MacWindowPlugin.SetOpacity(o);

#else
    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;
    public static bool SetTopMost(IntPtr h, bool t) => false;
    public static bool SetPositionAndSize(IntPtr h,int x,int y,int w,int ht) => false;
    public static bool GetPositionAndSize(IntPtr h,out int x,out int y,out int w,out int ht) { x=y=w=ht=0; return false; }
    public static bool Minimize(IntPtr h) => false;
    public static bool Maximize(IntPtr h) => false;
    public static bool Restore(IntPtr h)  => false;
    public static bool IsMinimized(IntPtr h) => false;
    public static bool IsMaximized(IntPtr h) => false;
    public static bool SetOpacity(IntPtr h, float o) => false;
    public static bool SetUnityWindowTopMost(bool t) => false;
    public static bool GetUnityWindowRect(out int x,out int y,out int w,out int h) { x=y=w=h=0; return false; }
    public static bool SetUnityWindowRect(int x,int y,int w,int h) => false;
    public static bool MinimizeUnityWindow()    => false;
    public static bool MaximizeUnityWindow()    => false;
    public static bool RestoreUnityWindow()     => false;
    public static bool IsUnityWindowMinimized() => false;
    public static bool IsUnityWindowMaximized() => false;
    public static bool SetUnityWindowOpacity(float o) => false;
#endif
}
