using System;
using UnityEngine;
using UnityEngine.UIElements;

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

        private UIDocument    _doc;
        private VisualElement _root;
        private Button[]        _navBtns;
        private VisualElement[] _pages;
        private const int PAGE_COUNT = 9;
        private const int PAGE_RESULTS = 8;

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

        public static readonly CfstOptions Options = new CfstOptions();
        private CfstDllRunner _runner;

        private void Awake()
        {
            _doc  = GetComponent<UIDocument>();
            _root = _doc.rootVisualElement;
        }

        private void OnEnable()
        {
            BindElements();
            InitPages();
            BindNav();
            BindButtons();
            AppState.Instance.OnChanged         += RefreshSidebar;
            TestResult.Instance.OnResultUpdated += RefreshBadge;
            NavigateTo(0);
            RefreshSidebar();
        }

        private void OnDisable()
        {
            AppState.Instance.OnChanged         -= RefreshSidebar;
            TestResult.Instance.OnResultUpdated -= RefreshBadge;
            if (_btnStart != null) _btnStart.clicked -= StartTest;
            if (_btnStop  != null) _btnStop.clicked  -= StopTest;
            _runner?.Dispose();
            _runner = null;
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

            // ── 导航按钮：限定在 nav-list 内查找，避免跨页面误匹配 ──
            var navList = _root.Q<VisualElement>("nav-list");
            UnityEngine.Debug.Log("[NAV] nav-list found: " + (navList != null));

            _navBtns = new Button[PAGE_COUNT];
            string[] navNames = { "nav-ip","nav-latency","nav-download","nav-schedule",
                                  "nav-hosts","nav-output","nav-other","nav-about","nav-results" };
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
                                   "page-hosts","page-output","page-other","page-about","page-results" };
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
        }

        private void BindNav()
        {
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                int idx = i;
                if (_navBtns[i] != null)
                {
                    // Use TrickleDown to receive click before ScrollView can intercept
                    _navBtns[i].RegisterCallback<ClickEvent>(evt =>
                    {
                        UnityEngine.Debug.Log("[NAV] clicked idx=" + idx);
                        NavigateTo(idx);
                        evt.StopPropagation();
                    }, TrickleDown.TrickleDown);
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[NAV] _navBtns[" + i + "] is null, skip bind");
                }
            }
        }

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
            if (_btnStart != null) _btnStart.clicked += StartTest;
            if (_btnStop  != null) _btnStop.clicked  += StopTest;
        }

        // ── 开始测速 ─────────────────────────────────────────
        private void StartTest()
        {
            if (AppState.Instance.IsRunning) return;

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
                return;
            }

            string cfgSummary = Options.ToArguments();
            UnityEngine.Debug.Log("[CFST] Config args: " + cfgSummary);
            PageOther?.AppendLog("[CMD] " + cfgSummary);

            _runner?.Dispose();
            _runner = new CfstDllRunner();

            _runner.OnLog += line =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    PageOther?.AppendLog(line);
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
                    HandleFinished(dllResults == null ? -1 : 0);
                    PageOther?.AppendLog($"完成");
                });

            _runner.OnError += ex =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    UnityEngine.Debug.LogError("[CFST] Runner error: " + ex.Message);
                    PageOther?.AppendLog("[ERROR] " + ex.Message);
                });

            try
            {
                _runner.Start(cfg);
                PageOther?.AppendLog("[INFO] 测速已启动");
                StartCoroutine(ElapsedTimer());
            }
            catch (Exception ex)
            {
                string msg = "启动失败: " + ex.Message;
                UnityEngine.Debug.LogError("[CFST] " + msg);
                AppState.Instance.IsRunning  = false;
                AppState.Instance.StatusText = msg;
                PageOther?.AppendLog("[ERROR] " + msg);
            }
        }

        // ── 停止测速 ─────────────────────────────────────────
        private void StopTest()
        {
            UnityEngine.Debug.Log("[CFST] StopTest called");
            PageOther?.AppendLog("[INFO] 用户请求停止");
            _runner?.Stop();
            AppState.Instance.IsRunning  = false;
            AppState.Instance.StatusText = "已停止";
        }

        // ── 完成处理 ─────────────────────────────────────────
        private void HandleFinished(int exitCode)
        {
            AppState.Instance.IsRunning  = false;
            AppState.Instance.FinishTime = DateTime.Now;
            AppState.Instance.Elapsed    = DateTime.Now - AppState.Instance.StartTime;
            AppState.Instance.StatusText = exitCode == 0 ? "已完成" : (exitCode == -1 ? "已取消" : "完成 (异常)");
            PageResults?.RefreshResults();
            // 有结果时自动导航到结果页（index 8）
            if (exitCode == 0 && TestResult.Instance.IpList.Count > 0)
                NavigateTo(PAGE_RESULTS);
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
    }
}
