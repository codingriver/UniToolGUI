using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Mac 窗口控制原生插件封装（位置、大小、置顶）
/// </summary>
public static class MacWindowPlugin
{
#if UNITY_STANDALONE_OSX
    private const string DllName = "MacTray";

    [DllImport(DllName)]
    private static extern int MacWindow_GetFrame(out int x, out int y, out int width, out int height);

    [DllImport(DllName)]
    private static extern int MacWindow_SetFrame(int x, int y, int width, int height);

    [DllImport(DllName)]
    private static extern int MacWindow_SetTopMost(int topMost);

    public static bool GetFrame(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        return MacWindow_GetFrame(out x, out y, out width, out height) != 0;
    }

    public static bool SetFrame(int x, int y, int width, int height)
    {
        return MacWindow_SetFrame(x, y, width, height) != 0;
    }

    public static bool SetTopMost(bool topMost)
    {
        return MacWindow_SetTopMost(topMost ? 1 : 0) != 0;
    }
#else
    public static bool GetFrame(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        Debug.LogWarning("[MacWindowPlugin] 仅在 Mac 平台可用");
        return false;
    }

    public static bool SetFrame(int x, int y, int width, int height)
    {
        Debug.LogWarning("[MacWindowPlugin] 仅在 Mac 平台可用");
        return false;
    }

    public static bool SetTopMost(bool topMost)
    {
        Debug.LogWarning("[MacWindowPlugin] 仅在 Mac 平台可用");
        return false;
    }
#endif
}
