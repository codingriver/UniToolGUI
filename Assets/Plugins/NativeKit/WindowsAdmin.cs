using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 管理员/root 权限检测与提权重启（Win/Mac/Linux）
/// </summary>
public static class WindowsAdmin
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool IsUserAnAdmin();

    public static bool IsRunningAsAdmin()
    {
        try { return IsUserAnAdmin(); }
        catch { return false; }
    }

    public static bool RestartAsAdmin(string args = null)
    {
        if (IsRunningAsAdmin()) return false;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule?.FileName,
                Arguments = args ?? "",
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(startInfo);
            Application.Quit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Admin] 提升权限失败: {ex.Message}");
            return false;
        }
    }

    public static string GetCurrentUserName()
    {
        try { return Environment.UserName; }
        catch { return "unknown"; }
    }

    public static string GetCurrentIdentityDisplay()
    {
        return IsRunningAsAdmin()
            ? $"{GetCurrentUserName()} (Administrator)"
            : $"{GetCurrentUserName()} (User)";
    }

#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    [DllImport("libc", SetLastError = true)]
    private static extern uint getuid();

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    public static bool IsRunningAsAdmin()
    {
        try { return geteuid() == 0; }
        catch { return false; }
    }

    public static bool RestartAsAdmin(string args = null)
    {
        if (IsRunningAsAdmin()) return false;

#if UNITY_STANDALONE_LINUX
        try
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe)) return false;
            Process.Start(new ProcessStartInfo
            {
                FileName = "pkexec",
                Arguments = "\"" + exe + "\" " + (args ?? ""),
                UseShellExecute = true
            });
            Application.Quit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Admin] Linux 提升权限失败: {ex.Message}");
            return false;
        }
#elif UNITY_STANDALONE_OSX
        try
        {
            // 在 macOS 上直接以管理员身份启动 app 内可执行文件
            // 注意：GUI 进程在新系统上可能仍由普通用户会话承载；
            // 最终是否提权成功以 geteuid()==0 为准。
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
            {
                Debug.LogWarning("[Admin] 未找到当前可执行文件路径，无法提升权限");
                return false;
            }

            string escapedExe = exePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string escapedArgs = (args ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
            string shellCmd = $"/usr/bin/nohup \"{escapedExe}\" {escapedArgs} >/tmp/unitool_admin_restart.log 2>&1 &";
            string script = $"do shell script \"{shellCmd.Replace("\"", "\\\"")}\" with administrator privileges";

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = $"-e \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            Process.Start(psi);
            Application.Quit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Admin] macOS 提升权限失败: {ex.Message}");
            return false;
        }
#else
        return false;
#endif
    }

    public static string GetCurrentUserName()
    {
        try
        {
#if UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            var who = ProcessHelper.RunAndRead("/usr/bin/id", "-un", 1500)?.Trim();
            if (!string.IsNullOrEmpty(who)) return who;
#endif
            return Environment.UserName;
        }
        catch
        {
            return Environment.UserName;
        }
    }

    public static string GetCurrentIdentityDisplay()
    {
        try
        {
            uint uid = getuid();
            uint euid = geteuid();
            string user = GetCurrentUserName();
            string role = euid == 0 ? "root" : "user";
            return $"{user} ({role}, uid={uid}, euid={euid})";
        }
        catch
        {
            return GetCurrentUserName();
        }
    }

#else
    public static bool IsRunningAsAdmin() => false;
    public static bool RestartAsAdmin(string args = null) => false;
    public static string GetCurrentUserName() => Environment.UserName;
    public static string GetCurrentIdentityDisplay() => GetCurrentUserName();
#endif
}
