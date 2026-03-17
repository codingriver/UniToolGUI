// ============================================================
// CfstDllRunner.cs  —  直接调用 cfst.dll 中的 CloudflareST 命名空间
// 替代原先启动 cfst.exe 子进程的方式
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudflareST;
using Newtonsoft.Json.Schema;
using UnityEditor;
using UnityEngine; // cfst.dll 命名空间

namespace CloudflareST.GUI
{
    /// <summary>
    /// 直接调用 cfst.dll 中 CfstRunner.RunSpeedTestAsync 的封装。
    /// 通过 CfstRunner.LogHandler / ProgressHandler 接收所有输出，
    /// 无需启动外部进程。
    /// </summary>
    public sealed class CfstDllRunner : IDisposable
    {
        private CancellationTokenSource _cts;
        private bool _disposed;
        private bool _isRunning;
        private readonly object _lock = new object();

        // ── 公开状态 ──────────────────────────────────────────
        public bool IsRunning
        {
            get { lock (_lock) return _isRunning; }
        }

        // ── 事件 ──────────────────────────────────────────────
        /// <summary>普通日志行（原 cfst stdout 非 PROGRESS 行）</summary>
        public event Action<string> OnLog;

        /// <summary>PROGRESS JSON 行（已去掉 "PROGRESS:" 前缀）</summary>
        public event Action<string> OnProgress;

        /// <summary>测速完成，携带结果列表（可能为 null 表示失败/取消）</summary>
        public event Action<IReadOnlyList<CloudflareST.IPInfo>> OnFinished;

        /// <summary>发生异常</summary>
        public event Action<Exception> OnError;

