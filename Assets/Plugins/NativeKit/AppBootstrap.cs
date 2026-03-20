using System.Collections;
using UnityEngine;
using System.Diagnostics;
using NativeKit;
using UIKit;
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

        private bool _shouldQuit = false;

        private void Awake()
        {
            // ── 日志系统最先初始化 ───────────────────────────────
            FileLogger.Init(LogFileName);

        // ── 单实例检测 ───────────────────────────────────────
        Debug.Log(string.Format("[AppBootstrap] 开始单实例检测 | MutexName={0} | platform={1}", MutexName, Application.platform));
        bool acquired = NativePlatform.SingleInstance.TryAcquire(MutexName);
        Debug.Log(string.Format("[AppBootstrap] TryAcquire 结果: {0}", acquired));
        if (!acquired)
        {
            FileLogger.LogWarning("[AppBootstrap] 检测到已有实例在运行，本次启动将退出。");
            Debug.LogWarning("[AppBootstrap] 检测到已有实例在运行，本次启动将退出。");
#if UNITY_EDITOR
            Debug.LogWarning("[AppBootstrap] Editor 模式下跳过进程退出（避免关闭编辑器），单实例检测本身已生效");
#else
            _shouldQuit = true;
            DontDestroyOnLoad(gameObject);
#endif
            return;
        }

        FileLogger.Log("[AppBootstrap] 单实例锁已获取：" + MutexName);
        Debug.Log("[AppBootstrap] 单实例锁已获取：" + MutexName);
        DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if !UNITY_EDITOR
            if (_shouldQuit)
            {
                StartCoroutine(ShowDuplicateToastAndQuit());
            }
#endif
        }

        private IEnumerator ShowDuplicateToastAndQuit()
        {
            // 等一帧让 UIDocument / ToastManager 完成初始化
            yield return null;
            yield return null;

            ToastManager.Warning("程序已在运行中，将自动退出…", 3f);
            FileLogger.LogWarning("[AppBootstrap] Toast 已显示，2秒后退出。");

            yield return new WaitForSeconds(2f);

            FileLogger.Shutdown();
            Process.GetCurrentProcess().Kill();
        }

        private void OnApplicationQuit()
        {
            if (!_shouldQuit)
            {
                NativePlatform.SingleInstance.Release();
                FileLogger.Log("[AppBootstrap] 应用退出，单实例锁已释放");
            }
            FileLogger.Shutdown();
        }
    }

