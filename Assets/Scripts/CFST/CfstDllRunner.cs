// ============================================================
// CfstDllRunner.cs  —  直接调用 cfst.dll 中的 CloudflareST 命名空间
// 替代原先启动 cfst.exe 子进程的方式
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudflareST;  // cfst.dll 命名空间

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
            try { OnLog?.Invoke(line); } catch { }
        }

        private void FireOnProgress(string json)
        {
            try { OnProgress?.Invoke(json); } catch { }
        }

        private void FireOnFinished(IReadOnlyList<CloudflareST.IPInfo> results)
        {
            try { OnFinished?.Invoke(results); } catch { }
        }

        private void FireOnError(Exception ex)
        {
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
