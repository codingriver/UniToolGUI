using System;
using UnityEngine;

/// <summary>
/// 方案A：通过注册表 AppCompatFlags 实现下次自动以管理员身份启动。
/// 写入 HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers
/// 値：RUNASADMIN
/// 此键对当前用户生效，无需管理员权限即可写入。
/// 使用 reg.exe 操作，避免 Microsoft.Win32 延伸库依赖。
/// </summary>
public static class WindowsAdminAutoElevate
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string RegKeyPath =
        @"HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    private const string RegValue = "RUNASADMIN";

    /// <summary>设置下次启动自动请求管理员权限（写注册表）</summary>
    public static bool EnableAutoAdmin()
    {
        try
        {
            string exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;
            // reg add "HKCU\..." /v "<exePath>" /t REG_SZ /d "RUNASADMIN" /f
            int code = RunReg($"add \"{RegKeyPath}\" /v \"{exePath}\" /t REG_SZ /d \"{RegValue}\" /f");
            if (code == 0) Debug.Log("[AutoAdmin] 已设置 RUNASADMIN: " + exePath);
            else Debug.LogWarning("[AutoAdmin] reg.exe 返回: " + code);
            return code == 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AutoAdmin] 写注册表失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>取消下次启动自动管理员（删除注册表键）</summary>
    public static bool DisableAutoAdmin()
    {
        try
        {
            string exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;
            // reg delete "HKCU\..." /v "<exePath>" /f  (ignore error if not exists)
            int code = RunReg($"delete \"{RegKeyPath}\" /v \"{exePath}\" /f");
            Debug.Log("[AutoAdmin] 已移除 RUNASADMIN (code=" + code + "): " + exePath);
            return true; // 键不存在时 reg delete 也返回非0，视为成功
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[AutoAdmin] 删注册表失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>检测当前是否已设置自动管理员启动</summary>
    public static bool IsAutoAdminEnabled()
    {
        try
        {
            string exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath)) return false;
            // reg query "HKCU\..." /v "<exePath>"
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = "reg.exe",
                Arguments              = $"query \"{RegKeyPath}\" /v \"{exePath}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode == 0 &&
                       output.IndexOf(RegValue, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        catch { return false; }
    }

    private static int RunReg(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName        = "reg.exe",
            Arguments       = args,
            UseShellExecute = false,
            CreateNoWindow  = true
        };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    private static string GetExePath()
    {
        try
        {
            string path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch { return null; }
    }

#else
    public static bool EnableAutoAdmin()    => false;
    public static bool DisableAutoAdmin()   => true;
    public static bool IsAutoAdminEnabled() => false;
#endif
}
