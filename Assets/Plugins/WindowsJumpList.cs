using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windows Jump List（任务栏右键菜单）（仅 Windows 平台有效）
/// 支持：添加最近文档、自定义任务
/// </summary>
public static class WindowsJumpList
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHAddToRecentDocs(uint uFlags, [MarshalAs(UnmanagedType.LPWStr)] string pv);

    [DllImport("shell32.dll")]
    private static extern void SHAddToRecentDocs(uint uFlags, IntPtr pv);

    [DllImport("shell32.dll")]
    private static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    private const uint SHARD_PATH = 0x02;
    private const uint SHARD_PIDL = 0x01;

    /// <summary>
    /// 添加文件到「最近」列表（会出现在任务栏图标的 Jump List 中）
    /// </summary>
    public static void AddToRecentDocs(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try
        {
            SHAddToRecentDocs(SHARD_PATH, filePath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsJumpList] AddToRecentDocs 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除最近文档列表（需重启应用后生效）
    /// 注意：此 API 会清除当前用户的全局最近文档，非仅本应用
    /// </summary>
    public static void ClearRecentDocs()
    {
        try
        {
            SHAddToRecentDocs(SHARD_PIDL, IntPtr.Zero); // NULL PIDL 表示清除
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsJumpList] ClearRecentDocs 失败: {ex.Message}");
        }
    }

    // 注：完整的自定义 Jump List（任务、分类等）需使用 ICustomDestinationList COM 接口，实现较复杂
    // 可参考：https://docs.microsoft.com/en-us/windows/win32/shell/taskbar-extensions

#else
    public static void AddToRecentDocs(string filePath)
    {
        Debug.LogWarning("WindowsJumpList 仅在 Windows 平台可用");
    }

    public static void ClearRecentDocs()
    {
        Debug.LogWarning("WindowsJumpList 仅在 Windows 平台可用");
    }
#endif
}
