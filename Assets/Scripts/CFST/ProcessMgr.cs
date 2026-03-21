// ============================================================
// ProcessMgr.cs  —  前/后置程序执行管理
// 替代 PageHookController 中内嵌的 RunHookSync 逻辑，
// 提供统一的进程启动、等待、强制终止接口。
// ============================================================
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace CloudflareST.GUI
{
    /// <summary>
    /// 同步执行一个外部可执行程序，支持超时强制终止。
    /// 所有方法均为线程安全的静态调用，适合在 Task/协程中使用。
    /// </summary>
    public static class ProcessMgr
    {
        // ── 退出码常量 ────────────────────────────────────────
        public const int EXIT_OK             =  0;
        public const int EXIT_NOT_CONFIGURED = -1;
        public const int EXIT_TIMED_OUT      = -2;
        public const int EXIT_LAUNCH_EXCEPT  = 97;
        public const int EXIT_START_FAILED   = 98;
        public const int EXIT_FILE_NOT_FOUND = 99;

        /// <summary>
        /// 同步执行程序并等待退出。
        /// </summary>
        /// <param name="path">可执行文件绝对路径。</param>
        /// <param name="args">命令行参数（可为 null）。</param>
        /// <param name="timeoutSec">超时秒数；0 或负数表示不限时。</param>
        /// <param name="logPrefix">日志前缀，用于 Debug.Log 输出。</param>
        /// <returns>进程退出码，或上方 EXIT_* 常量之一。</returns>
        public static int Run(string path, string args, int timeoutSec, string logPrefix = "[PROC]")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                UnityEngine.Debug.Log($"{logPrefix} path is empty, skip.");
                return EXIT_NOT_CONFIGURED;
            }
            if (!File.Exists(path))
            {
                UnityEngine.Debug.LogWarning($"{logPrefix} file not found: {path}");
                return EXIT_FILE_NOT_FOUND;
            }

            var psi = new ProcessStartInfo
            {
                FileName        = path,
                Arguments       = args ?? "",
                UseShellExecute = false,
                CreateNoWindow  = true,
            };

            UnityEngine.Debug.Log($"{logPrefix} Start: \"{path}\" {args}");

            try
            {
                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        UnityEngine.Debug.LogError($"{logPrefix} Process.Start returned null.");
                        return EXIT_START_FAILED;
                    }

                    int ms      = timeoutSec > 0 ? timeoutSec * 1000 : -1;
                    bool exited = proc.WaitForExit(ms);

                    if (!exited)
                    {
                        UnityEngine.Debug.LogWarning($"{logPrefix} Timed out ({timeoutSec}s), killing process.");
                        try { proc.Kill(); } catch { }
                        return EXIT_TIMED_OUT;
                    }

                    UnityEngine.Debug.Log($"{logPrefix} Exited with code {proc.ExitCode}.");
                    return proc.ExitCode;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"{logPrefix} Exception: {ex.Message}");
                return EXIT_LAUNCH_EXCEPT;
            }
        }

        /// <summary>
        /// 将退出码转换为可读字符串。
        /// </summary>
        public static string DescribeExitCode(int code) =>
            code == EXIT_OK             ? "OK (exit 0)"        :
            code == EXIT_NOT_CONFIGURED ? "Not configured"     :
            code == EXIT_TIMED_OUT      ? "Timed out"          :
            code == EXIT_LAUNCH_EXCEPT  ? "Launch exception"   :
            code == EXIT_START_FAILED   ? "Process start failed" :
            code == EXIT_FILE_NOT_FOUND ? "File not found"     :
            $"Failed (exit {code})";

        /// <summary>
        /// 判断退出码是否代表成功。
        /// </summary>
        public static bool IsSuccess(int code) => code == EXIT_OK;
    }
}
