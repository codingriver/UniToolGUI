using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// macOS 应用包/主可执行文件定位工具。
/// 统一处理 .app 包路径解析，避免各处重复实现不一致。
/// </summary>
public static class MacAppLocator
{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    public static bool TryGetAppBundlePath(out string appBundlePath)
    {
        foreach (var candidate in EnumerateCandidatePaths())
        {
            var resolved = FindAppBundleFromPath(candidate);
            if (!string.IsNullOrEmpty(resolved) && Directory.Exists(resolved))
            {
                appBundlePath = resolved;
                return true;
            }
        }

        appBundlePath = null;
        return false;
    }

    public static bool TryGetExecutablePath(out string executablePath)
    {
        foreach (var candidate in EnumerateCandidatePaths())
        {
            if (LooksLikeExecutable(candidate))
            {
                executablePath = candidate;
                return true;
            }
        }

        if (!TryGetAppBundlePath(out var appBundlePath))
        {
            executablePath = null;
            return false;
        }

        var macOsDir = Path.Combine(appBundlePath, "Contents", "MacOS");
        if (!Directory.Exists(macOsDir))
        {
            executablePath = null;
            return false;
        }

        var productName = Application.productName;
        if (!string.IsNullOrEmpty(productName))
        {
            var exact = Path.Combine(macOsDir, productName);
            if (LooksLikeExecutable(exact))
            {
                executablePath = exact;
                return true;
            }
        }

        var files = Directory.GetFiles(macOsDir);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (LooksLikeExecutable(file))
            {
                executablePath = file;
                return true;
            }
        }

        executablePath = null;
        return false;
    }

    public static bool StartAppBundleDelayed(string appBundlePath, int delaySeconds = 1)
    {
        if (string.IsNullOrEmpty(appBundlePath) || !Directory.Exists(appBundlePath))
            return false;

        var escapedPath = EscapeForDoubleQuotedBash(appBundlePath);
        var command = $"-c \"/bin/sleep {Math.Max(0, delaySeconds)} && /usr/bin/open \\\"{escapedPath}\\\"\"";
        return ProcessHelper.StartBackground("/bin/bash", command) != null;
    }

    private static string[] EnumerateCandidatePaths()
    {
        string[] values =
        {
            SafeGetCommandLinePath(),
            SafeGetMainModulePath(),
            SafeGetDataPath()
        };

        return values;
    }

    private static string SafeGetCommandLinePath()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
                return args[0];
        }
        catch { }

        return null;
    }

    private static string SafeGetMainModulePath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeGetDataPath()
    {
        try
        {
            return Application.dataPath;
        }
        catch
        {
            return null;
        }
    }

    private static string FindAppBundleFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var current = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
        {
            if (current.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return current;

            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    private static bool LooksLikeExecutable(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        var name = Path.GetFileName(path);
        return !string.IsNullOrEmpty(name) && !name.StartsWith(".", StringComparison.Ordinal);
    }

    private static string EscapeForDoubleQuotedBash(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
#else
    public static bool TryGetAppBundlePath(out string appBundlePath)
    {
        appBundlePath = null;
        return false;
    }

    public static bool TryGetExecutablePath(out string executablePath)
    {
        executablePath = null;
        return false;
    }

    public static bool StartAppBundleDelayed(string appBundlePath, int delaySeconds = 1)
    {
        return false;
    }
#endif
}
