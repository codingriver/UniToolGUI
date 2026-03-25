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
            // 通过 osascript 提权执行 app 内主可执行文件。
            // 用 stdin 传 AppleScript，避免命令行转义错误导致不弹密码框。
            if (!MacAppLocator.TryGetAppBundlePath(out var appPath))
            {
                Debug.LogWarning("[Admin] 未找到 .app 路径，无法提升权限");
                return false;
            }

            if (!MacAppLocator.TryGetExecutablePath(out var exePath))
            {
                Debug.LogWarning("[Admin] 未找到 app 主可执行文件，appPath: " + appPath);
                return false;
            }

            string macOsDir = System.IO.Path.GetDirectoryName(exePath);
            string safeExe = exePath.Replace("'", "'\\''");
            string safeArgs = (args ?? string.Empty).Replace("'", "'\\''");
            string safeWorkDir = macOsDir.Replace("'", "'\\''");
            string shell = $"cd '{safeWorkDir}' && /usr/bin/nohup '{safeExe}' {safeArgs} >/tmp/unitool_admin_restart.log 2>&1 &";
            string apple = $"do shell script \"{shell.Replace("\"", "\\\"")}\" with administrator privileges";

            Debug.Log("[Admin] 准备提权重启");
            Debug.Log("[Admin] appPath=" + appPath);
            Debug.Log("[Admin] exePath=" + exePath);

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var proc = Process.Start(psi))
            {
                if (proc == null)
                {
                    Debug.LogWarning("[Admin] osascript 启动失败");
                    return false;
                }

                proc.StandardInput.WriteLine(apple);
                proc.StandardInput.Close();

                bool exited = proc.WaitForExit(30000);
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                if (!exited)
                {
                    try { proc.Kill(); } catch { }
                    Debug.LogWarning("[Admin] osascript 超时，未完成提权");
                    return false;
                }

                if (proc.ExitCode != 0)
                {
                    Debug.LogWarning($"[Admin] osascript 失败 code={proc.ExitCode}, err={stderr}, out={stdout}");
                    return false;
                }

                Debug.Log($"[Admin] osascript 成功, out={stdout}");
            }

            try { NativePlatform.SingleInstance.Release(); } catch { }
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
