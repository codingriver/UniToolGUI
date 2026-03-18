using System;
using System.Threading;
using UnityEngine;

namespace CloudflareST.GUI
{
    /// <summary>
    /// CFST 业务托盘管理器。
    /// 只负责注册「开始/停止测速」菜单项和发送完成通知。
    /// 开机自启、初始化/关闭/图标加载均由 TrayBridge 负责。
    /// </summary>
    public class CfstTrayManager
    {
        public static readonly CfstTrayManager Instance = new CfstTrayManager();
        private CfstTrayManager() { }

        private Action _onStartTest;
        private Action _onStopTest;
        private Func<bool> _isRunning;
        private bool _initialized;
        private SynchronizationContext _mainCtx;
        private TrayBridge _trayBridge;

        public static bool MinimizeToTray { get; set; } = true;

        private TrayMenuItem _menuStart;
        private TrayMenuItem _menuStop;

        /// <summary>
        /// 注册业务菜单项。必须在 Unity 主线程、TrayBridge 初始化完成后调用。
        /// </summary>
        public void Init(Action onStartTest, Action onStopTest, Func<bool> isRunning, TrayBridge trayBridge = null)
        {
            if (_initialized) return;
            _onStartTest = onStartTest;
            _onStopTest  = onStopTest;
            _isRunning   = isRunning;
            _trayBridge  = trayBridge;
            _mainCtx     = SynchronizationContext.Current ?? new SynchronizationContext();
            BuildMenu();
            _initialized = true;
            Debug.Log("[CfstTrayManager] 业务菜单已注册");
        }

        /// <summary>测速运行状态变化时调用，刷新托盘菜单文字</summary>
        public void OnRunningStateChanged(bool isRunning)
        {
            if (!_initialized) return;
            if (_menuStart != null) _menuStart.Text = isRunning ? "测速中..." : "[>] 开始测速";
            if (_menuStop  != null) _menuStop.Text  = isRunning ? "[|] 停止测速" : "[|] 停止（未运行）";
            NativePlatform.Tray.RefreshMenu();
        }

        /// <summary>测速完成时显示托盘气泡通知</summary>
        public void ShowFinishedBalloon(int resultCount, float bestLatency, float bestSpeed)
        {
            if (!TrayBridge.IsInitialized) return;
            string msg;
            if (resultCount > 0)
            {
                msg = string.Format("有效 {0} 个 IP，最快 {1:F0} ms", resultCount, bestLatency);
                if (bestSpeed >= 0)
                    msg += string.Format(" / {0:F2} MB/s", bestSpeed);
            }
            else
            {
                msg = "未找到有效 IP";
            }
            NativePlatform.Tray.ShowBalloonTip("测速完成", msg, 1, 4000);
        }

        private void BuildMenu()
        {
            _menuStart = new TrayMenuItem
            {
                Text     = "[>] 开始测速",
                Callback = () => _mainCtx.Post(_ =>
                {
                    NativePlatform.Tray.ShowMainWindow();
                    if (_isRunning != null && !_isRunning())
                        _onStartTest?.Invoke();
                }, null)
            };

            _menuStop = new TrayMenuItem
            {
                Text     = "[|] 停止测速",
                Callback = () => _mainCtx.Post(_ =>
                {
                    if (_isRunning != null && _isRunning())
                        _onStopTest?.Invoke();
                }, null)
            };

            var items = new TrayMenuItem[] { _menuStart, _menuStop };

            // 通过 TrayBridge 统一管理菜单顺序和重建
            if (_trayBridge != null)
                _trayBridge.RegisterExtraMenuItems(items);
            else
                NativePlatform.Tray.RegisterMenuItems(items);
        }
    }
}
