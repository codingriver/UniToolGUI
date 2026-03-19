using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NativeKit
{
    /// <summary>
    /// 文件日志插件。
    /// - 非移动平台：日志写入运行目录下 logs/app.log
    /// - 移动平台：日志写入 Application.persistentDataPath/logs/app.log
    /// - Editor：同时输出到 Unity Console
    /// 用法：FileLogger.Log / LogWarning / LogError
    /// </summary>
    public static class FileLogger
    {
        private static string    _logPath;
        private static StreamWriter _writer;
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static bool _echoToConsole;

        public static string LogPath => _logPath;

        /// <summary>
        /// 初始化日志系统。需在应用启动时（AppBootstrap.Awake）调用一次。
        /// </summary>
        /// <param name="fileName">日志文件名，默认 app.log</param>
        /// <param name="echoToConsole">是否同时输出到 Unity Console（Editor 下默认 true）</param>
        public static void Init(string fileName = "app.log", bool? echoToConsole = null)
        {
            if (_initialized) return;

#if UNITY_EDITOR
            _echoToConsole = echoToConsole ?? true;
#else
            _echoToConsole = echoToConsole ?? false;
#endif

            try
            {
                string logDir;
#if UNITY_ANDROID || UNITY_IOS
                logDir = Path.Combine(Application.persistentDataPath, "logs");
#else
                logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
#endif
                Directory.CreateDirectory(logDir);
                _logPath = Path.Combine(logDir, fileName);

                // 保留最近一次日志，超过 4MB 时滚动备份
                RollIfNeeded(_logPath);

                _writer = new StreamWriter(_logPath, append: true, encoding: Encoding.UTF8)
                {
                    AutoFlush = true
                };

                _initialized = true;
                Log("INFO", "[FileLogger] 日志系统初始化完成，路径: " + _logPath);

                // 接管 Unity 日志回调
                Application.logMessageReceivedThreaded += OnUnityLog;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[FileLogger] 初始化失败: " + ex.Message);
            }
        }

        /// <summary>关闭日志文件（应用退出时调用）</summary>
        public static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            lock (_lock)
            {
                try { _writer?.Close(); } catch { }
                _writer = null;
                _initialized = false;
            }
        }

        // ── 公共日志方法 ──────────────────────────────────────
        public static void Log       (string message) => Log("INFO",    message);
        public static void LogWarning(string message) => Log("WARN",    message);
        public static void LogError  (string message) => Log("ERROR",   message);
        public static void LogDebug  (string message) => Log("DEBUG",   message);

        public static void Log(string level, string message)
        {
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{time}] [{level,-5}] {message}";

            lock (_lock)
            {
                try { _writer?.WriteLine(line); } catch { }
            }

            if (_echoToConsole)
            {
                switch (level)
                {
                    case "ERROR": UnityEngine.Debug.LogError(line);   break;
                    case "WARN":  UnityEngine.Debug.LogWarning(line); break;
                    default:      UnityEngine.Debug.Log(line);        break;
                }
            }
        }

        // ── Unity 日志回调（接管 Debug.Log 等）────────────────
        private static void OnUnityLog(string logString, string stackTrace, LogType type)
        {
            // 避免递归（FileLogger.Log 在 _echoToConsole=true 时调用 Debug.Log）
            if (logString.StartsWith("[20")) return; // 已是 FileLogger 格式，跳过

            string level;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception: level = "ERROR"; break;
                case LogType.Warning:   level = "WARN";  break;
                case LogType.Assert:    level = "ASSERT"; break;
                default:                level = "INFO";  break;
            }

            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{time}] [{level,-5}] {logString}";
            if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
                line += "\n" + stackTrace;

            lock (_lock)
            {
                try { _writer?.WriteLine(line); } catch { }
            }
        }

        // ── 日志滚动（超过 4MB 备份为 .bak）─────────────────
        private static void RollIfNeeded(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                var info = new FileInfo(path);
                if (info.Length < 4 * 1024 * 1024) return;
                string bak = path + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            catch { }
        }
    }
}
