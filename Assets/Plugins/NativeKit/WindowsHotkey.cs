using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows 全局热键（仅 Windows 平台有效）。
/// 热键触发时，通过 <see cref="TrayIconService.OnHotkeyPressed"/> 事件通知。
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

    private const uint MOD_ALT      = 0x0001;
    private const uint MOD_CONTROL  = 0x0002;
    private const uint MOD_SHIFT    = 0x0004;
    private const uint MOD_WIN      = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000; // 防止按住时重复触发（Windows 7+）

    // ────────────────────────────────────────
    // 修饰键枚举
    // ────────────────────────────────────────
    /// <summary>修饰键（可用 | 组合）</summary>
    [Flags]
    public enum ModifierKeys : uint
    {
        None     = 0,
        Alt      = MOD_ALT,
        Ctrl     = MOD_CONTROL,
        Shift    = MOD_SHIFT,
        Win      = MOD_WIN,
        NoRepeat = MOD_NOREPEAT,
    }

    // ────────────────────────────────────────
    // 公共 API
    // ────────────────────────────────────────

    /// <summary>
    /// 注册全局热键。
    /// </summary>
    /// <param name="hwnd">窗口句柄，可用 <see cref="GetUnityWindowHandle"/></param>
    /// <param name="id">热键 ID（0x0000–0xBFFF），需唯一</param>
    /// <param name="modifiers">修饰键（可用 | 组合）</param>
    /// <param name="virtualKey">虚拟键码，推荐使用 <see cref="VK"/> 常量</param>
    /// <returns>是否成功；失败时可通过 Marshal.GetLastWin32Error() 查看原因</returns>
    public static bool Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey)
    {
        return RegisterHotKey(hwnd, id, (uint)modifiers, virtualKey);
    }

    /// <summary>注销热键。</summary>
    public static bool Unregister(IntPtr hwnd, int id)
    {
        return UnregisterHotKey(hwnd, id);
    }

    /// <summary>获取 Unity 主窗口句柄（用于热键注册）。</summary>
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

    // ────────────────────────────────────────
    // 虚拟键码常量
    // ────────────────────────────────────────
    /// <summary>
    /// 常用虚拟键码（Windows Virtual-Key Codes）。
    /// 完整列表参见：https://docs.microsoft.com/windows/win32/inputdev/virtual-key-codes
    /// </summary>
    public static class VK
    {
        // ── 功能键 ──
        public const uint F1  = 0x70; public const uint F2  = 0x71;
        public const uint F3  = 0x72; public const uint F4  = 0x73;
        public const uint F5  = 0x74; public const uint F6  = 0x75;
        public const uint F7  = 0x76; public const uint F8  = 0x77;
        public const uint F9  = 0x78; public const uint F10 = 0x79;
        public const uint F11 = 0x7A; public const uint F12 = 0x7B;
        public const uint F13 = 0x7C; public const uint F14 = 0x7D;
        public const uint F15 = 0x7E; public const uint F16 = 0x7F;
        public const uint F17 = 0x80; public const uint F18 = 0x81;
        public const uint F19 = 0x82; public const uint F20 = 0x83;
        public const uint F21 = 0x84; public const uint F22 = 0x85;
        public const uint F23 = 0x86; public const uint F24 = 0x87;

        // ── 字母键 A–Z ──
        public const uint A = 0x41; public const uint B = 0x42;
        public const uint C = 0x43; public const uint D = 0x44;
        public const uint E = 0x45; public const uint F = 0x46;
        public const uint G = 0x47; public const uint H = 0x48;
        public const uint I = 0x49; public const uint J = 0x4A;
        public const uint K = 0x4B; public const uint L = 0x4C;
        public const uint M = 0x4D; public const uint N = 0x4E;
        public const uint O = 0x4F; public const uint P = 0x50;
        public const uint Q = 0x51; public const uint R = 0x52;
        public const uint S = 0x53; public const uint T = 0x54;
        public const uint U = 0x55; public const uint V = 0x56;
        public const uint W = 0x57; public const uint X = 0x58;
        public const uint Y = 0x59; public const uint Z = 0x5A;

        // ── 数字键（主键盘）0–9 ──
        public const uint D0 = 0x30; public const uint D1 = 0x31;
        public const uint D2 = 0x32; public const uint D3 = 0x33;
        public const uint D4 = 0x34; public const uint D5 = 0x35;
        public const uint D6 = 0x36; public const uint D7 = 0x37;
        public const uint D8 = 0x38; public const uint D9 = 0x39;

        // ── 小键盘 Numpad 0–9 ──
        public const uint Numpad0 = 0x60; public const uint Numpad1 = 0x61;
        public const uint Numpad2 = 0x62; public const uint Numpad3 = 0x63;
        public const uint Numpad4 = 0x64; public const uint Numpad5 = 0x65;
        public const uint Numpad6 = 0x66; public const uint Numpad7 = 0x67;
        public const uint Numpad8 = 0x68; public const uint Numpad9 = 0x69;
        public const uint Multiply  = 0x6A; // Numpad *
        public const uint Add        = 0x6B; // Numpad +
        public const uint Separator  = 0x6C; // Numpad Enter (locale)
        public const uint Subtract   = 0x6D; // Numpad -
        public const uint Decimal    = 0x6E; // Numpad .
        public const uint Divide     = 0x6F; // Numpad /

        // ── 方向键 ──
        public const uint Left  = 0x25;
        public const uint Up    = 0x26;
        public const uint Right = 0x27;
        public const uint Down  = 0x28;

        // ── 导航键 ──
        public const uint Home     = 0x24;
        public const uint End      = 0x23;
        public const uint PageUp   = 0x21;
        public const uint PageDown = 0x22;
        public const uint Insert   = 0x2D;
        public const uint Delete   = 0x2E;

        // ── 控制键 ──
        public const uint Space     = 0x20;
        public const uint Enter     = 0x0D;
        public const uint Escape    = 0x1B;
        public const uint Tab       = 0x09;
        public const uint BackSpace = 0x08;
        public const uint CapsLock  = 0x14;
        public const uint NumLock   = 0x90;
        public const uint ScrollLock = 0x91;
        public const uint PrintScreen = 0x2C;
        public const uint Pause     = 0x13;

        // ── 媒体键 ──
        public const uint MediaNextTrack  = 0xB0;
        public const uint MediaPrevTrack  = 0xB1;
        public const uint MediaStop       = 0xB2;
        public const uint MediaPlayPause  = 0xB3;
        public const uint VolumeMute      = 0xAD;
        public const uint VolumeDown      = 0xAE;
        public const uint VolumeUp        = 0xAF;

        // ── 浏览器键 ──
        public const uint BrowserBack    = 0xA6;
        public const uint BrowserForward = 0xA7;
        public const uint BrowserRefresh = 0xA8;
        public const uint BrowserStop    = 0xA9;
        public const uint BrowserSearch  = 0xAA;
        public const uint BrowserHome    = 0xAC;

        // ── 符号键（美式键盘布局）──
        public const uint OemSemicolon  = 0xBA; // ;
        public const uint OemPlus       = 0xBB; // =
        public const uint OemComma      = 0xBC; // ,
        public const uint OemMinus      = 0xBD; // -
        public const uint OemPeriod     = 0xBE; // .
        public const uint OemQuestion   = 0xBF; // /
        public const uint OemTilde      = 0xC0; // `
        public const uint OemOpenBracket  = 0xDB; // [
        public const uint OemBackslash    = 0xDC; // \
        public const uint OemCloseBracket = 0xDD; // ]
        public const uint OemQuote        = 0xDE; // '
    }

