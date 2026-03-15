// ============================================================
// CfstProcessManager.cs  —  管理 cfst 进程生命周期
// Unity .NET Standard 2.1 兼容版本
// ============================================================
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
// 明确使用 UnityEngine.Debug 避免与 System.Diagnostics.Debug 歧义

namespace CloudflareST.GUI
{
    public sealed class CfstProcessManager : IDisposable
    {
        private Process                  _process;
        private CancellationTokenSource _cts;
        private bool                     _disposed;

        public string ExePath          { get; set; }
        public string WorkingDirectory { get; set; }
        public bool   IsRunning        => _process != null && !_process.HasExited;
        public int?   ProcessId
        {
            get { try { return IsRunning ? _process.Id : (int?)null; } catch { return null; } }
        }

        public event Action<string> OnOutput;
        public event Action<string> OnError;
        public event Action<int>    OnExited;
        public event Action         OnStarted;

        public CfstProcessManager(string exePath)
        {
            ExePath = exePath ?? throw new ArgumentNullException("exePath");
        }

        public Task StartAsync(CfstOptions options = null,
                               CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(GetType().Name);
            if (IsRunning)
                throw new InvalidOperationException("cfst 进程已在运行，请先调用 Stop() 或 Kill()。");
            if (!System.IO.File.Exists(ExePath))
                throw new System.IO.FileNotFoundException(
                    "找不到 cfst 可执行文件：" + ExePath, ExePath);

            string arguments = options != null ? options.ToArguments() : string.Empty;
            string workDir   = WorkingDirectory
                               ?? System.IO.Path.GetDirectoryName(ExePath)
                               ?? ".";

            var psi = new ProcessStartInfo
            {
                FileName               = ExePath,
                Arguments              = arguments,
                WorkingDirectory       = workDir,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                RedirectStandardInput  = false,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding  = System.Text.Encoding.UTF8,
            };

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _process.OutputDataReceived += (sender, e) => { if (e.Data != null) OnOutput?.Invoke(e.Data); };
            _process.ErrorDataReceived  += (sender, e) => { if (e.Data != null) OnError?.Invoke(e.Data);  };
            _process.Exited             += (sender, e) => { int code = TryGetExitCode(); OnExited?.Invoke(code); };

            _cts.Token.Register(() => Stop());

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            OnStarted?.Invoke();
            return Task.CompletedTask;
        }

        /// <summary>等待进程退出（轮询方式，兼容 .NET Standard 2.1）</summary>
        public async Task<bool> WaitForExitAsync(int timeoutMs = -1)
        {
            if (_process == null || _process.HasExited) return true;

            int elapsed = 0;
            const int interval = 100;
            while (!_process.HasExited)
            {
                await Task.Delay(interval);
                elapsed += interval;
                if (timeoutMs >= 0 && elapsed >= timeoutMs)
                    return false;
            }
            return true;
        }

        public void Stop(int gracePeriodMs = 3000)
        {
            if (_process == null || _process.HasExited) return;
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                try { CfstNativeMethods.SendCtrlC(_process.Id); } catch { }
#endif
                bool exited = _process.WaitForExit(gracePeriodMs);
                if (!exited) Kill();
            }
            catch (InvalidOperationException) { }
        }

        public void Kill()
        {
            if (_process == null || _process.HasExited) return;
            try   { _process.Kill(); }
            catch (InvalidOperationException) { }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[CfstProcessManager] Kill: " + ex.Message);
            }
        }

        public void Cancel() => _cts?.Cancel();

        private int TryGetExitCode()
        {
            try { return _process != null ? _process.ExitCode : -1; }
            catch { return -1; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Kill();
            if (_cts     != null) { _cts.Dispose();     _cts     = null; }
            if (_process != null) { _process.Dispose(); _process = null; }
        }
    }

    // ── Windows Ctrl+C 辅助（重命名避免与 NativeKit 内部类冲突）──────
    internal static class CfstNativeMethods
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

        private const uint CTRL_C_EVENT = 0;

        internal static void SendCtrlC(int pid)
        {
            FreeConsole();
            if (AttachConsole((uint)pid))
            {
                GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
                FreeConsole();
            }
        }
#else
        internal static void SendCtrlC(int pid) { }
#endif
    }
}
