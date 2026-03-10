using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 开机自启（Win/Mac/Linux）
/// </summary>
public static class WindowsStartup
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

    public static string GetStartupKeyName()
    {
        return Application.productName ?? "UnityApp";
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null) return false;
                var value = key.GetValue(GetStartupKeyName());
                return value != null && !string.IsNullOrEmpty(value.ToString());
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsStartup] 检查失败: {ex.Message}");
            return false;
        }
    }

    public static bool EnableStartup(string exePath = null, string keyName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.LogError("[WindowsStartup] 无法获取可执行文件路径");
                return false;
            }
            var name = keyName ?? GetStartupKeyName();
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null) return false;
                key.SetValue(name, "\"" + exePath + "\"");
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WindowsStartup] 添加失败: {ex.Message}");
            return false;
        }
    }

    public static bool DisableStartup(string keyName = null)
    {
        try
        {
            var name = keyName ?? GetStartupKeyName();
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key == null) return false;
                key.DeleteValue(name, false);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsStartup] 移除失败: {ex.Message}");
            return false;
        }
    }

    public static bool ToggleStartup(string exePath = null)
    {
        if (IsStartupEnabled())
            return DisableStartup();
        return EnableStartup(exePath);
    }

#elif UNITY_STANDALONE_OSX
    private static string GetPlistPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "com.unity." + (Application.productName ?? "UnityApp").Replace(" ", "") + ".plist");

    public static string GetStartupKeyName() => Application.productName ?? "UnityApp";

    public static bool IsStartupEnabled() => File.Exists(GetPlistPath());

    public static bool EnableStartup(string exePath = null, string keyName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;
            var label = "com.unity." + (keyName ?? Application.productName ?? "UnityApp").Replace(" ", "");
            var escaped = exePath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            var plist = "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>Label</key><string>" + label + "</string><key>ProgramArguments</key><array><string>" + escaped + "</string></array><key>RunAtLoad</key><true/></dict></plist>";
            var dir = Path.GetDirectoryName(GetPlistPath());
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(GetPlistPath(), plist);
            return true;
        }
        catch (Exception ex) { Debug.LogError($"[Startup] {ex.Message}"); return false; }
    }

    public static bool DisableStartup(string keyName = null)
    {
        try
        {
            var path = GetPlistPath();
            if (File.Exists(path)) { File.Delete(path); return true; }
            return false;
        }
        catch (Exception ex) { Debug.LogWarning($"[Startup] Mac DisableStartup 失败: {ex.Message}"); return false; }
    }

    public static bool ToggleStartup(string exePath = null) => IsStartupEnabled() ? DisableStartup() : EnableStartup(exePath);

#elif UNITY_STANDALONE_LINUX
    private static string GetDesktopPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", (Application.productName ?? "UnityApp").Replace(" ", "") + ".desktop");

    public static string GetStartupKeyName() => Application.productName ?? "UnityApp";

    public static bool IsStartupEnabled() => File.Exists(GetDesktopPath());

    public static bool EnableStartup(string exePath = null, string keyName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return false;
            var dir = Path.GetDirectoryName(GetDesktopPath());
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var appName = keyName ?? Application.productName ?? "UnityApp";
            var quotedExe = exePath.Contains(" ") ? "\"" + exePath + "\"" : exePath;
            var desktop = "[Desktop Entry]\nType=Application\nName=" + appName + "\nExec=" + quotedExe + "\nX-GNOME-Autostart-enabled=true\n";
            File.WriteAllText(GetDesktopPath(), desktop);
            return true;
        }
        catch (Exception ex) { Debug.LogError($"[Startup] {ex.Message}"); return false; }
    }

    public static bool DisableStartup(string keyName = null)
    {
        try
        {
            var path = GetDesktopPath();
            if (File.Exists(path)) { File.Delete(path); return true; }
            return false;
        }
        catch (Exception ex) { Debug.LogWarning($"[Startup] Linux DisableStartup 失败: {ex.Message}"); return false; }
    }

    public static bool ToggleStartup(string exePath = null) => IsStartupEnabled() ? DisableStartup() : EnableStartup(exePath);

#else
    public static string GetStartupKeyName() => "UnityApp";
    public static bool IsStartupEnabled() => false;
    public static bool EnableStartup(string exePath = null, string keyName = null) => false;
    public static bool DisableStartup(string keyName = null) => false;
    public static bool ToggleStartup(string exePath = null) => false;
#endif
}
