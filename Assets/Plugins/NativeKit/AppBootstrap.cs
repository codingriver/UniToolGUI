using UnityEngine;



    /// <summary>
    /// 应用启动引导：单实例检测。
    /// 挂载到场景根 GameObject，Script Execution Order 设为最早（-100 或更小）。
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Tooltip("托盘图标悬停提示文字")]
        public string MutexName = "Unity_UniToolGUI_SingleInstance";

        private void Awake()
        {
            // ── 单实例检测 ───────────────────────────────────────
            if (!NativePlatform.SingleInstance.TryAcquire(MutexName))
            {
                Debug.LogWarning("[AppBootstrap] 检测到已有实例在运行，本次启动将退出。");
#if !UNITY_EDITOR
                Application.Quit();
#else
                Debug.LogWarning("[AppBootstrap] Editor 模式下跳过退出（避免关闭编辑器）");
#endif
                return;
            }

            Debug.Log("[AppBootstrap] 单实例锁已获取：" + MutexName);
            DontDestroyOnLoad(gameObject);
        }

        private void OnApplicationQuit()
        {
            NativePlatform.SingleInstance.Release();
            Debug.Log("[AppBootstrap] 单实例锁已释放");
        }
    }