#else
    // ── 非 Windows 平台：空实现 ──

    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8, NoRepeat = 0x4000,
    }

    public static bool Register(IntPtr hwnd, int id, ModifierKeys modifiers, uint virtualKey)
    {
        Debug.LogWarning("[WindowsHotkey] 全局热键仅在 Windows 平台可用");
        return false;
    }

    public static bool Unregister(IntPtr hwnd, int id) => false;

    public static IntPtr GetUnityWindowHandle() => IntPtr.Zero;

    public static class VK
    {
        public const uint F1=0x70; public const uint F2=0x71; public const uint F3=0x72;
        public const uint F4=0x73; public const uint F5=0x74; public const uint F6=0x75;
        public const uint F7=0x76; public const uint F8=0x77; public const uint F9=0x78;
        public const uint F10=0x79; public const uint F11=0x7A; public const uint F12=0x7B;
        public const uint A=0x41; public const uint B=0x42; public const uint C=0x43;
        public const uint D=0x44; public const uint E=0x45; public const uint F=0x46;
        public const uint G=0x47; public const uint H=0x48; public const uint I=0x49;
        public const uint J=0x4A; public const uint K=0x4B; public const uint L=0x4C;
        public const uint M=0x4D; public const uint N=0x4E; public const uint O=0x4F;
        public const uint P=0x50; public const uint Q=0x51; public const uint R=0x52;
        public const uint S=0x53; public const uint T=0x54; public const uint U=0x55;
        public const uint V=0x56; public const uint W=0x57; public const uint X=0x58;
        public const uint Y=0x59; public const uint Z=0x5A;
        public const uint D0=0x30; public const uint D1=0x31; public const uint D2=0x32;
        public const uint D3=0x33; public const uint D4=0x34; public const uint D5=0x35;
        public const uint D6=0x36; public const uint D7=0x37; public const uint D8=0x38;
        public const uint D9=0x39;
        public const uint Left=0x25; public const uint Up=0x26;
        public const uint Right=0x27; public const uint Down=0x28;
        public const uint Home=0x24; public const uint End=0x23;
        public const uint PageUp=0x21; public const uint PageDown=0x22;
        public const uint Insert=0x2D; public const uint Delete=0x2E;
        public const uint Space=0x20; public const uint Enter=0x0D;
        public const uint Escape=0x1B; public const uint Tab=0x09;
        public const uint BackSpace=0x08;
        public const uint MediaNextTrack=0xB0; public const uint MediaPrevTrack=0xB1;
        public const uint MediaStop=0xB2; public const uint MediaPlayPause=0xB3;
        public const uint VolumeMute=0xAD; public const uint VolumeDown=0xAE;
        public const uint VolumeUp=0xAF;
    }
#endif
}
