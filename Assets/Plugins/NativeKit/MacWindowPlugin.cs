using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Mac 窗口控制原生插件封装（位置、大小、置顶、最小化、最大化、还原、透明度）。
/// 依赖编译好的 MacTray.bundle。
/// </summary>
public static class MacWindowPlugin
{
#if UNITY_STANDALONE_OSX
    private const string Lib = "MacTray";

    [DllImport(Lib)] static extern int MacWindow_GetFrame(out int x, out int y, out int w, out int h);
    [DllImport(Lib)] static extern int MacWindow_SetFrame(int x, int y, int w, int h);
    [DllImport(Lib)] static extern int MacWindow_SetTopMost(int topMost);
    [DllImport(Lib)] static extern int MacWindow_Minimize();
    [DllImport(Lib)] static extern int MacWindow_Maximize();
    [DllImport(Lib)] static extern int MacWindow_Restore();
    [DllImport(Lib)] static extern int MacWindow_IsMinimized();
    [DllImport(Lib)] static extern int MacWindow_IsMaximized();
    [DllImport(Lib)] static extern int MacWindow_SetAlpha(float alpha);

    public static bool GetFrame(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        return MacWindow_GetFrame(out x, out y, out width, out height) != 0;
    }

    public static bool SetFrame(int x, int y, int width, int height)
        => MacWindow_SetFrame(x, y, width, height) != 0;

    public static bool SetTopMost(bool topMost)
        => MacWindow_SetTopMost(topMost ? 1 : 0) != 0;

    public static bool Minimize()    => MacWindow_Minimize()    != 0;
    public static bool Maximize()    => MacWindow_Maximize()    != 0;
    public static bool Restore()     => MacWindow_Restore()     != 0;
    public static bool IsMinimized() => MacWindow_IsMinimized() != 0;
    public static bool IsMaximized() => MacWindow_IsMaximized() != 0;

    /// <summary>设置窗口透明度（0.0 = 完全透明，1.0 = 完全不透明）</summary>
    public static bool SetOpacity(float alpha)
        => MacWindow_SetAlpha(UnityEngine.Mathf.Clamp01(alpha)) != 0;

#else
    public static bool GetFrame(out int x, out int y, out int width, out int height)
    { x = y = width = height = 0; Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool SetFrame(int x, int y, int width, int height)   { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool SetTopMost(bool topMost)                         { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool Minimize()    { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool Maximize()    { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool Restore()     { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
    public static bool IsMinimized() => false;
    public static bool IsMaximized() => false;
    public static bool SetOpacity(float alpha) { Debug.LogWarning("[MacWindowPlugin] 仅 macOS 可用"); return false; }
#endif
}
