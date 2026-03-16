using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 系统主题检测与 DPI 缩放（Win/Mac/Linux）
/// </summary>
public static class WindowsTheme
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    /// <summary>
    /// 检测系统是否处于深色模式
    /// </summary>
    public static bool IsDarkMode()
    {
        try
        {
            using (var key = RegistryHelper.OpenCurrentUserKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (key == null) return false;
                var val = key.GetValue("AppsUseLightTheme");
                return val != null && (int)val == 0;
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// 获取系统 DPI 缩放比例（1.0=100%, 1.25=125%, 1.5=150%, 2.0=200%）
    /// </summary>
    public static float GetDpiScale()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            return dpi > 0 ? dpi / 96f : 1f;
        }
        catch { return 1f; }
    }

    /// <summary>
    /// 获取指定窗口的 DPI 缩放比例
    /// </summary>
    public static float GetDpiScaleForWindow(IntPtr hwnd)
    {
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96f : GetDpiScale();
        }
        catch { return GetDpiScale(); }
    }

    /// <summary>
    /// 获取系统强调色（ARGB）
    /// </summary>
    public static Color32 GetAccentColor()
    {
        try
        {
            using (var key = RegistryHelper.OpenCurrentUserKey(
                @"Software\Microsoft\Windows\DWM", false))
            {
                if (key == null) return new Color32(0, 120, 215, 255);
                var val = key.GetValue("AccentColor");
                if (val == null) return new Color32(0, 120, 215, 255);
                uint abgr = (uint)(int)val;
                byte a = (byte)(abgr >> 24);
                byte b = (byte)(abgr >> 16);
                byte g = (byte)(abgr >> 8);
                byte r = (byte)(abgr);
                return new Color32(r, g, b, a);
            }
        }
        catch { return new Color32(0, 120, 215, 255); }
    }

#elif UNITY_STANDALONE_OSX
    public static bool IsDarkMode()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("defaults", "read -g AppleInterfaceStyle", 2000);
            return !string.IsNullOrEmpty(output) && output.Contains("Dark");
        }
        catch { return false; }
    }

    public static float GetDpiScale()
    {
        return Screen.dpi > 0 ? Screen.dpi / 96f : 1f;
    }

    public static float GetDpiScaleForWindow(IntPtr hwnd) => GetDpiScale();

    public static Color32 GetAccentColor()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("defaults", "read -g AppleAccentColor", 2000);
            if (string.IsNullOrEmpty(output)) return new Color32(0, 122, 255, 255);
            switch (output.Trim())
            {
                case "-1": return new Color32(142, 142, 147, 255); // Graphite
                case "0": return new Color32(255, 59, 48, 255);   // Red
                case "1": return new Color32(255, 149, 0, 255);   // Orange
                case "2": return new Color32(255, 204, 0, 255);   // Yellow
                case "3": return new Color32(76, 217, 100, 255);  // Green
                case "5": return new Color32(175, 82, 222, 255);  // Purple
                case "6": return new Color32(255, 45, 85, 255);   // Pink
                default: return new Color32(0, 122, 255, 255);    // Blue (default)
            }
        }
        catch { return new Color32(0, 122, 255, 255); }
    }

#elif UNITY_STANDALONE_LINUX
    public static bool IsDarkMode()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("gsettings", "get org.gnome.desktop.interface color-scheme", 2000);
            if (!string.IsNullOrEmpty(output) && output.Contains("dark")) return true;
            output = ProcessHelper.RunAndRead("gsettings", "get org.gnome.desktop.interface gtk-theme", 2000);
            return !string.IsNullOrEmpty(output) && (output.ToLower().Contains("dark") || output.ToLower().Contains("adwaita-dark"));
        }
        catch { return false; }
    }

    public static float GetDpiScale()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("gsettings", "get org.gnome.desktop.interface text-scaling-factor", 2000);
            if (!string.IsNullOrEmpty(output) && float.TryParse(output.Trim().Trim('\''), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scale))
                return scale;
        }
        catch { }
        return Screen.dpi > 0 ? Screen.dpi / 96f : 1f;
    }

    public static float GetDpiScaleForWindow(IntPtr hwnd) => GetDpiScale();

    public static Color32 GetAccentColor()
    {
        return new Color32(53, 132, 228, 255); // GNOME 默认蓝
    }

#else
    public static bool IsDarkMode() => false;
    public static float GetDpiScale() => 1f;
    public static float GetDpiScaleForWindow(IntPtr hwnd) => 1f;
    public static Color32 GetAccentColor() => new Color32(0, 120, 215, 255);
#endif
}
