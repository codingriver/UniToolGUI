using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 窗口控制：置顶、位置、大小、最小化/最大化/还原、透明度（Win/Mac）
/// </summary>
public static class WindowsWindow
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int SW_RESTORE = 9;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x2;

    public static IntPtr GetUnityWindowHandle()
    {
        try
        {
            var h = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (h != IntPtr.Zero) return h;
        }
        catch { }
        var hwnd = FindWindow("UnityWndClass", null);
        return hwnd != IntPtr.Zero ? hwnd : FindWindow("UnityContainerWndClass", null);
    }

    public static bool SetTopMost(IntPtr hwnd, bool topMost)
    {
        return SetWindowPos(hwnd, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static bool SetPositionAndSize(IntPtr hwnd, int x, int y, int width, int height)
    {
        return SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOACTIVATE);
    }

    public static bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (!GetWindowRect(hwnd, out RECT r)) return false;
        x = r.Left;
        y = r.Top;
        width = r.Right - r.Left;
        height = r.Bottom - r.Top;
        return true;
    }

    public static bool Minimize(IntPtr hwnd) => ShowWindow(hwnd, SW_MINIMIZE);
    public static bool Maximize(IntPtr hwnd) => ShowWindow(hwnd, SW_MAXIMIZE);
    public static bool Restore(IntPtr hwnd) => ShowWindow(hwnd, SW_RESTORE);
    public static bool IsMinimized(IntPtr hwnd) => IsIconic(hwnd);
    public static bool IsMaximized(IntPtr hwnd) => IsZoomed(hwnd);

    /// <summary>
    /// 设置窗口透明度（0.0=完全透明，1.0=完全不透明）
    /// </summary>
    public static bool SetOpacity(IntPtr hwnd, float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
        return SetLayeredWindowAttributes(hwnd, 0, (byte)(opacity * 255), LWA_ALPHA);
    }

    // ---------- 便捷方法 ----------
    public static bool SetUnityWindowTopMost(bool topMost)
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && SetTopMost(h, topMost);
    }

    public static bool GetUnityWindowRect(out int x, out int y, out int width, out int height)
    {
        return GetPositionAndSize(GetUnityWindowHandle(), out x, out y, out width, out height);
    }

    public static bool SetUnityWindowRect(int x, int y, int width, int height)
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && SetPositionAndSize(h, x, y, width, height);
    }

    public static bool MinimizeUnityWindow()
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && Minimize(h);
    }

    public static bool MaximizeUnityWindow()
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && Maximize(h);
    }

    public static bool RestoreUnityWindow()
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && Restore(h);
    }

    public static bool SetUnityWindowOpacity(float opacity)
    {
        var h = GetUnityWindowHandle();
        return h != IntPtr.Zero && SetOpacity(h, opacity);
    }

#elif UNITY_STANDALONE_OSX
    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;

    public static bool SetTopMost(IntPtr hwnd, bool topMost) => MacWindowPlugin.SetTopMost(topMost);
    public static bool SetPositionAndSize(IntPtr hwnd, int x, int y, int width, int height) => MacWindowPlugin.SetFrame(x, y, width, height);
    public static bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int width, out int height) => MacWindowPlugin.GetFrame(out x, out y, out width, out height);

    public static bool Minimize(IntPtr hwnd)
    {
        Debug.LogWarning("[WindowsWindow] Mac Minimize 需通过 MacTray.bundle 扩展");
        return false;
    }
    public static bool Maximize(IntPtr hwnd)
    {
        Debug.LogWarning("[WindowsWindow] Mac Maximize 需通过 MacTray.bundle 扩展");
        return false;
    }
    public static bool Restore(IntPtr hwnd)
    {
        Debug.LogWarning("[WindowsWindow] Mac Restore 需通过 MacTray.bundle 扩展");
        return false;
    }
    public static bool IsMinimized(IntPtr hwnd) => false;
    public static bool IsMaximized(IntPtr hwnd) => false;
    public static bool SetOpacity(IntPtr hwnd, float opacity)
    {
        Debug.LogWarning("[WindowsWindow] Mac SetOpacity 需通过 MacTray.bundle 扩展（NSWindow.alphaValue）");
        return false;
    }

    public static bool SetUnityWindowTopMost(bool topMost) => MacWindowPlugin.SetTopMost(topMost);
    public static bool GetUnityWindowRect(out int x, out int y, out int width, out int height) => MacWindowPlugin.GetFrame(out x, out y, out width, out height);
    public static bool SetUnityWindowRect(int x, int y, int width, int height) => MacWindowPlugin.SetFrame(x, y, width, height);
    public static bool MinimizeUnityWindow() => Minimize(IntPtr.Zero);
    public static bool MaximizeUnityWindow() => Maximize(IntPtr.Zero);
    public static bool RestoreUnityWindow() => Restore(IntPtr.Zero);
    public static bool SetUnityWindowOpacity(float opacity) => SetOpacity(IntPtr.Zero, opacity);

#else
    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;
    public static bool SetTopMost(IntPtr hwnd, bool topMost) => false;
    public static bool SetPositionAndSize(IntPtr hwnd, int x, int y, int width, int height) => false;
    public static bool GetPositionAndSize(IntPtr hwnd, out int x, out int y, out int width, out int height) { x = y = width = height = 0; return false; }
    public static bool Minimize(IntPtr hwnd) => false;
    public static bool Maximize(IntPtr hwnd) => false;
    public static bool Restore(IntPtr hwnd) => false;
    public static bool IsMinimized(IntPtr hwnd) => false;
    public static bool IsMaximized(IntPtr hwnd) => false;
    public static bool SetOpacity(IntPtr hwnd, float opacity) => false;
    public static bool SetUnityWindowTopMost(bool topMost) => false;
    public static bool GetUnityWindowRect(out int x, out int y, out int width, out int height) { x = y = width = height = 0; return false; }
    public static bool SetUnityWindowRect(int x, int y, int width, int height) => false;
    public static bool MinimizeUnityWindow() => false;
    public static bool MaximizeUnityWindow() => false;
    public static bool RestoreUnityWindow() => false;
    public static bool SetUnityWindowOpacity(float opacity) => false;
#endif
}
