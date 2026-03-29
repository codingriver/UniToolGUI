using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
using NativeKit;
using System.Threading.Tasks;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CloudflareST.GUI
{
    public enum WindowsStandaloneWindowPreset
    {
        Compact1120x720,
        Recommended1200x800,
        Large1280x860
    }

    public enum MacStandaloneWindowPreset
    {
        Compact1024x700,
        Recommended1080x720,
        Balanced1120x760,
        Large1200x800
    }

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

        [Header("Platform UI")]
        [Tooltip("仅 macOS 独立版生效，用于微调整体 UI 缩放；Windows 平台不会使用该值。建议范围 0.90 ~ 1.00，默认 0.95。")]
        public float MacStandaloneUiScale = 0.95f;
        [Tooltip("仅 macOS 独立版生效，用于选择工具窗口默认尺寸预设；Windows 平台不会使用该选项。")]
        public MacStandaloneWindowPreset MacWindowPreset = MacStandaloneWindowPreset.Recommended1080x720;
        [SerializeField, HideInInspector] private MacStandaloneWindowPreset _lastMacWindowPreset = MacStandaloneWindowPreset.Recommended1080x720;
        [Tooltip("仅 Windows 独立版生效，用于选择工具窗口默认尺寸预设；macOS 平台不会使用该选项。")]
        public WindowsStandaloneWindowPreset WindowsWindowPreset = WindowsStandaloneWindowPreset.Recommended1200x800;
        [SerializeField, HideInInspector] private WindowsStandaloneWindowPreset _lastWindowsWindowPreset = WindowsStandaloneWindowPreset.Recommended1200x800;
        [Tooltip("仅在 Unity 编辑器中生效：切换桌面平台窗口预设时，同步修改当前激活目标平台的默认启动分辨率，减少 Play/本地测试时窗口尺寸跳变。正式构建前还会由 Editor 构建钩子自动覆盖为目标平台预设。")]
        public bool SyncMacPresetToProjectSettings = true;

        [Header("About Page")]
        public string AboutGuiVersionText = string.Empty;
        public string AboutDllVersionText = string.Empty;
        public string AboutRepoUrl = "";
        public string AboutRepoButtonText = "查看源码仓库";
        public string AboutLicenseText = "MIT License";

        private UIDocument    _doc;
        private VisualElement _root;
        private Button[]        _navBtns;
        private VisualElement[] _pages;
        private const int PAGE_COUNT   = 10;
        private const int PAGE_RESULTS = 8;

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
            EnsureAboutDefaults();
            EnsurePageControllers();
        }

        private void OnValidate()
        {
            if (_lastMacWindowPreset != MacWindowPreset)
            {
                MacStandaloneUiScale = GetRecommendedMacScale(MacWindowPreset);
                _lastMacWindowPreset = MacWindowPreset;
            }

            if (_lastWindowsWindowPreset != WindowsWindowPreset)
                _lastWindowsWindowPreset = WindowsWindowPreset;

#if UNITY_EDITOR
            SyncProjectSettingsResolutionForActiveDesktopTarget();
#endif
            EnsureAboutDefaults();
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
            AppState.Instance.OnChanged         -= RefreshSidebar;
            TestResult.Instance.OnResultUpdated -= RefreshBadge;
            UnbindButtons();
            UnbindNav();
            UnbindScheduleManager();
            _runner?.Dispose();
            _runner = null;
            _initialized = false;

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            MacHelperService.OnEvent -= OnMacHelperStatusEvent;
            MacHelperInstallService.OnHelperStateChanged -= OnMacHelperStateChanged;
            if (_macHelperRefreshCoroutine != null)
            {
                StopCoroutine(_macHelperRefreshCoroutine);
                _macHelperRefreshCoroutine = null;
            }
#endif
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
            ApplyPlatformUiScale();

            BindElements();
            LogVisualTreeDiagnostics();

            bool hasAllPages = _pages != null && _pages.All(page => page != null);
            if (!hasAllPages)
            {
                UnityEngine.Debug.LogWarning("[UI] initialization deferred: page roots are not ready");
                return;
            }

            InitPages();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PageLatencyController.LogPingModeGroupFromRoot(_root);
#endif
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

            _initialized = true;
        }

        private void ApplyPlatformUiScale()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (_doc == null || _doc.panelSettings == null)
                return;

            var ps = _doc.panelSettings;

            // ── 根本原因 ────────────────────────────────────────────────────────────
            // PanelSettings.ScaleMode = ConstantPhysicalSize（值=2）时，
            // UI Toolkit 用 Screen.dpi / ReferenceDpi 计算缩放系数。
            // macOS 上 Screen.dpi 通常返回 72（逻辑 DPI），ReferenceDpi=96，
            // 导致 scale = 72/96 = 0.75，界面整体缩小 25%。
            //
            // 修复方案：在运行时切换为 ScaleWithScreenSize（值=0），
            // 以参考分辨率（1200x800）为基准做比例缩放，与 Windows 观感一致。
            // 这里不再额外做 Retina 放大补偿，否则控件会明显比 Windows 更大。
            // ────────────────────────────────────────────────────────────────────────

            // 切换 ScaleMode 为 ScaleWithScreenSize
            // PanelSettings.scaleMode 是 enum PanelScaleMode { ConstantPixelSize=0, ScaleWithScreenSize=1, ConstantPhysicalSize=2 }
            // 运行时直接赋值即可（不影响 Asset 文件，仅本次运行生效）
            try
            {
                // 用反射读写，避免版本差异导致编译失败
                var scaleModeProp = ps.GetType().GetProperty("scaleMode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (scaleModeProp != null)
                {
                    // PanelScaleMode.ScaleWithScreenSize = 1
                    var enumType = scaleModeProp.PropertyType;
                    var val = System.Enum.ToObject(enumType, 1);
                    scaleModeProp.SetValue(ps, val);
                    UnityEngine.Debug.Log("[UI] macOS: PanelSettings.scaleMode -> ScaleWithScreenSize");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[UI] macOS: scaleMode 设置失败: " + ex.Message);
            }

            // Retina 补偿：物理分辨率 / 逻辑分辨率 > 1.5 认为是 Retina 屏
            float backingRatio = 1f;
            try
            {
                if (Screen.width > 0 && Screen.currentResolution.width > 0)
                    backingRatio = (float)Screen.currentResolution.width / Screen.width;
            }
            catch { }

            float targetScale = Mathf.Clamp(MacStandaloneUiScale, 0.75f, 1.25f);
            if (!Mathf.Approximately(ps.scale, targetScale))
                ps.scale = targetScale;

            UnityEngine.Debug.Log($"[UI] macOS scale applied: scale={ps.scale:F2} inspectorScale={MacStandaloneUiScale:F2} backingRatio={backingRatio:F2} screenDpi={Screen.dpi:F0}");

            // ── DPI 自适应窗口物理尺寸 ────────────────────────────────────────────
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            ApplyMacWindowSize(backingRatio);
#endif
#endif
        }

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
        private void ApplyMacWindowSize(float backingRatio)
        {
            var preset = GetMacWindowPresetSize(MacWindowPreset);
            int refW = preset.width;
            int refH = preset.height;

            // Screen.currentResolution 在 macOS 返回物理像素分辨率
            // backingRatio = 物理像素 / 逻辑点（Retina=2，异常值时回退到 2）
            float physW = Screen.currentResolution.width;
            float physH = Screen.currentResolution.height;
            float backingScale = backingRatio >= 1.4f ? Mathf.Round(backingRatio) : 1f;
            if (backingScale > 3f) backingScale = 2f; // 避免偶发 4.x 导致坐标异常

            float logicW = physW / backingScale;
            float logicH = physH / backingScale;

            int maxAllowedW = Mathf.RoundToInt(logicW * 0.90f);
            int maxAllowedH = Mathf.RoundToInt(logicH * 0.90f);
            int targetW = refW;
            int targetH = refH;

            bool needsClampForSmallScreen = refW > maxAllowedW || refH > maxAllowedH;
            if (needsClampForSmallScreen)
            {
                targetW = Mathf.Min(refW, maxAllowedW);
                targetH = Mathf.Min(refH, maxAllowedH);
            }

            bool alreadyMatchesStartupSize =
                Mathf.Abs(Screen.width - targetW) <= 2 &&
                Mathf.Abs(Screen.height - targetH) <= 2;

            if (alreadyMatchesStartupSize && !needsClampForSmallScreen)
            {
                UnityEngine.Debug.Log(
                    $"[UI] macOS window size already matches startup size, skip runtime resize: " +
                    $"screen={Screen.width}x{Screen.height}, target={targetW}x{targetH}");
                return;
            }

            // 使用当前窗口中心点计算目标位置，避免启动后跳到右下角
            int posX;
            int posY;
            try
            {
                if (MacWindowPlugin.GetFrame(out int curX, out int curY, out int curW, out int curH) && curW > 0 && curH > 0)
                {
                    int centerX = curX + curW / 2;
                    int centerY = curY + curH / 2;
                    posX = centerX - targetW / 2;
                    posY = centerY - targetH / 2;
                }
                else
                {
                    posX = Mathf.RoundToInt((logicW - targetW) / 2f);
                    posY = Mathf.RoundToInt((logicH - targetH) / 2f);
                }
            }
            catch
            {
                posX = Mathf.RoundToInt((logicW - targetW) / 2f);
                posY = Mathf.RoundToInt((logicH - targetH) / 2f);
            }

            posX = Mathf.Clamp(posX, 0, Mathf.Max(0, Mathf.RoundToInt(logicW - targetW)));
            posY = Mathf.Clamp(posY, 0, Mathf.Max(0, Mathf.RoundToInt(logicH - targetH)));

            UnityEngine.Debug.Log($"[UI] macOS window: {targetW}x{targetH} @ ({posX},{posY}) logical, backingScale={backingScale} screen={logicW}x{logicH} (physical={physW}x{physH})");

            try
            {
                // 延后 1 帧应用，避免 Unity 首帧窗口初始化后再次覆盖位置引起闪动
                StartCoroutine(ApplyMacWindowSizeNextFrame(posX, posY, targetW, targetH));
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning("[UI] macOS SetFrame schedule failed: " + ex.Message);
            }
        }

        private IEnumerator ApplyMacWindowSizeNextFrame(int x, int y, int w, int h)
        {
            yield return null;
            try { MacWindowPlugin.SetFrame(x, y, w, h); }
            catch (System.Exception ex) { UnityEngine.Debug.LogWarning("[UI] macOS SetFrame failed: " + ex.Message); }
        }

#endif

        public static (int width, int height) GetMacWindowPresetSize(MacStandaloneWindowPreset preset)
        {
            switch (preset)
            {
                case MacStandaloneWindowPreset.Compact1024x700:
                    return (1024, 700);
                case MacStandaloneWindowPreset.Balanced1120x760:
                    return (1120, 760);
                case MacStandaloneWindowPreset.Large1200x800:
                    return (1200, 800);
                case MacStandaloneWindowPreset.Recommended1080x720:
                default:
                    return (1080, 720);
            }
        }

        public static float GetRecommendedMacScale(MacStandaloneWindowPreset preset)
        {
            switch (preset)
            {
                case MacStandaloneWindowPreset.Compact1024x700:
                    return 0.93f;
                case MacStandaloneWindowPreset.Balanced1120x760:
                    return 0.97f;
                case MacStandaloneWindowPreset.Large1200x800:
                    return 1.00f;
                case MacStandaloneWindowPreset.Recommended1080x720:
                default:
                    return 0.95f;
            }
        }

        public static (int width, int height) GetWindowsWindowPresetSize(WindowsStandaloneWindowPreset preset)
        {
            switch (preset)
            {
                case WindowsStandaloneWindowPreset.Compact1120x720:
                    return (1120, 720);
                case WindowsStandaloneWindowPreset.Large1280x860:
                    return (1280, 860);
                case WindowsStandaloneWindowPreset.Recommended1200x800:
                default:
                    return (1200, 800);
            }
        }

#if UNITY_EDITOR
        public (int width, int height) GetConfiguredDesktopResolution(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneOSX:
                    return GetMacWindowPresetSize(MacWindowPreset);
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return GetWindowsWindowPresetSize(WindowsWindowPreset);
                default:
                    return (PlayerSettings.defaultScreenWidth, PlayerSettings.defaultScreenHeight);
            }
        }

        private void SyncProjectSettingsResolutionForActiveDesktopTarget()
        {
            if (!SyncMacPresetToProjectSettings)
                return;

            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneOSX &&
                target != BuildTarget.StandaloneWindows &&
                target != BuildTarget.StandaloneWindows64)
                return;

            var preset = GetConfiguredDesktopResolution(target);
            if (PlayerSettings.defaultScreenWidth != preset.width)
                PlayerSettings.defaultScreenWidth = preset.width;
            if (PlayerSettings.defaultScreenHeight != preset.height)
                PlayerSettings.defaultScreenHeight = preset.height;
        }
