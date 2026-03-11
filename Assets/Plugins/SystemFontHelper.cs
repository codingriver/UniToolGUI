using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 各平台系统字体候选路径，按语言分组、按优先级排列。
/// </summary>
public static class SystemFontHelper
{
    public enum FontGroup
    {
        CJK,
        Latin,
        Arabic,
        Thai,
        Fallback
    }

    private static readonly Dictionary<FontGroup, string[]> CandidateMap =
        new Dictionary<FontGroup, string[]>
        {
            [FontGroup.CJK] = new[]
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                @"C:\Windows\Fonts\msyh.ttc",
                @"C:\Windows\Fonts\msyhbd.ttc",
                @"C:\Windows\Fonts\msjh.ttc",
                @"C:\Windows\Fonts\simsun.ttc",
                @"C:\Windows\Fonts\simhei.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                "/System/Library/Fonts/PingFang.ttc",
                "/System/Library/Fonts/STHeiti Medium.ttc",
                "/Library/Fonts/Arial Unicode MS.ttf",
#elif UNITY_ANDROID
                "/system/fonts/NotoSansCJK-Regular.ttc",
                "/system/fonts/DroidSansFallback.ttf",
#elif UNITY_IOS
                "/System/Library/Fonts/PingFang.ttc",
#else
                "",
#endif
            },
            [FontGroup.Latin] = new[]
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                @"C:\Windows\Fonts\segoeui.ttf",
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\tahoma.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                "/System/Library/Fonts/Helvetica.ttc",
#elif UNITY_ANDROID
                "/system/fonts/Roboto-Regular.ttf",
#elif UNITY_IOS
                "/System/Library/Fonts/Helvetica.ttc",
#else
                "",
#endif
            },
            [FontGroup.Arabic] = new[]
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\tahoma.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                "/System/Library/Fonts/GeezaPro.ttc",
#elif UNITY_ANDROID
                "/system/fonts/NotoNaskhArabic-Regular.ttf",
#elif UNITY_IOS
                "/System/Library/Fonts/GeezaPro.ttc",
#else
                "",
#endif
            },
            [FontGroup.Thai] = new[]
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                @"C:\Windows\Fonts\tahoma.ttf",
                @"C:\Windows\Fonts\cordia.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                "/System/Library/Fonts/Thonburi.ttf",
#elif UNITY_ANDROID
                "/system/fonts/NotoSansThai-Regular.ttf",
#elif UNITY_IOS
                "/System/Library/Fonts/Thonburi.ttf",
#else
                "",
#endif
            },
            [FontGroup.Fallback] = new[]
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                @"C:\Windows\Fonts\arial.ttf",
                @"C:\Windows\Fonts\segoeui.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                "/Library/Fonts/Arial Unicode MS.ttf",
                "/System/Library/Fonts/Helvetica.ttc",
#elif UNITY_ANDROID
                "/system/fonts/Roboto-Regular.ttf",
#elif UNITY_IOS
                "/System/Library/Fonts/Helvetica.ttc",
#else
                "",
#endif
            },
        };

    /// <summary>
    /// 获取分组下所有实际存在的字体路径
    /// </summary>
    public static List<string> GetAllExistingPaths(FontGroup group)
    {
        var result = new List<string>();
        if (!CandidateMap.TryGetValue(group, out var paths)) return result;
        foreach (var p in paths)
            if (!string.IsNullOrEmpty(p) && File.Exists(p))
                result.Add(p);
        return result;
    }

    /// <summary>
    /// 获取分组下第一个存在的字体路径
    /// </summary>
    public static string GetFirstExistingPath(FontGroup group)
    {
        var list = GetAllExistingPaths(group);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// 兼容旧接口
    /// </summary>
    public static string[] GetCandidatePaths()
        => CandidateMap.TryGetValue(FontGroup.CJK, out var p)
            ? p : System.Array.Empty<string>();

    /// <summary>
    /// 列出系统上所有已安装字体名称（用于诊断）
    /// </summary>
    public static void LogAllInstalledFonts()
    {
        var names = Font.GetOSInstalledFontNames();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[SystemFontHelper] 系统已安装字体（共 {names.Length} 个）:");
        foreach (var n in names) sb.AppendLine($"  {n}");
        Debug.Log(sb.ToString());
    }
}
