using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 跨平台进程调用工具类，统一封装 Process.Start + 超时 + 输出读取
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// 运行命令并等待退出，返回退出码
    /// </summary>
    /// <param name="fileName">可执行文件名</param>
    /// <param name="arguments">参数</param>
    /// <param name="timeoutMs">超时毫秒，0=不等待</param>
    /// <returns>退出码，超时或异常返回 -1</returns>
    public static int Run(string fileName, string arguments, int timeoutMs = 5000)
    {
        try
        {
            using (var p = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                if (p == null) return -1;
                if (timeoutMs > 0)
                {
                    p.WaitForExit(timeoutMs);
                    return p.HasExited ? p.ExitCode : -1;
                }
                return 0;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProcessHelper] Run({fileName}) 失败: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 运行命令并读取标准输出
    /// </summary>
    /// <param name="fileName">可执行文件名</param>
    /// <param name="arguments">参数</param>
    /// <param name="timeoutMs">超时毫秒</param>
    /// <returns>标准输出（trim 后），失败返回 null</returns>
    public static string RunAndRead(string fileName, string arguments, int timeoutMs = 30000)
    {
        try
        {
            using (var p = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            }))
            {
                if (p == null) return null;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(timeoutMs);
                return output?.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProcessHelper] RunAndRead({fileName}) 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 启动后台进程（不等待退出），返回 Process 对象
    /// </summary>
    public static Process StartBackground(string fileName, string arguments)
    {
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProcessHelper] StartBackground({fileName}) 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 对参数进行引号包裹和转义
    /// </summary>
    public static string Quote(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
