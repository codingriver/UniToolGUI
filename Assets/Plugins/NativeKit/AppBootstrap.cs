using UnityEngine;
using NativeKit;
using Debug = UnityEngine.Debug;

    /// <summary>
    /// 应用启动引导：日志系统初始化。
    /// 挂载到场景根 GameObject，Script Execution Order 设为最早（-100 或更小）。
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Tooltip("预留字段：单实例功能已停用，暂不使用")]
        public string MutexName = "Unity_UniToolGUI_SingleInstance";
        [Tooltip("日志文件名")]
        public string LogFileName = "app.log";

        private void Awake()
        {
            // ── 日志系统最先初始化 ───────────────────────────────
            FileLogger.Init(LogFileName);
            string identity;
            try { identity = WindowsAdmin.GetCurrentIdentityDisplay(); }
            catch { identity = System.Environment.UserName; }
            FileLogger.Log("[AppBootstrap] 当前身份: " + identity);
            FileLogger.Log("[AppBootstrap] 当前日志文件: " + FileLogger.LogPath);
            FileLogger.Log("[AppBootstrap] 单实例功能已停用，跳过启动检测");
            Debug.Log("[AppBootstrap] 单实例功能已停用，跳过启动检测");
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            FileLogger.Log("[AppBootstrap] 应用退出");
            FileLogger.Shutdown();
        }
    }
