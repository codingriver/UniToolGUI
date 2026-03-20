using System;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainWindowController : MonoBehaviour
    {
        [Header("Page Controllers")]
        public PageIpSourceController  PageIpSource;
        public PageLatencyController   PageLatency;
        public PageDownloadController  PageDownload;
        public PageScheduleController  PageSchedule;
        public PageHostsController     PageHosts;
        public PageOutputController    PageOutput;
        public PageOtherController     PageOther;
        public PageAboutController     PageAbout;
        public PageResultsController   PageResults;
        public PageLogController       PageLog;
        public PageHookController      PageHook;

        private UIDocument    _doc;
        private VisualElement _root;
        private Button[]        _navBtns;
        private VisualElement[] _pages;
        private const int PAGE_COUNT   = 11;
        private const int PAGE_RESULTS = 8;
        private const int PAGE_LOG     = 9;
        private const int PAGE_HOOK    = 10;

        private Button        _btnStart;
        private Button        _btnStop;
        private VisualElement _progressFill;
        private Label         _progressPct;
        private Label         _sidebarStatus;
        private Label         _resultBadge;
        private Label         _sbStatus;
        private Label         _sbTested;
        private Label         _sbElapsed;
        private Label         _sbBest;
        private Label         _sbUserRole;
        private Label         _sbNextRun;
        private Label         _sbNextRunSep;

        public static readonly CfstOptions Options = new CfstOptions();
        private CfstDllRunner _runner;

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;
        }

        private void OnEnable()
        {
            // 启动时加载持久化设置
            SettingsStorage.Load(Options);

            BindElements();
            InitPages();
            BindNav();
            BindButtons();
            AppState.Instance.OnChanged         += RefreshSidebar;
            TestResult.Instance.OnResultUpdated += RefreshBadge;
            NavigateTo(0);
            RefreshSidebar();

            // 订阅重置事件
            if (PageOther != null)
                PageOther.OnSettingsReset += OnSettingsReset;

            TrayBridge.TrayReady += RegisterTrayMenuItems;
            if (TrayBridge.IsInitialized)
                RegisterTrayMenuItems();
        }

        private void OnDisable()
        {
            // 退出时保存所有设置
            SettingsStorage.Save(Options);

            if (PageOther != null)
                PageOther.OnSettingsReset -= OnSettingsReset;
            TrayBridge.TrayReady -= RegisterTrayMenuItems;
            AppState.Instance.OnChanged         -= RefreshSidebar;
            TestResult.Instance.OnResultUpdated -= RefreshBadge;
            if (_btnStart != null) _btnStart.clicked -= () => StartTest();
            if (_btnStop  != null) _btnStop.clicked  -= StopTest;
            _runner?.Dispose();
            _runner = null;
        }

        /// <summary>重置后重新初始化所有页面控制器，使 UI 与新默认值同步</summary>
        private void OnSettingsReset()
        {
            InitPages();
        }

        private void OnApplicationQuit()
        {
            // 退出时保存（OnDisable 不一定在 Application.Quit 时触发）
            SettingsStorage.Save(Options);
        }

        private void BindElements()
        {
            _btnStart      = _root.Q<Button>("btn-start");
            _btnStop       = _root.Q<Button>("btn-stop");
            _progressFill  = _root.Q<VisualElement>("progress-fill");
            _progressPct   = _root.Q<Label>("progress-pct");
            _sidebarStatus = _root.Q<Label>("status-text");
            _resultBadge   = _root.Q<Label>("result-badge");
            _sbStatus      = _root.Q<Label>("sb-status");
            _sbTested      = _root.Q<Label>("sb-tested");
            _sbElapsed     = _root.Q<Label>("sb-elapsed");
            _sbBest        = _root.Q<Label>("sb-best");
            _sbUserRole    = _root.Q<Label>("sb-user-role");
            _sbNextRun     = _root.Q<Label>("sb-next-run");
            _sbNextRunSep  = _root.Q<Label>("sb-next-run-sep");
            InitUserRoleLabel();

            // ── 导航按钮：限定在 nav-list 内查找，避免跨页面误匹配 ──
            var navList = _root.Q<VisualElement>("nav-list");
            UnityEngine.Debug.Log("[NAV] nav-list found: " + (navList != null));

            _navBtns = new Button[PAGE_COUNT];
            string[] navNames = { "nav-ip","nav-latency","nav-download","nav-schedule",
                                  "nav-hosts","nav-output","nav-other","nav-about","nav-results",
                                  "nav-log","nav-hook" };
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                _navBtns[i] = navList != null
                    ? navList.Q<Button>(navNames[i])
                    : _root.Q<Button>(navNames[i]);
                UnityEngine.Debug.Log("[NAV] " + navNames[i] + " = " + (_navBtns[i] != null ? "OK" : "NULL"));
            }

            // ── 页面容器：限定在 page-container 内查找 ──
            var pageContainer = _root.Q<VisualElement>("page-container");
            UnityEngine.Debug.Log("[NAV] page-container found: " + (pageContainer != null));

            _pages = new VisualElement[PAGE_COUNT];
            string[] pageNames = { "page-ip","page-latency","page-download","page-schedule",
                                   "page-hosts","page-output","page-other","page-about","page-results",
                                   "page-log","page-hook" };
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                _pages[i] = pageContainer != null
                    ? pageContainer.Q<VisualElement>(pageNames[i])
                    : _root.Q<VisualElement>(pageNames[i]);
                UnityEngine.Debug.Log("[NAV] " + pageNames[i] + " = " + (_pages[i] != null ? _pages[i].GetType().Name : "NULL"));
            }
        }

        private void InitPages()
        {
            PageIpSource?.Init(_pages[0], Options);
            PageLatency ?.Init(_pages[1], Options);
            PageDownload?.Init(_pages[2], Options);
            PageSchedule?.Init(_pages[3], Options);
            PageHosts   ?.Init(_pages[4], Options);
            PageOutput  ?.Init(_pages[5], Options);
            PageOther   ?.Init(_pages[6], Options);
            PageAbout   ?.Init(_pages[7], Options);
            PageResults ?.Init(_pages[8], Options);
            PageLog     ?.Init(_pages[9],  Options);
            PageHook    ?.Init(_pages[10], Options);
            // 注入日志控制器到钩子页面
            if (PageHook  != null) PageHook.LogController  = PageLog;
            // 注入日志控制器到其他设置页面（转发 AppendLog）
            if (PageOther != null) PageOther.LogController = PageLog;
            // 绑定 ScheduleManager 依赖
            BindScheduleManager();
        }

        private void BindScheduleManager()
        {
            var mgr = ScheduleManager.Instance;
            if (mgr == null) return;
            mgr.StartTestAction  = () => StartTest(fromScheduler: true);
            mgr.HookController   = PageHook;
            mgr.LogController    = PageLog;
            // 调度状态变更时刷新状态栏
            mgr.OnStateChanged  += RefreshSidebar;
        }

        private void BindNav()
        {
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                int idx = i;
                if (_navBtns[i] == null)
                {
                    UnityEngine.Debug.LogWarning("[NAV] _navBtns[" + i + "] is null, skip bind");
                    continue;
                }

                // PointerUpEvent fires reliably even inside a ScrollView
                _navBtns[i].RegisterCallback<PointerUpEvent>(evt =>
                {
                    UnityEngine.Debug.Log("[NAV] PointerUp idx=" + idx);
                    NavigateTo(idx);
                    evt.StopImmediatePropagation();
                }, TrickleDown.NoTrickleDown);
            }
        }
        public void NavigateToResults() => NavigateTo(PAGE_RESULTS);
        public void NavigateTo(int idx)
        {
            // UnityEngine.Debug.Log("[NAV] NavigateTo(" + idx + ")");
            AppState.Instance.CurrentPage = idx;
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                bool active = i == idx;
                if (_pages[i] != null)
                {
                    if (active) _pages[i].AddToClassList("page--active");
                    else        _pages[i].RemoveFromClassList("page--active");
                    // UnityEngine.Debug.Log("[NAV] page[" + i + "] active=" + active
                    //     + " classes=" + string.Join(",", _pages[i].GetClasses()));
                }
                if (_navBtns[i] != null)
                {
                    if (active) _navBtns[i].AddToClassList("nav-item--active");
                    else        _navBtns[i].RemoveFromClassList("nav-item--active");
                }
            }
        }

        private void BindButtons()
        {
            if (_btnStart != null) _btnStart.clicked += () => StartTest();
            if (_btnStop  != null) _btnStop.clicked  += StopTest;
        }

        // ── 托盘业务菜单项注册 ────────────────────────────────
        // 由 TrayBridge.TrayReady 事件触发，与 TrayBridge 解耦
        private void RegisterTrayMenuItems()
        {
            CfstTrayManager.Instance.Init(
                onStartTest: () => StartTest(),
                onStopTest:  StopTest,
                isRunning:   () => AppState.Instance.IsRunning,
                trayBridge:  GetComponent<TrayBridge>() ?? FindObjectOfType<TrayBridge>()
            );
        }

        private bool _startedByScheduler;

        // ── 开始测速 ─────────────────────────────────────────
        /// <param name="fromScheduler">true 时跳过前/后钩子（ScheduleManager 已在 RunOnce 中调用）</param>
        public void StartTest(bool fromScheduler = false)
        {
            if (AppState.Instance.IsRunning) return;

            // ── 前钩子（非调度器调用时执行）─────────────────
            if (!fromScheduler && PageHook != null && !PageHook.RunPreHook())
                return;  // 前钩子失败，取消测速

            _startedByScheduler = fromScheduler;

            TestResult.Instance.Clear();
            AppState.Instance.Reset();
            AppState.Instance.IsRunning  = true;
            AppState.Instance.StatusText = "启动中...";
            AppState.Instance.StartTime  = DateTime.Now;

            // 构建 cfst.dll 配置
            CloudflareST.Config cfg;
            try
            {
                cfg = CfstConfigBuilder.Build(Options);
            }
            catch (Exception ex)
            {
                string msg = "配置错误: " + ex.Message;
                UnityEngine.Debug.LogError("[CFST] " + msg);
                AppState.Instance.IsRunning  = false;
                AppState.Instance.StatusText = msg;
                PageOther?.AppendLog("[ERROR] " + msg);
                PageLog?.AppendLog("[ERROR] " + msg);
                return;
            }

            string cfgSummary = Options.ToArguments();
            UnityEngine.Debug.Log("[CFST] Config args: " + cfgSummary);
            PageOther?.AppendLog("[CMD] " + cfgSummary);
            PageLog?.AppendLog("[CMD] " + cfgSummary);

            _runner?.Dispose();
            _runner = new CfstDllRunner();

            _runner.OnLog += line =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    PageOther?.AppendLog(line);
                    PageLog?.AppendLog(line);
                    OutputParser.ParseLog(line, AppState.Instance);
                });

            _runner.OnProgress += json =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OutputParser.ParseProgress(json, AppState.Instance, TestResult.Instance);
                });

            _runner.OnFinished += dllResults =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    
                    // DLL 返回的完整排序结果列表覆盖进度 JSON 中的简略结果
                    OutputParser.ApplyDllResults(dllResults, AppState.Instance, TestResult.Instance);
                    int exitCode = dllResults == null ? -1 : 0;
                    HandleFinished(exitCode);
                    string finMsg = "完成";
                    PageOther?.AppendLog(finMsg);
                    PageLog?.AppendLog("[INFO] " + finMsg);
                    // ── 后钩子（非调度器调用时执行，调度器在 RunOnce 中自行调用）
                    if (!_startedByScheduler)
                        PageHook?.RunPostHook(exitCode);
                });

            _runner.OnError += ex =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    UnityEngine.Debug.LogError("[CFST] Runner error: " + ex.Message);
                    PageOther?.AppendLog("[ERROR] " + ex.Message);
                    PageLog?.AppendLog("[ERROR] " + ex.Message);
                });

            try
            {
                _runner.Start(cfg);
                PageOther?.AppendLog("[INFO] 测速已启动");
                PageLog?.AppendLog("[INFO] 测速已启动");
                ToastManager.Info("测速已启动");
                StartCoroutine(ElapsedTimer());
            }
            catch (Exception ex)
            {
                string msg = "启动失败: " + ex.Message;
                UnityEngine.Debug.LogError("[CFST] " + msg);
                AppState.Instance.IsRunning  = false;
                AppState.Instance.StatusText = msg;
                PageOther?.AppendLog("[ERROR] " + msg);
                PageLog?.AppendLog("[ERROR] " + msg);
            }
        }

        // ── 停止测速 ─────────────────────────────────────────
        public void StopTest()
        {
            UnityEngine.Debug.Log("[CFST] StopTest called");
            PageOther?.AppendLog("[INFO] 用户请求停止");
            PageLog?.AppendLog("[INFO] 用户请求停止");
            _runner?.Stop();
            AppState.Instance.IsRunning  = false;
            AppState.Instance.StatusText = "已停止";
            ToastManager.Warning("测速已中止");
        }

        // ── 完成处理 ─────────────────────────────────────────
        private void HandleFinished(int exitCode)
        {
            // 通知 ScheduleManager 本次测速已结束（供协程等待信号）
            ScheduleManager.Instance?.NotifyTestFinished?.Invoke(exitCode);
            AppState.Instance.IsRunning  = false;
            AppState.Instance.FinishTime = DateTime.Now;
            AppState.Instance.Elapsed    = DateTime.Now - AppState.Instance.StartTime;
            AppState.Instance.StatusText = exitCode == 0 ? "已完成" : (exitCode == -1 ? "已取消" : "完成 (异常)");
            if (exitCode == 0)
                ToastManager.Success("测速完成！共 " + TestResult.Instance.IpList.Count + " 条结果", 4f);
            else if (exitCode == -1)
                ToastManager.Warning("测速已取消");
            else
                ToastManager.Error("测速异常退出 (code=" + exitCode + ")");
            PageResults?.RefreshResults();
            // 有结果时自动导航到结果页（index 8）
            if (exitCode == 0 && TestResult.Instance.IpList.Count > 0)
                NavigateTo(PAGE_RESULTS);

            // ── 托盘气泡通知 + 状态刷新 ──────────────────────
            CfstTrayManager.Instance.OnRunningStateChanged(false);
            if (exitCode == 0)
            {
                var s = AppState.Instance;
                CfstTrayManager.Instance.ShowFinishedBalloon(
                    TestResult.Instance.IpList.Count,
                    s.BestLatency,
                    s.BestSpeed);
                // 同时发送 Toast（后台运行时也能收到）
                NativePlatform.ShowToast(
                    "CFST 测速完成",
                    TestResult.Instance.IpList.Count > 0
                        ? string.Format("有效 {0} 个 IP，最快 {1:F0} ms",
                            TestResult.Instance.IpList.Count, s.BestLatency)
                        : "未找到有效 IP");
            }

            UnityEngine.Debug.Log("[CFST] Finished, exitCode=" + exitCode
                + ", results=" + TestResult.Instance.IpList.Count);
        }

        // ── 计时器 ───────────────────────────────────────────
        private System.Collections.IEnumerator ElapsedTimer()
        {
            while (AppState.Instance.IsRunning)
            {
                AppState.Instance.Elapsed = DateTime.Now - AppState.Instance.StartTime;
                yield return new WaitForSeconds(1f);
            }
        }

        // ── 侧边栏刷新 ───────────────────────────────────────
        private void RefreshSidebar()
        {
            // 同步托盘菜单运行状态
            CfstTrayManager.Instance.OnRunningStateChanged(AppState.Instance.IsRunning);

            var s = AppState.Instance;
            float pct = s.Progress * 100f;
            if (_progressFill != null)
                _progressFill.style.width = new StyleLength(new Length(pct, LengthUnit.Percent));
            if (_progressPct   != null) _progressPct.text   = string.Format("{0:F0}%", pct);
            if (_sidebarStatus != null) _sidebarStatus.text = s.StatusText;

            if (_btnStart != null) _btnStart.SetEnabled(!s.IsRunning);
            if (_btnStop  != null) _btnStop.SetEnabled(s.IsRunning);
            if (_btnStop  != null)
            {
                if (s.IsRunning) _btnStop.RemoveFromClassList("btn-stop--disabled");
                else             _btnStop.AddToClassList("btn-stop--disabled");
            }

            if (_sbStatus  != null) _sbStatus.text = "@ " + s.StatusText;
            if (_sbTested  != null)
                _sbTested.text = s.TotalCount > 0
                    ? "已测: " + s.TestedCount + "/" + s.TotalCount
                    : "已测: --";
            if (_sbElapsed != null)
                _sbElapsed.text = s.Elapsed > TimeSpan.Zero
                    ? "耗时: " + s.Elapsed.ToString(@"mm\:ss") : "耗时: --";
            if (_sbBest != null)
            {
                string best = s.BestLatency >= 0
                    ? "当前最快: " + s.BestLatency.ToString("F0") + "ms"
                      + (s.BestSpeed >= 0 ? " / " + s.BestSpeed.ToString("F2") + " MB/s" : "")
                    : "当前最快: --";
                _sbBest.text = best;
            }

            // ── 状态栏：下次运行时间 ──────────────────────────
            var mgr = ScheduleManager.Instance;
            bool schedEnabled = mgr != null && mgr.IsEnabled && mgr.NextRunAt.HasValue;
            if (_sbNextRun != null)
                _sbNextRun.text = schedEnabled
                    ? "⏰ 下次: " + mgr.NextRunAt.Value.ToString("MM-dd HH:mm:ss")
                    : "";
            if (_sbNextRunSep != null)
                _sbNextRunSep.text = schedEnabled ? "|" : "";
        }

        private void RefreshBadge()
        {
            if (_resultBadge == null) return;
            int n = TestResult.Instance.IpList.Count;
            if (n > 0)
            {
                _resultBadge.text = n.ToString();
                _resultBadge.RemoveFromClassList("result-badge--hidden");
            }
            else
            {
                _resultBadge.AddToClassList("result-badge--hidden");
            }
        }
        private void InitUserRoleLabel()
        {
            if (_sbUserRole == null) return;
            bool isAdmin = false;
            try { isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }
            if (isAdmin)
            {
                _sbUserRole.text = "管理员";
                _sbUserRole.RemoveFromClassList("sb-user-role--normal");
                _sbUserRole.AddToClassList("sb-user-role--admin");
            }
            else
            {
                _sbUserRole.text = "普通用户";
                _sbUserRole.RemoveFromClassList("sb-user-role--admin");
                _sbUserRole.AddToClassList("sb-user-role--normal");
            }
        }
    }
}
