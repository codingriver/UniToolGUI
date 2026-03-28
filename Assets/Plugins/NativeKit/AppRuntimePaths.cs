using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 应用运行时路径统一入口。
/// 在 macOS 长期以管理员/高权限账号运行时，避免普通用户与高权限账号因工作目录不同而写到不同位置。
/// </summary>
public static class AppRuntimePaths
{
    public static string GetDesktopDataDir()
    {
#if UNITY_ANDROID || UNITY_IOS
        return Application.persistentDataPath;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return EnsureDirectory(Path.Combine("/Users/Shared", GetProductDirName()));
#else
        return EnsureDirectory(Environment.CurrentDirectory);
#endif
    }

    public static string GetLogsDir()
    {
        return EnsureDirectory(Path.Combine(GetDesktopDataDir(), "logs"));
    }

    public static string GetLogFilePath(string fileName)
    {
        return Path.Combine(GetLogsDir(), fileName);
    }

    public static string GetDataFilePath(string fileName)
    {
        return Path.Combine(GetDesktopDataDir(), fileName);
    }

    private static string GetProductDirName()
    {
        return string.IsNullOrWhiteSpace(Application.productName)
            ? "UniToolGUI"
            : Application.productName.Trim();
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
