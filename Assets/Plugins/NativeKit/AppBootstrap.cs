using UnityEngine;
using System.Diagnostics;
using NativeKit;
using Debug = UnityEngine.Debug;

    /// <summary>
    /// 应用启动引导：单实例检测 + 日志系统初始化。
    /// 挂载到场景根 GameObject，Script Execution Order 设为最早（-100 或更小）。
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Tooltip("单实例 Mutex 名称")]
        public string MutexName = "Unity_UniToolGUI_SingleInstance";
        [Tooltip("日志文件名")]
        public string LogFileName = "app.log";

        private void Awake()
        {
            // ── 日志系统最先初始化 ───────────────────────────────
            FileLogger.Init(LogFileName);

            // ── 单实例检测 ───────────────────────────────────────
            if (!NativePlatform.SingleInstance.TryAcquire(MutexName))
            {
                FileLogger.LogWarning("[AppBootstrap] 检测到已有实例在运行，本次启动将退出。");
#if UNITY_EDITOR
                Debug.LogWarning("[AppBootstrap] Editor 模式下跳过退出（避免关闭编辑器）");
#else
                // Application.Quit() 在 Awake 首帧无效，必须强制杀死进程
                FileLogger.Shutdown();
                Process.GetCurrentProcess().Kill();
#endif
                return;
            }

            FileLogger.Log("[AppBootstrap] 单实例锁已获取：" + MutexName);
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            NativePlatform.SingleInstance.Release();
            FileLogger.Log("[AppBootstrap] 应用退出，单实例锁已释放");
            FileLogger.Shutdown();
        }
    }