        // ── 启动测速 ──────────────────────────────────────────
        /// <summary>
        /// 异步启动测速，不阻塞 Unity 主线程。
        /// 所有回调均在后台线程触发，使用 UnityMainThreadDispatcher 转发到主线程。
        /// </summary>
        public void Start(CloudflareST.Config config, CancellationToken externalCt = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            lock (_lock)
            {
                if (_isRunning)
                    throw new InvalidOperationException("CfstDllRunner: 已有测速任务正在运行，请先调用 Stop()。");
                _isRunning = true;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);

            // 注册 cfst.dll 的日志/进度回调
            // 注意：CfstRunner 的 Handler 是静态的，需要在任务结束后清理
            CfstRunner.LogHandler      = line => FireOnLog(line);
            CfstRunner.ProgressHandler = json => FireOnProgress(json);

            var ct = _cts.Token;

            // 在线程池运行，避免阻塞 Unity 主线程
            Task.Run(async () =>
            {
                IReadOnlyList<CloudflareST.IPInfo> results = null;
                try
                {
                    // ── 打印 Config 所有参数（含默认值）──────────────────
                    string arg=@"[CFST][Config] ====== RunSpeedTestAsync 参数一览 ======\n" +
                        $"  [IP来源]\n" +
                        $"    IpRanges          = {(string.IsNullOrEmpty(config.IpRanges) ? "(空，使用文件)" : config.IpRanges)}\n" +
                        $"    IpFiles           = [{(config.IpFiles == null ? "null" : string.Join(", ", config.IpFiles))}]\n" +
                        $"    MaxIpCount        = {config.MaxIpCount}  (0=不限)\n" +
                        $"    AllIp             = {config.AllIp}\n" +
                        $"  [延迟测速]\n" +
                        $"    TcpPingMode       = {config.TcpPingMode}\n" +
                        $"    HttpingMode       = {config.HttpingMode}\n" +
                        $"    ForceIcmp         = {config.ForceIcmp}\n" +
                        $"    PingThreads       = {config.PingThreads}\n" +
                        $"    PingCount         = {config.PingCount}\n" +
                        $"    DelayThresholdMs  = {config.DelayThresholdMs} ms\n" +
                        $"    DelayMinMs        = {config.DelayMinMs} ms\n" +
                        $"    LossRateThreshold = {config.LossRateThreshold:F2} (0~1)\n" +
                        $"    HttpingStatusCode = {config.HttpingStatusCode}  (0=接受200/301/302)\n" +
                        $"    CfColo            = {(string.IsNullOrEmpty(config.CfColo) ? "(不过滤)" : config.CfColo)}\n" +
                        $"  [下载测速]\n" +
                        $"    DisableSpeedTest       = {config.DisableSpeedTest}\n" +
                        $"    SpeedUrl               = {config.SpeedUrl}\n" +
                        $"    Port                   = {config.Port}\n" +
                        $"    SpeedNum               = {config.SpeedNum}\n" +
                        $"    DownloadTimeoutSeconds = {config.DownloadTimeoutSeconds} s\n" +
                        $"    SpeedMinMbps           = {config.SpeedMinMbps:F2} Mbps\n" +
                        $"  [输出]\n" +
                        $"    OutputFile    = {config.OutputFile}\n" +
                        $"    OutputNum     = {config.OutputNum}\n" +
                        $"    OnlyIpFile    = {config.OnlyIpFile}\n" +
                        $"    Silent        = {config.Silent}\n" +
                        $"    Debug         = {config.Debug}\n" +
                        $"    ShowProgress  = {config.ShowProgress}\n" +
                        $"  [定时调度]\n" +
                        $"    IntervalMinutes = {config.IntervalMinutes}\n" +
                        $"    AtTimes         = {(string.IsNullOrEmpty(config.AtTimes) ? "(未设置)" : config.AtTimes)}\n" +
                        $"    CronExpression  = {(string.IsNullOrEmpty(config.CronExpression) ? "(未设置)" : config.CronExpression)}\n" +
                        $"    TimeZoneId      = {(string.IsNullOrEmpty(config.TimeZoneId) ? "(系统默认)" : config.TimeZoneId)}\n" +
                        $"  [Hosts更新]\n" +
                        $"    HostEntries   = {(config.HostEntries == null || config.HostEntries.Count == 0 ? "(未启用)" : config.HostEntries.Count + " 条")}\n" +
                        $"    HostsFilePath = {(string.IsNullOrEmpty(config.HostsFilePath) ? "(系统默认)" : config.HostsFilePath)}\n" +
                        $"    HostsDryRun   = {config.HostsDryRun}\n" +
                        "  ===========================================";
                    Debug.Log(arg);
                    if (config.IpFiles != null)
                    {
                        foreach (string ipFile in config.IpFiles)
                        {
                            FireOnLog(ipFile);        
                        }
                    }
                    FireOnLog("开始");
                    results = await CfstRunner.RunSpeedTestAsync(config, ct);
                }
                catch (OperationCanceledException)
                {
                    FireOnLog("[INFO] 测速已取消");
                }
                catch (Exception ex)
                {
                    FireOnLog($"[ERROR] {ex.Message}");
                    FireOnError(ex);
                }
                finally
                {
                    // 清理静态回调，防止悬挂
                    CfstRunner.LogHandler      = null;
                    CfstRunner.ProgressHandler = null;

                    lock (_lock) _isRunning = false;

                    try { _cts?.Dispose(); } catch { }
                    _cts = null;

                    FireOnFinished(results);
                }
            }, ct);
        }

        /// <summary>取消当前测速任务（通过 CancellationToken）</summary>
        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            // 同时通知 cfst.dll 内部 CfstRunner 停止
            CfstRunner.Stop();
        }

        // ── 私有触发 ──────────────────────────────────────────
        private void FireOnLog(string line)
        {
            if (line.StartsWith('\r'))
            {
                line=line.TrimStart('\r'); // 去掉行首的回车符，避免在 Unity Console 中出现多行输出
            }            
            Debug.Log(line);
            try { OnLog?.Invoke(line); } catch { }
        }

        private void FireOnProgress(string json)
        {
            Debug.LogWarning(json);
            try { OnProgress?.Invoke(json); } catch { }
        }

        private void FireOnFinished(IReadOnlyList<CloudflareST.IPInfo> results)
        {
            if (results == null)
            {
                Debug.Log("[FireOnFinished] results = null（已取消或失败）");
            }
            else
            {
                Debug.Log($"[FireOnFinished] 共 {results.Count} 条结果:");
                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    Debug.Log($"  [{i}] IP={r.IP}  延迟={r.DelayMs}ms  丢包={r.LossRate:P1}  速度={r.DownloadSpeedMbps:F2}MB/s  数据中心={r.Colo}");
                }
            }
            try { OnFinished?.Invoke(results); } catch { }
        }

        private void FireOnError(Exception ex)
        {
            Debug.LogError($"ERROR::{ex}");
            try { OnError?.Invoke(ex); } catch { }
        }

        // ── IDisposable ───────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
