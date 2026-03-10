using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows 全局热键（仅 Windows 平台有效）
/// </summary>
public static class WindowsHotkey
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    /// <summary>
    /// 修饰键
    /// </summary>
    [Flags]
    public enum ModifierKeys:uint
    {
        None = 0,
        Alt = MOD_ALT,
        Ctrl = MOD_CONTROL,
        Shift = MOD_SHIFT,
        Win = MOD_WIN,
    }

    /// <summary>
    /// 注册全局热键
    /// </summary>
    /// <param name="hwnd">窗口句柄，可用 GetUnityWindowHandle()</param>
    /// <param name="id">热键 ID（0x0000-0xBFFF），需唯一</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="virtualKey">虚拟键码，如 VK_F1=0x70, 'A'=0x41</param>
    /// <returns>是否成功</returns>
    public static bool Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey)
    {
        return RegisterHotKey(hwnd, id, (uint)modifiers, virtualKey);
    }

    /// <summary>
    /// 取消注册热键
    /// </summary>
    public static bool Unregister(IntPtr hwnd, int id)
    {
        return UnregisterHotKey(hwnd, id);
    }

    /// <summary>
    /// 获取 Unity 主窗口句柄（用于热键注册）
    /// </summary>
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

    /// <summary>
    /// 常用虚拟键码
    /// </summary>
    public static class VK
    {
        public const uint F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74;
        public const uint F6 = 0x75, F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79;
        public const uint F11 = 0x7A, F12 = 0x7B;
        public const uint Space = 0x20;
        public const uint Escape = 0x1B;
    }

#else
    [Flags]
    public enum ModifierKeys { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

    public static bool Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey)
    {
        Debug.LogWarning("WindowsHotkey 仅在 Windows 平台可用");
        return false;
    }

    public static bool Unregister(IntPtr hwnd, int id)
    {
        return false;
    }

    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;

    public static class VK
    {
        public const uint F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74;
        public const uint F6 = 0x75, F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79;
        public const uint F11 = 0x7A, F12 = 0x7B;
        public const uint Space = 0x20;
        public const uint Escape = 0x1B;
    }
#endif
}
