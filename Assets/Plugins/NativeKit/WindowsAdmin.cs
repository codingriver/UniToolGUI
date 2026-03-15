using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 管理员/root 权限检测（Win/Mac/Linux）
/// </summary>
public static class WindowsAdmin
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern bool IsUserAnAdmin();

    /// <summary>
    /// 检测当前进程是否以管理员权限运行
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            return IsUserAnAdmin();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 以管理员身份重新启动当前应用（会启动新进程并退出当前进程）
    /// </summary>
    /// <param name="args">命令行参数</param>
    /// <returns>是否成功启动</returns>
    public static bool RestartAsAdmin(string args = null)
    {
        if (IsRunningAsAdmin()) return false;
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName,
                Arguments = args ?? "",
                UseShellExecute = true,
                Verb = "runas"
            };
            System.Diagnostics.Process.Start(startInfo);
            Application.Quit();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsAdmin] 提升权限失败: {ex.Message}");
            return false;
        }
    }

#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    [DllImport("libc", SetLastError = true)]
    private static extern uint getuid();

    public static bool IsRunningAsAdmin()
    {
        try { return getuid() == 0; }
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
            Process.Start(new ProcessStartInfo { FileName = "pkexec", Arguments = "\"" + exe + "\" " + (args ?? ""), UseShellExecute = true });
            Application.Quit();
            return true;
        }
        catch (Exception ex) { Debug.LogWarning($"[Admin] 提升权限失败: {ex.Message}"); return false; }
#else
        Debug.LogWarning("[Admin] Mac 上以管理员重启需用户手动操作");
        return false;
#endif
    }

#else
    public static bool IsRunningAsAdmin() => false;
    public static bool RestartAsAdmin(string args = null) => false;
#endif
}
