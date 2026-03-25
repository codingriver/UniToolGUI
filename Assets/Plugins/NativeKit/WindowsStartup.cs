using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 开机自启（Win/Mac/Linux）
/// </summary>
public static class WindowsStartup
{
    /// <summary>开机自启状态变化时广播（参数=新状态 true/false），在主线程触发</summary>
    public static event Action<bool> OnStartupChanged;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string RunKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

    public static string GetStartupKeyName()
    {
        return Application.productName ?? "UnityApp";
    }

    /// <summary>
    /// 安全获取当前进程的可执行文件路径。
    /// 优先使用 Environment.GetCommandLineArgs()[0]（Unity 打包后最可靠），
    /// 回退到 Process.MainModule，最终回退到 Application.dataPath 推算。
    /// </summary>
    public static string GetCurrentExePath()
    {
        // 1. 命令行第一个参数（最可靠，打包后即 exe 路径）
        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                string p = args[0];
                if (File.Exists(p)) return p;
            }
        }
        catch { }

        // 2. Process.MainModule（Editor 下可能抛异常）
        try
        {
            string p = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
        }
        catch { }

        // 3. Application.dataPath 推算（_Data 目录同级的 .exe）
        try
        {
            // dataPath = "X:/Game/Game_Data" -> exe = "X:/Game/Game.exe"
            string dataPath = Application.dataPath;
            string dir  = Path.GetDirectoryName(dataPath);
            string name = Path.GetFileName(dataPath); // "Game_Data"
            if (name.EndsWith("_Data"))
            {
                string exeName = name.Substring(0, name.Length - 5) + ".exe";
                string full    = Path.Combine(dir, exeName);
                if (File.Exists(full)) return full;
            }
        }
        catch { }

        Debug.LogError("[WindowsStartup] 无法获取可执行文件路径");
        return null;
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using (var key = RegistryHelper.OpenCurrentUserKey(RunKeyPath, false))
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
                exePath = GetCurrentExePath();
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.LogError("[WindowsStartup] 无法获取可执行文件路径");
                return false;
            }
            var name = keyName ?? GetStartupKeyName();
            using (var key = RegistryHelper.OpenCurrentUserKey(RunKeyPath, true))
            {
                if (key == null) return false;
                key.SetValue(name, "\"" + exePath + "\"");
                OnStartupChanged?.Invoke(true);
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
            using (var key = RegistryHelper.OpenCurrentUserKey(RunKeyPath, true))
            {
                if (key == null) return false;
                key.DeleteValue(name, false);
                OnStartupChanged?.Invoke(false);
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

    public static string GetCurrentExePath()
    {
        if (MacAppLocator.TryGetExecutablePath(out var executablePath))
            return executablePath;

        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]) && File.Exists(args[0]))
                return args[0];
        }
        catch { }

        try
        {
            var mainModule = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModule) && File.Exists(mainModule))
                return mainModule;
        }
        catch { }

        try
        {
            var dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                var contentsDir = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(contentsDir))
                {
                    var macOsDir = Path.Combine(contentsDir, "MacOS");
                    var exeName = Application.productName ?? "UnityApp";
                    var candidate = Path.Combine(macOsDir, exeName);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
        }
        catch { }

        Debug.LogError("[Startup] 无法获取 macOS 可执行文件路径");
        return null;
    }

    public static bool EnableStartup(string exePath = null, string keyName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(exePath))
                exePath = GetCurrentExePath();
            if (string.IsNullOrEmpty(exePath)) return false;
            var label = "com.unity." + (keyName ?? Application.productName ?? "UnityApp").Replace(" ", "");
            var escaped = exePath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            var plist = "<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>Label</key><string>" + label + "</string><key>ProgramArguments</key><array><string>" + escaped + "</string></array><key>RunAtLoad</key><true/></dict></plist>";
            var plistPath = GetPlistPath();
            var dir = Path.GetDirectoryName(plistPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(plistPath, plist);
            // 仅写入 LaunchAgents 配置，不在勾选当下主动 bootstrap。
            // 否则 launchctl 会立刻拉起一个新实例，表现为“勾选开机自启后启动了第二个程序”。
            OnStartupChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex) { Debug.LogError($"[Startup] {ex.Message}"); return false; }
    }

    public static bool DisableStartup(string keyName = null)
    {
        try
        {
            var path = GetPlistPath();
            if (File.Exists(path))
            {
                File.Delete(path);
                OnStartupChanged?.Invoke(false);
                return true;
            }
            OnStartupChanged?.Invoke(false);
            return false;
        }
        catch (Exception ex) { Debug.LogWarning($"[Startup] Mac DisableStartup 失败: {ex.Message}"); return false; }
    }

    public static bool ToggleStartup(string exePath = null) => IsStartupEnabled() ? DisableStartup() : EnableStartup(exePath);

    private static void TryLaunchCtl(string action, string plistPath, string label)
    {
        try
        {
            if (string.Equals(action, "bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                int code = ProcessHelper.Run("launchctl", "bootstrap gui/" + GetUserId() + " " + ProcessHelper.Quote(plistPath), 5000);
                if (code != 0)
                    ProcessHelper.Run("launchctl", "load -w " + ProcessHelper.Quote(plistPath), 5000);
                return;
            }

            if (string.Equals(action, "bootout", StringComparison.OrdinalIgnoreCase))
            {
                int code = ProcessHelper.Run("launchctl", "bootout gui/" + GetUserId() + "/" + label, 5000);
                if (code != 0)
                    ProcessHelper.Run("launchctl", "unload -w " + ProcessHelper.Quote(plistPath), 5000);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Startup] launchctl 调用失败: " + ex.Message);
        }
    }

    private static string GetUserId()
    {
        try
        {
            var uid = ProcessHelper.RunAndRead("id", "-u", 3000);
            return string.IsNullOrWhiteSpace(uid) ? "501" : uid.Trim();
        }
        catch
        {
            return "501";
        }
    }

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
            OnStartupChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex) { Debug.LogError($"[Startup] {ex.Message}"); return false; }
    }

    public static bool DisableStartup(string keyName = null)
    {
        try
        {
            var path = GetDesktopPath();
            if (File.Exists(path))
            {
                File.Delete(path);
                OnStartupChanged?.Invoke(false);
                return true;
            }
            OnStartupChanged?.Invoke(false);
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
