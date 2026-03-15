using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 剪贴板操作（Win/Mac/Linux）
/// </summary>
public static class WindowsClipboard
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>
    /// 获取剪贴板文本
    /// </summary>
    public static string GetText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            IntPtr hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == IntPtr.Zero) return null;
            IntPtr pData = GlobalLock(hData);
            if (pData == IntPtr.Zero) return null;
            try
            {
                return Marshal.PtrToStringUni(pData);
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// 设置剪贴板文本
    /// </summary>
    public static bool SetText(string text)
    {
        if (text == null) text = "";
        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            EmptyClipboard();
            int byteCount = (text.Length + 1) * 2;
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (hMem == IntPtr.Zero) return false;
            IntPtr pMem = GlobalLock(hMem);
            if (pMem == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return false;
            }
            Marshal.Copy((text + "\0").ToCharArray(), 0, pMem, text.Length + 1);
            GlobalUnlock(hMem);
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return false;
            }
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// 检查剪贴板是否包含文本
    /// </summary>
    public static bool HasText()
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            return GetClipboardData(CF_UNICODETEXT) != IntPtr.Zero;
        }
        finally
        {
            CloseClipboard();
        }
    }

#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    public static string GetText()
    {
        try
        {
            return GUIUtility.systemCopyBuffer;
        }
        catch { return null; }
    }

    public static bool SetText(string text)
    {
        try
        {
            GUIUtility.systemCopyBuffer = text ?? "";
            return true;
        }
        catch { return false; }
    }

    public static bool HasText()
    {
        try
        {
            return !string.IsNullOrEmpty(GUIUtility.systemCopyBuffer);
        }
        catch { return false; }
    }

#else
    public static string GetText() => null;
    public static bool SetText(string text) => false;
    public static bool HasText() => false;
#endif
}