#endif

        private void LogVisualTreeDiagnostics()
        {
            string assetName = _doc != null && _doc.visualTreeAsset != null ? _doc.visualTreeAsset.name : "<null>";
            string rootName = _root != null ? (_root.name ?? "<unnamed>") : "<null>";
            int rootChildCount = _root != null ? _root.childCount : -1;
            var pageContainer = _root != null ? _root.Q<VisualElement>("page-container") : null;
            int pageContainerChildren = pageContainer != null ? pageContainer.childCount : -1;
            UnityEngine.Debug.Log($"[UI] asset={assetName}, root={rootName}, rootChildren={rootChildCount}, pageContainer={(pageContainer != null)}, pageContainerChildren={pageContainerChildren}, mobile={_isMobileStructure}");
        }

        private void EnsureAboutDefaults()
        {
            if (string.IsNullOrWhiteSpace(AboutGuiVersionText))
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                AboutGuiVersionText = ver != null ? ver.ToString() : "1.0.0";
            }

            if (string.IsNullOrWhiteSpace(AboutDllVersionText))
            {
                try
                {
                    var dllPath = typeof(CloudflareST.CfstRunner).Assembly.Location;
                    var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                    var productVer = fvi.ProductVersion;
                    if (!string.IsNullOrEmpty(productVer))
                    {
                        int plusIdx = productVer.IndexOf('+');
                        AboutDllVersionText = plusIdx > 0 ? productVer.Substring(0, plusIdx) : productVer;
                    }
                    else
                    {
                        var dllVer = typeof(CloudflareST.CfstRunner).Assembly.GetName().Version;
                        AboutDllVersionText = dllVer != null ? dllVer.ToString() : "unknown";
                    }
                }
                catch
                {
                    AboutDllVersionText = "unknown";
                }
            }

            if (string.IsNullOrWhiteSpace(AboutRepoUrl))
                AboutRepoUrl = "https://github.com/codingriver/UniToolGUI";
            if (string.IsNullOrWhiteSpace(AboutRepoButtonText))
                AboutRepoButtonText = "查看源码仓库";
            if (string.IsNullOrWhiteSpace(AboutLicenseText))
                AboutLicenseText = "MIT License";
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
            if (FeatureFlags.HookFeatureEnabled)
                PageHook = EnsureController(PageHook);
            else
                PageHook = null;
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
                                  "nav-log" };
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
                                   "page-log" };
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
            mgr.HookController   = FeatureFlags.HookFeatureEnabled ? PageHook : null;
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
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (IsHostsUpdateEnabled() && !EnsureMacRootHelperReadyForHosts())
                return;
