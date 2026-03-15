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
        public PageResultsController   PageResults;

        private UIDocument    _doc;
        private VisualElement _root;
        private Button[]        _navBtns;
        private VisualElement[] _pages;
        private const int PAGE_COUNT = 8;

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
        private CfstProcessManager _procManager;
        private System.Collections.IEnumerator _timerRoutine;

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
            AppState.Instance.OnChanged          += RefreshSidebar;
            TestResult.Instance.OnResultUpdated  += RefreshBadge;
            NavigateTo(0);
            RefreshSidebar();
        }

        private void OnDisable()
        {
            AppState.Instance.OnChanged          -= RefreshSidebar;
            TestResult.Instance.OnResultUpdated  -= RefreshBadge;
            _procManager?.Dispose();
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

            _navBtns = new Button[PAGE_COUNT];
            _navBtns[0] = _root.Q<Button>("nav-ip");
            _navBtns[1] = _root.Q<Button>("nav-latency");
            _navBtns[2] = _root.Q<Button>("nav-download");
            _navBtns[3] = _root.Q<Button>("nav-schedule");
            _navBtns[4] = _root.Q<Button>("nav-hosts");
            _navBtns[5] = _root.Q<Button>("nav-output");
            _navBtns[6] = _root.Q<Button>("nav-other");
            _navBtns[7] = _root.Q<Button>("nav-results");

            _pages = new VisualElement[PAGE_COUNT];
            _pages[0] = _root.Q<VisualElement>("page-ip");
            _pages[1] = _root.Q<VisualElement>("page-latency");
            _pages[2] = _root.Q<VisualElement>("page-download");
            _pages[3] = _root.Q<VisualElement>("page-schedule");
            _pages[4] = _root.Q<VisualElement>("page-hosts");
            _pages[5] = _root.Q<VisualElement>("page-output");
            _pages[6] = _root.Q<VisualElement>("page-other");
            _pages[7] = _root.Q<VisualElement>("page-results");
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
            PageResults ?.Init(_pages[7], Options);
        }

        private void BindNav()
        {
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                int idx = i;
                _navBtns[i]?.RegisterCallback<ClickEvent>(_ => NavigateTo(idx));
            }
        }

        public void NavigateTo(int idx)
        {
            AppState.Instance.CurrentPage = idx;
            for (int i = 0; i < PAGE_COUNT; i++)
            {
                bool active = i == idx;
                if (_pages[i] != null)
                {
                    if (active) _pages[i].AddToClassList("page--active");
                    else        _pages[i].RemoveFromClassList("page--active");
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
            _btnStart?.RegisterCallback<ClickEvent>(_ => StartTest());
            _btnStop ?.RegisterCallback<ClickEvent>(_ => StopTest());
        }

        private void StartTest()
        {
            if (AppState.Instance.IsRunning) return;

            // StreamingAssets 在各平台的实际路径
            string exePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.streamingAssetsPath, "windows", "cfst.exe"));

            UnityEngine.Debug.Log($"[CFST] StreamingAssetsPath = {Application.streamingAssetsPath}");
            UnityEngine.Debug.Log($"[CFST] exePath (full) = {exePath}");
            UnityEngine.Debug.Log($"[CFST] exePath exists = {System.IO.File.Exists(exePath)}");

            if (!System.IO.File.Exists(exePath))
            {
                string msg = $"找不到 cfst.exe\n期望路径: {exePath}";
                UnityEngine.Debug.LogError($"[CFST] {msg}");
                AppState.Instance.StatusText = "找不到 cfst.exe";
                PageOther?.AppendLog($"[ERROR] {msg}");
                return;
            }

            TestResult.Instance.Clear();
            AppState.Instance.Reset();
            AppState.Instance.IsRunning  = true;
            AppState.Instance.StatusText = "启动中...";
            AppState.Instance.StartTime  = DateTime.Now;

            // 打印完整命令行参数
            string args = Options.ToArguments();
            string fullCmd = Options.ToFullCommand(exePath);
            UnityEngine.Debug.Log($"[CFST] Arguments = \"{args}\"");
            UnityEngine.Debug.Log($"[CFST] Full command = {fullCmd}");
            PageOther?.AppendLog($"[CMD] {fullCmd}");

            _procManager?.Dispose();
            _procManager = new CfstProcessManager(exePath);
            _procManager.OnStarted += () =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    UnityEngine.Debug.Log($"[CFST] Process started, PID = {_procManager.ProcessId}");
                    PageOther?.AppendLog($"[INFO] 进程已启动 PID={_procManager.ProcessId}");
                    AppState.Instance.StatusText = "延迟测速中...";
                });
            _procManager.OnOutput += line =>
                UnityMainThreadDispatcher.Enqueue(() => HandleOutput(line));
            _procManager.OnError  += line =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    UnityEngine.Debug.LogWarning($"[CFST][stderr] {line}");
                    HandleOutput(line);
                });
            _procManager.OnExited += code =>
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    UnityEngine.Debug.Log($"[CFST] Process exited, code = {code}");
                    HandleExited(code);
                });

            _procManager.StartAsync(Options);
            StartCoroutine(ElapsedTimer());
        }

        private void StopTest()
        {
            UnityEngine.Debug.Log("[CFST] StopTest called");
            PageOther?.AppendLog("[INFO] 用户请求停止测速");
            _procManager?.Stop();
            AppState.Instance.IsRunning  = false;
            AppState.Instance.StatusText = "已停止";
        }

        private void HandleOutput(string line)
        {
            PageOther?.AppendLog(line);
            OutputParser.Parse(line, AppState.Instance, TestResult.Instance);
        }

        private void HandleExited(int code)
        {
            AppState.Instance.IsRunning  = false;
            AppState.Instance.FinishTime = DateTime.Now;
            AppState.Instance.StatusText = code == 0 ? "已完成" : $"已完成 (exit {code})";
            PageResults?.RefreshResults();
        }

        private System.Collections.IEnumerator ElapsedTimer()
        {
            while (AppState.Instance.IsRunning)
            {
                AppState.Instance.Elapsed =
                    DateTime.Now - AppState.Instance.StartTime;
                yield return new WaitForSeconds(1f);
            }
        }

        private void RefreshSidebar()
        {
            var s = AppState.Instance;
            float pct = s.Progress * 100f;
            if (_progressFill != null)
                _progressFill.style.width =
                    new StyleLength(new Length(pct, LengthUnit.Percent));
            if (_progressPct   != null) _progressPct.text   = $"{pct:F0}%";
            if (_sidebarStatus != null) _sidebarStatus.text = s.StatusText;

            if (_btnStart != null) _btnStart.SetEnabled(!s.IsRunning);
            if (_btnStop  != null) _btnStop.SetEnabled(s.IsRunning);
            if (_btnStop  != null)
            {
                if (s.IsRunning) _btnStop.RemoveFromClassList("btn-stop--disabled");
                else             _btnStop.AddToClassList("btn-stop--disabled");
            }

            if (_sbStatus  != null) _sbStatus.text  = $"@ {s.StatusText}";
            if (_sbTested  != null)
                _sbTested.text = s.TotalCount > 0
                    ? $"已测: {s.TestedCount}/{s.TotalCount}"
                    : "已测: --";
            if (_sbElapsed != null)
                _sbElapsed.text = s.Elapsed > TimeSpan.Zero
                    ? $"耗时: {s.Elapsed:mm\\:ss}" : "耗时: --";
            if (_sbBest != null)
            {
                string best = s.BestLatency >= 0
                    ? $"当前最快: {s.BestLatency:F0}ms"
                      + (s.BestSpeed >= 0 ? $" / {s.BestSpeed:F2} MB/s" : "")
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
            else _resultBadge.AddToClassList("result-badge--hidden");
        }
    }
}
