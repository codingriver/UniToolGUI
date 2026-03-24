using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Mac 系统托盘原生插件封装（通过 P/Invoke 调用 MacTray.bundle）
/// 注意：Init() 必须在 Unity 主线程调用，否则回调可能不在主线程执行。
/// </summary>
public static class MacTrayPlugin
{
#if UNITY_STANDALONE_OSX
    private const string DllName = "MacTray";

    [DllImport(DllName)]
    private static extern int MacTray_Init();

    [DllImport(DllName)]
    private static extern void MacTray_Shutdown();

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    private static extern void MacTray_SetTooltip(string tooltip);

    [DllImport(DllName)]
    private static extern void MacTray_SetMenu(IntPtr items);

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    private static extern void MacTray_ShowBalloon(string title, string message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MenuCallbackDelegate(int index);

    [DllImport(DllName)]
    private static extern void MacTray_SetMenuCallback(MenuCallbackDelegate callback);

    [DllImport(DllName)]
    private static extern void MacTray_ShowMainWindow();

    [DllImport(DllName, CharSet = CharSet.Ansi)]
    private static extern int MacTray_SetIcon(string imagePath);

    [DllImport(DllName)]
    private static extern int MacTray_SetIconFromData(byte[] pngData, int length);

    [DllImport(DllName)]
    private static extern void MacTray_SetHideOnClose(int enable);

    private static MenuCallbackDelegate _callbackDelegate;
    private static SynchronizationContext _mainContext;
    private static List<TrayMenuItem> _menuItems;
    private static int[] _indexMap;
    private static readonly object _lock = new object();

    public static bool Init()
    {
        try
        {
            _mainContext = SynchronizationContext.Current ?? new SynchronizationContext();
            return MacTray_Init() != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    public static void Shutdown()
    {
        MacTray_SetMenuCallback(null);
        MacTray_Shutdown();
    }

    public static void SetTooltip(string tooltip)
    {
        MacTray_SetTooltip(tooltip ?? "");
    }

    public static void SetMenu(List<TrayMenuItem> items)
    {
        lock (_lock)
        {
            _menuItems = items;
        }

        if (items == null || items.Count == 0)
        {
            MacTray_SetMenu(IntPtr.Zero);
            return;
        }

        var nonSepItems = new List<TrayMenuItem>();
        foreach (var item in items)
        {
            if (!item.IsSeparator) nonSepItems.Add(item);
        }
        var indexMap = new int[nonSepItems.Count];
        int idx = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].IsSeparator)
                indexMap[idx++] = i;
        }

        lock (_lock)
        {
            _indexMap = indexMap;
        }

        var strings = new List<string>();
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                strings.Add("---");
                continue;
            }

            var title = item.Text ?? "";
            if (item.IsToggle && item.Checked)
                title = "\u2713 " + title;
            strings.Add(title);
        }

        var ptrs = new IntPtr[strings.Count + 1];
        IntPtr arrPtr = IntPtr.Zero;
        try
        {
            for (int i = 0; i < strings.Count; i++)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(strings[i] + "\0");
                ptrs[i] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, ptrs[i], bytes.Length);
            }
            ptrs[strings.Count] = IntPtr.Zero;

            arrPtr = Marshal.AllocCoTaskMem(IntPtr.Size * ptrs.Length);
            for (int i = 0; i < ptrs.Length; i++)
                Marshal.WriteIntPtr(arrPtr, i * IntPtr.Size, ptrs[i]);

            _callbackDelegate = OnMenuClicked;
            MacTray_SetMenuCallback(_callbackDelegate);
            MacTray_SetMenu(arrPtr);
        }
        finally
        {
            if (arrPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(arrPtr);
            for (int i = 0; i < strings.Count; i++)
            {
                if (ptrs[i] != IntPtr.Zero) Marshal.FreeCoTaskMem(ptrs[i]);
            }
        }
    }

    [AOT.MonoPInvokeCallback(typeof(MenuCallbackDelegate))]
    private static void OnMenuClicked(int index)
    {
        List<TrayMenuItem> items;
        int[] map;
        lock (_lock)
        {
            items = _menuItems;
            map = _indexMap;
        }
        if (items == null || map == null || index < 0 || index >= map.Length) return;
        int itemIndex = map[index];
        if (itemIndex < 0 || itemIndex >= items.Count) return;
        var item = items[itemIndex];
        _mainContext?.Post(_ => item.Callback?.Invoke(), null);
    }

    public static void ShowBalloon(string title, string message)
    {
        MacTray_ShowBalloon(title ?? "", message ?? "");
    }

    public static void ShowMainWindow()
    {
        MacTray_ShowMainWindow();
    }

    /// <summary>从文件路径设置托盘图标（PNG/ICNS，建议 18x18 px）。</summary>
    public static bool SetIcon(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return false;
        return MacTray_SetIcon(imagePath) != 0;
    }

    /// <summary>从 PNG 字节数组设置托盘图标（可由 Texture2D.EncodeToPNG() 提供）。</summary>
    public static bool SetIconFromData(byte[] pngData)
    {
        if (pngData == null || pngData.Length == 0) return false;
        return MacTray_SetIconFromData(pngData, pngData.Length) != 0;
    }

    /// <summary>
    /// 设置点击关闭按钮时的行为。
    /// enable=true：隐藏窗口（最小化到托盘）；enable=false：正常关闭。
    /// 必须在 Init() 之后调用。
    /// </summary>
    public static void SetHideOnClose(bool enable)
    {
        MacTray_SetHideOnClose(enable ? 1 : 0);
    }
#endif
}
