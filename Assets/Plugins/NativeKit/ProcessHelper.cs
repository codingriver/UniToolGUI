using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 跨平台进程调用工具类，统一封装 Process.Start + 超时 + 输出读取。
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// 运行命令并等待退出，返回退出码。
    /// </summary>
    /// <param name="fileName">可执行文件名</param>
    /// <param name="arguments">参数</param>
    /// <param name="timeoutMs">超时毫秒，0=不等待</param>
    /// <returns>退出码；超时或异常返回 -1</returns>
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
                    bool exited = p.WaitForExit(timeoutMs);
                    return exited ? p.ExitCode : -1;
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
    /// 运行命令并读取标准输出（异步读取，避免管道满导致的死锁）。
    /// </summary>
    /// <param name="fileName">可执行文件名</param>
    /// <param name="arguments">参数</param>
    /// <param name="timeoutMs">超时毫秒</param>
    /// <returns>标准输出（trim 后）；失败返回 null</returns>
    public static string RunAndRead(string fileName, string arguments, int timeoutMs = 30000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true   // 同时重定向 stderr，防止 stderr 满导致阻塞
            };

            using (var p = new Process { StartInfo = psi, EnableRaisingEvents = false })
            {
                var stdout = new StringBuilder();
                var stdoutLock = new object();

                p.StartInfo = psi;
                p.Start();

                // 异步读取，避免同步 ReadToEnd 在输出超过管道缓冲区时死锁
                p.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    lock (stdoutLock) stdout.AppendLine(e.Data);
                };

                // stderr 异步消费（防止子进程因 stderr 管道满而阻塞，不收集内容）
                p.ErrorDataReceived += (_, _2) => { };

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                bool exited = p.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { p.Kill(); } catch { }
                    Debug.LogWarning($"[ProcessHelper] RunAndRead({fileName}) 超时 ({timeoutMs}ms)");
                    return null;
                }

                // WaitForExit(int) 不保证异步事件已全部触发；再调用无参版确保 flush
                p.WaitForExit();

                lock (stdoutLock)
                    return stdout.ToString().Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProcessHelper] RunAndRead({fileName}) 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 启动后台进程（不等待退出），返回 Process 对象。
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
    /// 对参数进行引号包裹和转义（适用于 shell 参数）。
    /// </summary>
    public static string Quote(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
