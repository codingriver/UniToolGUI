using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainWindowController : MonoBehaviour
    {
        [Header("Page Controllers")]
        [SerializeField] private PageIpSourceController  PageIpSource;
        [SerializeField] private PageLatencyController   PageLatency;
        [SerializeField] private PageDownloadController  PageDownload;
        [SerializeField] private PageScheduleController  PageSchedule;
        [SerializeField] private PageHostsController     PageHosts;
        [SerializeField] private PageOutputController    PageOutput;
        [SerializeField] private PageOtherController     PageOther;
        [SerializeField] private PageAboutController     PageAbout;
        [SerializeField] private PageResultsController   PageResults;
        [SerializeField] private PageLogController       PageLog;
        [SerializeField] private PageHookController      PageHook;

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
        private readonly Action[] _navClickHandlers = new Action[PAGE_COUNT];
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
        private MainWindowLayoutBootstrap _layoutBootstrap;
        private bool _isMobileStructure;

        private bool _navBound;
        private bool _buttonsBound;
        private bool _scheduleManagerBound;
        private bool _initialized;
        private Coroutine _deferredInitCoroutine;

        public static readonly CfstOptions Options = new CfstOptions();
        private CfstDllRunner _runner;

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _layoutBootstrap = GetComponent<MainWindowLayoutBootstrap>();
            EnsurePageControllers();
        }

        private void OnEnable()
        {
            SettingsStorage.Load(Options);
            _initialized = false;
            if (_deferredInitCoroutine != null)
                StopCoroutine(_deferredInitCoroutine);
            _deferredInitCoroutine = StartCoroutine(DeferredInitialize());
        }

        private void OnDisable()
        {
            SettingsStorage.Save(Options);

            if (_deferredInitCoroutine != null)
            {
                StopCoroutine(_deferredInitCoroutine);
                _deferredInitCoroutine = null;
            }

            if (PageOther != null)
                PageOther.OnSettingsReset -= OnSettingsReset;
            TrayBridge.TrayReady -= RegisterTrayMenuItems;
            AppState.Instance.OnChanged         -= RefreshSidebar;
            TestResult.Instance.OnResultUpdated -= RefreshBadge;
            UnbindButtons();
            UnbindNav();
            UnbindScheduleManager();
            _runner?.Dispose();
            _runner = null;
            _initialized = false;
        }

        /// <summary>重置后重新初始化所有页面控制器，使 UI 与新默认值同步</summary>
        private void OnSettingsReset()
        {
            if (!_initialized)
                return;
            InitPages();
        }

        private void OnApplicationQuit()
        {
            // 退出时保存（OnDisable 不一定在 Application.Quit 时触发）
            SettingsStorage.Save(Options);
        }

        private IEnumerator DeferredInitialize()
        {
            yield return null;
            yield return null;
            TryInitializeUI();
            _deferredInitCoroutine = null;
        }

        private void TryInitializeUI()
        {
            _layoutBootstrap = GetComponent<MainWindowLayoutBootstrap>();
            _isMobileStructure = _layoutBootstrap != null && _layoutBootstrap.IsMobileStructure;

            BindElements();
            LogVisualTreeDiagnostics();

            bool hasAllPages = _pages != null && _pages.All(page => page != null);
            if (!hasAllPages)
            {
                UnityEngine.Debug.LogWarning("[UI] initialization deferred: page roots are not ready");
                return;
            }

            InitPages();
            BindNav();
            BindButtons();

            AppState.Instance.OnChanged         -= RefreshSidebar;
            AppState.Instance.OnChanged         += RefreshSidebar;
            TestResult.Instance.OnResultUpdated -= RefreshBadge;
            TestResult.Instance.OnResultUpdated += RefreshBadge;

            NavigateTo(0);
            RefreshSidebar();

            if (PageOther != null)
                PageOther.OnSettingsReset -= OnSettingsReset;
            if (PageOther != null)
                PageOther.OnSettingsReset += OnSettingsReset;

            TrayBridge.TrayReady -= RegisterTrayMenuItems;
            TrayBridge.TrayReady += RegisterTrayMenuItems;
            if (!_isMobileStructure && TrayBridge.IsInitialized)
                RegisterTrayMenuItems();

            _initialized = true;
        }

        private void LogVisualTreeDiagnostics()
        {
            string assetName = _doc != null && _doc.visualTreeAsset != null ? _doc.visualTreeAsset.name : "<null>";
            string rootName = _root != null ? (_root.name ?? "<unnamed>") : "<null>";
            int rootChildCount = _root != null ? _root.childCount : -1;
            var pageContainer = _root != null ? _root.Q<VisualElement>("page-container") : null;
            int pageContainerChildren = pageContainer != null ? pageContainer.childCount : -1;
            UnityEngine.Debug.Log($"[UI] asset={assetName}, root={rootName}, rootChildren={rootChildCount}, pageContainer={(pageContainer != null)}, pageContainerChildren={pageContainerChildren}, mobile={_isMobileStructure}");
        }

        private void EnsurePageControllers()
        {
            PageIpSource = EnsureController(PageIpSource);
            PageLatency  = EnsureController(PageLatency);
            PageDownload = EnsureController(PageDownload);
            PageSchedule = EnsureController(PageSchedule);
            PageHosts    = EnsureController(PageHosts);
            PageOutput   = EnsureController(PageOutput);
            PageOther    = EnsureController(PageOther);
            PageAbout    = EnsureController(PageAbout);
            PageResults  = EnsureController(PageResults);
            PageLog      = EnsureController(PageLog);
            PageHook     = EnsureController(PageHook);
        }

        private T EnsureController<T>(T existing) where T : Component
        {
            if (existing != null)
                return existing;

            var found = GetComponent<T>();
            if (found != null)
                return found;

            return gameObject.AddComponent<T>();
        }

        private void BindElements()
        {
            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null)
            {
                UnityEngine.Debug.LogError("[UI] rootVisualElement is null");
                return;
            }

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

            var navRoots = _root.Query<VisualElement>(name: "nav-list").ToList();
            _navBtns = new Button[PAGE_COUNT];
            string[] navNames = { "nav-ip","nav-latency","nav-download","nav-schedule",
                                  "nav-hosts","nav-output","nav-other","nav-about","nav-results",
                                  "nav-log","nav-hook" };
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                foreach (var navRoot in navRoots)
                {
                    var btn = navRoot.Q<Button>(navNames[i]);
                    if (btn == null) continue;
                    _navBtns[i] = btn;
                    break;
                }
                if (_navBtns[i] == null)
                    _navBtns[i] = _root.Q<Button>(navNames[i]);
            }

            var pageContainer = _root.Q<VisualElement>("page-container");
            _pages = new VisualElement[PAGE_COUNT];
            string[] pageNames = { "page-ip","page-latency","page-download","page-schedule",
                                   "page-hosts","page-output","page-other","page-about","page-results",
                                   "page-log","page-hook" };
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                _pages[i] = FindPageRoot(pageContainer, pageNames[i]);
            }
        }

        private VisualElement FindPageRoot(VisualElement pageContainer, string pageName)
        {
            if (pageContainer == null)
                return _root?.Q<VisualElement>(pageName);

            var direct = _root?.Q<VisualElement>(pageName);
            if (direct != null)
                return direct;

            foreach (var child in pageContainer.Children())
            {
                if (child == null) continue;
                if (child.name == pageName)
                    return child;

                var nested = child.Q<VisualElement>(pageName);
                if (nested != null)
                    return nested;
            }

            UnityEngine.Debug.LogWarning("[UI] page root not found: " + pageName);
            return null;
        }

        private void InitPages()
        {
            if (_pages == null || _pages.Length < PAGE_COUNT)
            {
                UnityEngine.Debug.LogError("[UI] page array not initialized");
                return;
            }

            InitPage(PageIpSource, _pages[0], nameof(PageIpSource));
            InitPage(PageLatency,  _pages[1], nameof(PageLatency));
            InitPage(PageDownload, _pages[2], nameof(PageDownload));
            InitPage(PageSchedule, _pages[3], nameof(PageSchedule));
            InitPage(PageHosts,    _pages[4], nameof(PageHosts));
            InitPage(PageOutput,   _pages[5], nameof(PageOutput));
            InitPage(PageOther,    _pages[6], nameof(PageOther));
            InitPage(PageAbout,    _pages[7], nameof(PageAbout));
            InitPage(PageResults,  _pages[8], nameof(PageResults));
            InitPage(PageLog,      _pages[9], nameof(PageLog));
            InitPage(PageHook,     _pages[10], nameof(PageHook));
            if (PageHook  != null) PageHook.LogController  = PageLog;
            if (PageOther != null) PageOther.LogController = PageLog;
            BindScheduleManager();
        }

        private void InitPage(PageIpSourceController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageLatencyController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageDownloadController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageScheduleController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageHostsController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageOutputController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageOtherController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageAboutController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageResultsController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageLogController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void InitPage(PageHookController controller, VisualElement root, string pageName)
        {
            if (controller == null || root == null)
            {
                UnityEngine.Debug.LogError($"[UI] init skipped: {pageName}, controller={(controller != null)}, root={(root != null)}");
                return;
            }
            controller.Init(root, Options);
        }

        private void BindScheduleManager()
        {
            var mgr = ScheduleManager.Instance;
            if (mgr == null) return;
            if (_scheduleManagerBound)
            {
                mgr.OnStateChanged -= RefreshSidebar;
            }
            mgr.StartTestAction  = () => StartTest(fromScheduler: true);
            mgr.HookController   = PageHook;
            mgr.LogController    = PageLog;
            // 调度状态变更时刷新状态栏
            mgr.OnStateChanged  += RefreshSidebar;
            _scheduleManagerBound = true;
        }

        private void UnbindScheduleManager()
        {
            var mgr = ScheduleManager.Instance;
            if (mgr != null)
            {
                mgr.OnStateChanged -= RefreshSidebar;
            }
            _scheduleManagerBound = false;
        }

        private void BindNav()
        {
            if (_navBound) return;
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                int idx = i;
                if (_navBtns[i] == null)
                {
                    UnityEngine.Debug.LogWarning("[NAV] _navBtns[" + i + "] is null, skip bind");
                    continue;
                }

                _navClickHandlers[i] = () => OnNavClicked(idx);
                _navBtns[i].clicked += _navClickHandlers[i];
            }
            _navBound = true;
        }

        private void UnbindNav()
        {
            if (!_navBound || _navBtns == null) return;
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                if (_navBtns[i] == null || _navClickHandlers[i] == null) continue;
                _navBtns[i].clicked -= _navClickHandlers[i];
                _navClickHandlers[i] = null;
            }
            _navBound = false;
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
            if (_buttonsBound) return;
            if (_btnStart != null) _btnStart.clicked += OnStartClicked;
            if (_btnStop  != null) _btnStop.clicked  += OnStopClicked;
            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound) return;
            if (_btnStart != null) _btnStart.clicked -= OnStartClicked;
            if (_btnStop  != null) _btnStop.clicked  -= OnStopClicked;
            _buttonsBound = false;
        }

        private void OnStartClicked()
        {
            StartTest();
        }

        private void OnStopClicked()
        {
            StopTest();
        }

        private void OnNavClicked(int idx)
        {
            UnityEngine.Debug.Log("[NAV] Click idx=" + idx);
            NavigateTo(idx);
        }

        // ── 托盘业务菜单项注册 ────────────────────────────────
        // 由 TrayBridge.TrayReady 事件触发，与 TrayBridge 解耦
        private void RegisterTrayMenuItems()
        {
            var trayBridge = GetComponent<TrayBridge>() ?? FindObjectOfType<TrayBridge>();
            if (trayBridge == null) return;

            CfstTrayManager.Instance.Init(
                onStartTest: () => StartTest(),
                onStopTest:  StopTest,
                isRunning:   () => AppState.Instance.IsRunning,
                trayBridge:  trayBridge
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

            // Hosts 参数详细日志（避免只看到数量）
            if (Options.HostsDomains != null && Options.HostsDomains.Count > 0)
            {
                string hostDomains = string.Join(", ", Options.HostsDomains
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Domain))
                    .Select(x => $"{x.Domain.Trim()}(rank={Mathf.Max(1, x.IpRank)})"));
                string hostFile = string.IsNullOrWhiteSpace(Options.HostsFile)
                    ? "(default system hosts)"
                    : Options.HostsFile.Trim();
                string hostDryRun = Options.HostsDryRun ? "true" : "false";

                PageOther?.AppendLog("[HOST] domains=" + hostDomains);
                PageOther?.AppendLog("[HOST] file=" + hostFile);
                PageOther?.AppendLog("[HOST] dry-run=" + hostDryRun);

                PageLog?.AppendLog("[HOST] domains=" + hostDomains);
                PageLog?.AppendLog("[HOST] file=" + hostFile);
                PageLog?.AppendLog("[HOST] dry-run=" + hostDryRun);
            }

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
            if (!_isMobileStructure)
                CfstTrayManager.Instance.OnRunningStateChanged(false);
            if (!_isMobileStructure && exitCode == 0)
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
            if (!_isMobileStructure)
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
            {
                _sbNextRun.text = schedEnabled
                    ? "⏰ 下次: " + mgr.NextRunAt.Value.ToString("MM-dd HH:mm:ss")
                    : "";
                _sbNextRun.style.display = schedEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_sbNextRunSep != null)
            {
                _sbNextRunSep.text = schedEnabled ? "|" : "";
                _sbNextRunSep.style.display = schedEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
            if (!_isMobileStructure)
            {
                try { isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }
            }
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