#endif
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

        private bool _startedByScheduler;

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
        private enum MacHelperUiState
        {
            Unknown,
            Checking,
            NotInstalled,
            NotConnected,
            TrustFailed,
            Error,
            Ok
        }

        private bool _macHelperStatusBound;
        private bool _macHelperTrustCheckInFlight;
        private Coroutine _macHelperRefreshCoroutine;
        private MacHelperUiState _macHelperLastState = MacHelperUiState.Unknown;
        private string _macHelperLastDetail = string.Empty;

        private bool IsHostsUpdateEnabled()
        {
            return Options.HostsDomains != null
                && Options.HostsDomains.Count > 0
                && !Options.HostsDryRun;
        }

        private void LogMacHelperCheck(string message, bool isError = false)
        {
            string line = "[MAC-HELPER-CHECK] " + message;
            if (isError)
                UnityEngine.Debug.LogError(line);
            else
                UnityEngine.Debug.Log(line);
            PageOther?.AppendLog(isError ? "[ERROR] " + line : "[INFO] " + line);
            PageLog?.AppendLog(isError ? "[ERROR] " + line : "[INFO] " + line);
        }

        private bool EnsureMacRootHelperReadyForHosts()
        {
            try
            {
                LogMacHelperCheck("开始检查权限组件安装/权限/信任状态");

                var status = MacHelperInstallService.QueryStatus();
                LogMacHelperCheck("当前状态: " + status.message);

                if (!status.isInstalled)
                {
                    LogMacHelperCheck("检测到未安装权限组件");
                    bool confirmInstall = WindowsMessageBox.Confirm(
                        "检测到 macOS 权限组件未安装，更新 hosts 需要先安装。\n是否立即安装？",
                        "需要安装权限组件");
                    LogMacHelperCheck("用户确认安装: " + confirmInstall);
                    if (!confirmInstall)
                    {
                        ToastManager.Warning("未安装权限组件，无法更新 hosts");
                        return false;
                    }

                    if (!MacHelperInstallService.Install(out var installMessage))
                    {
                        LogMacHelperCheck("安装失败: " + installMessage, isError: true);
                        ToastManager.Error("权限组件安装失败: " + installMessage);
                        return false;
                    }

                    LogMacHelperCheck("安装成功: " + installMessage);
                    ToastManager.Success("权限组件安装成功");
                }

                if (!TryPingForTrust(out var pingEvent, out var pingError))
                {
                    bool isTrustError = IsTrustError(pingEvent, pingError);
                    string reason = isTrustError ? "信任校验未通过" : "权限/通信校验未通过";
                    LogMacHelperCheck(reason + ": " + (pingError ?? pingEvent?.Message ?? "unknown"), isError: true);

                    bool confirmReinstall = WindowsMessageBox.Confirm(
                        reason + "，需要重新安装权限组件才能继续。\n是否立即重新安装？",
                        "权限组件需要重装");
                    LogMacHelperCheck("用户确认重装: " + confirmReinstall);
                    if (!confirmReinstall)
                    {
                        ToastManager.Warning(reason + "，已取消开始测试");
                        return false;
                    }

                    if (!MacHelperInstallService.Uninstall(out var uninstallMessage))
                    {
                        LogMacHelperCheck("卸载失败: " + uninstallMessage, isError: true);
                        ToastManager.Error("权限组件卸载失败: " + uninstallMessage);
                        return false;
                    }

                    LogMacHelperCheck("卸载成功: " + uninstallMessage);
                    ToastManager.Success("权限组件卸载成功");

                    if (!MacHelperInstallService.Install(out var reinstallMessage))
                    {
                        LogMacHelperCheck("重新安装失败: " + reinstallMessage, isError: true);
                        ToastManager.Error("权限组件安装失败: " + reinstallMessage);
                        return false;
                    }

                    LogMacHelperCheck("重新安装成功: " + reinstallMessage);
                    ToastManager.Success("权限组件重新安装成功");

                    if (!TryPingForTrust(out var pingEventAfter, out var pingErrorAfter))
                    {
                        LogMacHelperCheck("重装后仍无法通过校验: " + (pingErrorAfter ?? pingEventAfter?.Message ?? "unknown"), isError: true);
                        ToastManager.Error("权限组件校验失败，请检查安装与权限");
                        return false;
                    }
                }

                LogMacHelperCheck("权限组件前置检查通过");
                return true;
            }
            catch (Exception ex)
            {
                LogMacHelperCheck("检查异常: " + ex.Message, isError: true);
                ToastManager.Error("权限组件检查异常: " + ex.Message);
                return false;
            }
        }

        private static bool IsTrustError(MacHelperEvent evt, string errorMessage)
        {
            if (evt != null && evt.ExitCode == 403)
                return true;
            if (!string.IsNullOrEmpty(evt?.Message) && evt.Message.Contains("信任"))
                return true;
            if (!string.IsNullOrEmpty(errorMessage) && errorMessage.Contains("信任"))
                return true;
            return false;
        }

        private static bool TryPingForTrust(out MacHelperEvent pingEvent, out string errorMessage)
        {
            try
            {
                return MacHelperService.Ping(out pingEvent, out errorMessage);
            }
            catch (Exception ex)
            {
                pingEvent = null;
                errorMessage = ex.Message;
                return false;
            }
        }

        private void InitMacHelperStatusLabel()
        {
            if (_sbUserRole == null)
                return;

            if (!_macHelperStatusBound)
            {
                _sbUserRole.RegisterCallback<ClickEvent>(_ => OnMacHelperStatusClicked());
                _macHelperStatusBound = true;
            }

            MacHelperService.OnEvent -= OnMacHelperStatusEvent;
            MacHelperService.OnEvent += OnMacHelperStatusEvent;
            MacHelperInstallService.OnHelperStateChanged -= OnMacHelperStateChanged;
            MacHelperInstallService.OnHelperStateChanged += OnMacHelperStateChanged;

            RefreshMacHelperStatusLabel();
        }

        private void OnMacHelperStateChanged()
        {
            _macHelperTrustCheckInFlight = false;
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                RefreshMacHelperStatusLabel();
                ScheduleMacHelperRefresh();
            });
        }

        private void ScheduleMacHelperRefresh()
        {
            if (_macHelperRefreshCoroutine != null)
                StopCoroutine(_macHelperRefreshCoroutine);
            _macHelperRefreshCoroutine = StartCoroutine(DelayedMacHelperRefresh());
        }

        private IEnumerator DelayedMacHelperRefresh()
        {
            yield return new WaitForSeconds(1.0f);
            _macHelperTrustCheckInFlight = false;
            RefreshMacHelperStatusLabel();
            yield return new WaitForSeconds(2.0f);
            _macHelperTrustCheckInFlight = false;
            RefreshMacHelperStatusLabel();
            _macHelperRefreshCoroutine = null;
        }

        private void OnMacHelperStatusEvent(MacHelperEvent evt)
        {
            if (evt == null)
                return;
            if (evt.EventType == "connection_opened"
                || evt.EventType == "connection_closed"
                || evt.EventType == "connection_error"
                || string.Equals(evt.Action, "trust.refresh", StringComparison.Ordinal)
                || string.Equals(evt.Action, "helper.status", StringComparison.Ordinal)
                || string.Equals(evt.Action, "helper.ping", StringComparison.Ordinal))
                RefreshMacHelperStatusLabel();
        }

        private void RefreshMacHelperStatusLabel()
        {
            if (_sbUserRole == null)
                return;

            try
            {
                MacHelperService.Initialize();
                var status = MacHelperInstallService.QueryStatus();

                if (!status.isInstalled)
                {
                    UpdateMacHelperStatus(MacHelperUiState.NotInstalled, "未安装", "权限组件未安装");
                    return;
                }

                if (!status.isConnected)
                {
                    UpdateMacHelperStatus(MacHelperUiState.NotConnected, "未连接", status.message);
                    return;
                }

                UpdateMacHelperStatus(MacHelperUiState.Checking, "已连接(校验中)", status.message);
                KickoffTrustCheck();
            }
            catch (Exception ex)
            {
                UpdateMacHelperStatus(MacHelperUiState.Error, "状态异常", ex.Message);
            }
        }

        private void KickoffTrustCheck()
        {
            if (_macHelperTrustCheckInFlight)
                return;

            _macHelperTrustCheckInFlight = true;
            Task.Run(() =>
            {
                MacHelperEvent pingEvent;
                string pingError;
                bool ok = TryPingForTrust(out pingEvent, out pingError);
                bool trustFailed = !ok && IsTrustError(pingEvent, pingError);
                string detail = ok
                    ? (pingEvent?.Message ?? "pong")
                    : (pingError ?? pingEvent?.Message ?? "unknown");

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    _macHelperTrustCheckInFlight = false;
                    if (ok)
                        UpdateMacHelperStatus(MacHelperUiState.Ok, "正常", detail);
                    else if (trustFailed)
                        UpdateMacHelperStatus(MacHelperUiState.TrustFailed, "信任失败", detail);
                    else
                        UpdateMacHelperStatus(MacHelperUiState.Error, "连接异常", detail);
                });
            });
        }

        private void UpdateMacHelperStatus(MacHelperUiState state, string shortText, string detail)
        {
            _macHelperLastState = state;
            _macHelperLastDetail = detail ?? string.Empty;
            string suffix = state == MacHelperUiState.Ok || state == MacHelperUiState.Checking
                ? string.Empty
                : " (点击修复)";
            _sbUserRole.text = "权限: " + (shortText ?? "未知") + suffix;
        }

        private void OnMacHelperStatusClicked()
        {
            try
            {
                string detail = string.IsNullOrWhiteSpace(_macHelperLastDetail) ? "无更多信息" : _macHelperLastDetail;
                switch (_macHelperLastState)
                {
                    case MacHelperUiState.NotInstalled:
                        LogMacHelperCheck("点击状态：未安装，提示安装");
                        if (WindowsMessageBox.Confirm("权限组件未安装，更新 hosts 需要先安装。\n详情: " + detail + "\n是否立即安装？", "需要安装权限组件"))
                        {
                            if (MacHelperInstallService.Install(out var msg))
                            {
                                LogMacHelperCheck("安装成功: " + msg);
                                ToastManager.Success("权限组件安装成功");
                            }
                            else
                            {
                                LogMacHelperCheck("安装失败: " + msg, isError: true);
                                ToastManager.Error("权限组件安装失败: " + msg);
                            }
                            RefreshMacHelperStatusLabel();
                        }
                        break;
                    case MacHelperUiState.NotConnected:
                        LogMacHelperCheck("点击状态：未连接，提示重装");
                        if (WindowsMessageBox.Confirm("权限组件未连接或异常，建议重新安装。\n详情: " + detail + "\n是否立即重新安装？", "需要重新安装"))
                        {
                            if (MacHelperInstallService.Uninstall(out var msg1))
                            {
                                LogMacHelperCheck("卸载成功: " + msg1);
                                ToastManager.Success("权限组件卸载成功");
                            }
                            else
                            {
                                LogMacHelperCheck("卸载失败: " + msg1, isError: true);
                                ToastManager.Error("权限组件卸载失败: " + msg1);
                            }

                            if (MacHelperInstallService.Install(out var msg2))
                            {
                                LogMacHelperCheck("重新安装成功: " + msg2);
                                ToastManager.Success("权限组件重新安装成功");
                            }
                            else
                            {
                                LogMacHelperCheck("重新安装失败: " + msg2, isError: true);
                                ToastManager.Error("权限组件重新安装失败: " + msg2);
                            }

                            RefreshMacHelperStatusLabel();
                        }
                        break;
                    case MacHelperUiState.TrustFailed:
                        LogMacHelperCheck("点击状态：信任失败，提示重装");
                        if (WindowsMessageBox.Confirm("权限组件信任校验失败，需要重新安装。\n详情: " + detail + "\n是否立即重新安装？", "信任校验失败"))
                        {
                            if (MacHelperInstallService.Uninstall(out var msg1))
                            {
                                LogMacHelperCheck("卸载成功: " + msg1);
                                ToastManager.Success("权限组件卸载成功");
                            }
                            else
                            {
                                LogMacHelperCheck("卸载失败: " + msg1, isError: true);
                                ToastManager.Error("权限组件卸载失败: " + msg1);
                            }

                            if (MacHelperInstallService.Install(out var msg2))
                            {
                                LogMacHelperCheck("重新安装成功: " + msg2);
                                ToastManager.Success("权限组件重新安装成功");
                            }
                            else
                            {
                                LogMacHelperCheck("重新安装失败: " + msg2, isError: true);
                                ToastManager.Error("权限组件重新安装失败: " + msg2);
                            }

                            RefreshMacHelperStatusLabel();
                        }
                        break;
                    case MacHelperUiState.Error:
                        LogMacHelperCheck("点击状态：连接异常，提示重装");
                        if (WindowsMessageBox.Confirm("权限组件当前异常，建议重新安装。\n详情: " + detail + "\n是否立即重新安装？", "权限组件异常"))
                        {
                            if (MacHelperInstallService.Uninstall(out var msg1))
                            {
                                LogMacHelperCheck("卸载成功: " + msg1);
                                ToastManager.Success("权限组件卸载成功");
                            }
                            else
                            {
                                LogMacHelperCheck("卸载失败: " + msg1, isError: true);
                                ToastManager.Error("权限组件卸载失败: " + msg1);
                            }

                            if (MacHelperInstallService.Install(out var msg2))
                            {
                                LogMacHelperCheck("重新安装成功: " + msg2);
                                ToastManager.Success("权限组件重新安装成功");
                            }
                            else
                            {
                                LogMacHelperCheck("重新安装失败: " + msg2, isError: true);
                                ToastManager.Error("权限组件重新安装失败: " + msg2);
                            }

                            RefreshMacHelperStatusLabel();
                        }
                        break;
                    case MacHelperUiState.Ok:
                        WindowsMessageBox.Info("权限组件状态正常。\n详情: " + detail, "权限组件");
                        break;
                    default:
                        WindowsMessageBox.Info("权限组件状态未知。\n详情: " + detail, "权限组件");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMacHelperCheck("状态弹窗异常: " + ex.Message, isError: true);
            }
        }
#endif

        // ── 开始测速 ─────────────────────────────────────────
        /// <param name="fromScheduler">true 时跳过前/后钩子（钩子功能暂时禁用）</param>
        public void StartTest(bool fromScheduler = false)
        {
            if (AppState.Instance.IsRunning) return;

            // ── 前钩子（非调度器调用时执行）─────────────────
            if (FeatureFlags.HookFeatureEnabled && !fromScheduler && PageHook != null && !PageHook.RunPreHook())
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

            // 注册 Hosts 写入结果回调（解耦 Plugins 程序集与 Scripts 程序集）
            HostsUpdater.OnHostsWriteResult = (ok, denied) =>
            {
                TestResult.Instance.HostsWriteSuccess     = ok;
                TestResult.Instance.HostsPermissionDenied = denied;
            };
            HostsUpdater.OnShowToast          = (title, msg) => NativePlatform.ShowToast(title, msg);
            HostsUpdater.GetDesktopDataDirFunc = () => AppRuntimePaths.GetDesktopDataDir();
            HostsUpdater.OnPrivilegedWrite    = (path, content, log) => PrivilegedHostsWriter.TryWrite(path, content, log);
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            HostsUpdater.OnMacHostsUpdate = (string path, string content, Action<string> log, out string error) =>
                NativeKit.MacHelperService.SubmitHostsUpdate(path, content, log, out error);
#endif

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
                    if (!_startedByScheduler && FeatureFlags.HookFeatureEnabled)
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
            {
                ToastManager.Success("测速完成！共 " + TestResult.Instance.IpList.Count + " 条结果", 4f);

                // Hosts 写入结果 toast
                var tr = TestResult.Instance;
                bool hostsEnabled = Options.HostsDomains != null && Options.HostsDomains.Count > 0;
                if (hostsEnabled)
                {
                    if (Options.HostsDryRun)
                        ToastManager.Info("Hosts 预览已生成，未实际写入（预览模式）", 4f);
                    else if (tr.HostsWriteSuccess)
                        ToastManager.Success("Hosts 更新成功", 4f);
                    else if (tr.HostsPermissionDenied)
                        ToastManager.Warning("Hosts 更新失败：权限不足，内容已输出至 hosts-pending.txt", 5f);
                    else
                        ToastManager.Error("Hosts 更新失败", 4f);
                }
            }
            else if (exitCode == -1)
                ToastManager.Warning("测速已取消");
            else
                ToastManager.Error("测速异常退出 (code=" + exitCode + ")");
            PageResults?.RefreshResults();
            // 有结果时自动导航到结果页（index 8）
            if (exitCode == 0 && TestResult.Instance.IpList.Count > 0)
                NavigateTo(PAGE_RESULTS);

            var s = AppState.Instance;
            // 同时发送系统通知（后台运行时也能收到）
            string title;
            string body;
            if (exitCode == 0)
            {
                title = "CFST 测速完成";
                body = TestResult.Instance.IpList.Count > 0
                    ? string.Format("有效 {0} 个 IP，最快 {1:F0} ms",
                        TestResult.Instance.IpList.Count, s.BestLatency)
                    : "未找到有效 IP";
            }
            else if (exitCode == -1)
            {
                title = "CFST 测速已取消";
                body = "用户取消或已中止";
            }
            else
            {
                title = "CFST 测速失败";
                body = "异常退出 (code=" + exitCode + ")";
            }

            NativePlatform.ShowToast(title, body);

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
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            InitMacHelperStatusLabel();
            return;
#endif
            bool isAdmin = false;
            string user = "unknown";
            if (!_isMobileStructure)
            {
                try
                {
                    isAdmin = WindowsAdmin.IsRunningAsAdmin();
                    user = WindowsAdmin.GetCurrentUserName();
                }
                catch { }
            }
            if (isAdmin)
            {
                _sbUserRole.text = $"管理员({user})";
                _sbUserRole.RemoveFromClassList("sb-user-role--normal");
                _sbUserRole.AddToClassList("sb-user-role--admin");
            }
            else
            {
                _sbUserRole.text = $"普通用户({user})";
                _sbUserRole.RemoveFromClassList("sb-user-role--admin");
                _sbUserRole.AddToClassList("sb-user-role--normal");
            }
        }
    }
}
