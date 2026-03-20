// ============================================================
// PageHookController.cs  —  执行钩子页面控制器
// RunPreHook / RunPostHook 改用 ProcessMgr 统一执行。
// ============================================================
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    public class PageHookController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;
        private Toggle       _preEnabled;
        private TextField    _prePath;
        private TextField    _preArgs;
        private IntegerField _preTimeout;
        private Toggle       _preWait;
        private Label        _preStatusLabel;
        private Label        _prePathHint;
        private Toggle       _postEnabled;
        private TextField    _postPath;
        private TextField    _postArgs;
        private IntegerField _postTimeout;
        private Toggle       _postOnlySuccess;
        private Label        _postStatusLabel;
        private Label        _postPathHint;
        public PageLogController LogController { get; set; }

        // ── 供 ScheduleManager 查询 ───────────────────────────
        public bool IsPreHookEnabled  => _opts != null && _opts.PreHookEnabled;
        public bool IsPostHookEnabled => _opts != null && _opts.PostHookEnabled;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root; _opts = opts;
            _preEnabled     = root.Q<Toggle>("toggle-pre-enabled");
            _prePath        = root.Q<TextField>("field-pre-path");
            _preArgs        = root.Q<TextField>("field-pre-args");
            _preTimeout     = root.Q<IntegerField>("field-pre-timeout");
            _preWait        = root.Q<Toggle>("toggle-pre-wait");
            _preStatusLabel = root.Q<Label>("label-pre-status");
            _prePathHint    = root.Q<Label>("label-pre-path-hint");
            _postEnabled     = root.Q<Toggle>("toggle-post-enabled");
            _postPath        = root.Q<TextField>("field-post-path");
            _postArgs        = root.Q<TextField>("field-post-args");
            _postTimeout     = root.Q<IntegerField>("field-post-timeout");
            _postOnlySuccess = root.Q<Toggle>("toggle-post-only-success");
            _postStatusLabel = root.Q<Label>("label-post-status");
            _postPathHint    = root.Q<Label>("label-post-path-hint");
            RestoreFromOptions();
            _preEnabled?.RegisterValueChangedCallback(e => {
                _opts.PreHookEnabled = e.newValue;
                UpdatePreEnabledState();
                ToastManager.Info(e.newValue ? "已启用前置钩子" : "已禁用前置钩子");
            });
            _prePath?.RegisterValueChangedCallback(e => { _opts.PreHookPath = e.newValue; UpdatePathHint(_prePathHint, e.newValue); });
            _preArgs?.RegisterValueChangedCallback(e    => _opts.PreHookArgs       = e.newValue);
            _preTimeout?.RegisterValueChangedCallback(e => _opts.PreHookTimeoutSec = e.newValue < 0 ? 0 : e.newValue);
            _preWait?.RegisterValueChangedCallback(e    => _opts.PreHookWait       = e.newValue);
            _postEnabled?.RegisterValueChangedCallback(e => {
                _opts.PostHookEnabled = e.newValue;
                UpdatePostEnabledState();
                ToastManager.Info(e.newValue ? "已启用后置钩子" : "已禁用后置钩子");
            });
            _postPath?.RegisterValueChangedCallback(e => { _opts.PostHookPath = e.newValue; UpdatePathHint(_postPathHint, e.newValue); });
            _postArgs?.RegisterValueChangedCallback(e        => _opts.PostHookArgs        = e.newValue);
            _postTimeout?.RegisterValueChangedCallback(e     => _opts.PostHookTimeoutSec  = e.newValue < 0 ? 0 : e.newValue);
            _postOnlySuccess?.RegisterValueChangedCallback(e => _opts.PostHookOnlySuccess = e.newValue);
            root.Q<Button>("btn-pre-browse") ?.RegisterCallback<ClickEvent>(_ => BrowsePath(_prePath));
            root.Q<Button>("btn-post-browse")?.RegisterCallback<ClickEvent>(_ => BrowsePath(_postPath));
            root.Q<Button>("btn-hook-test-pre") ?.RegisterCallback<ClickEvent>(_ => TestHook(false));
            root.Q<Button>("btn-hook-test-post")?.RegisterCallback<ClickEvent>(_ => TestHook(true));
            UpdatePreEnabledState(); UpdatePostEnabledState();
        }

        private void RestoreFromOptions()
        {
            _preEnabled ?.SetValueWithoutNotify(_opts.PreHookEnabled);
            _prePath    ?.SetValueWithoutNotify(_opts.PreHookPath    ?? "");
            _preArgs    ?.SetValueWithoutNotify(_opts.PreHookArgs    ?? "");
            _preTimeout ?.SetValueWithoutNotify(_opts.PreHookTimeoutSec);
            _preWait    ?.SetValueWithoutNotify(_opts.PreHookWait);
            _postEnabled    ?.SetValueWithoutNotify(_opts.PostHookEnabled);
            _postPath       ?.SetValueWithoutNotify(_opts.PostHookPath    ?? "");
            _postArgs       ?.SetValueWithoutNotify(_opts.PostHookArgs    ?? "");
            _postTimeout    ?.SetValueWithoutNotify(_opts.PostHookTimeoutSec);
            _postOnlySuccess?.SetValueWithoutNotify(_opts.PostHookOnlySuccess);
            UpdatePathHint(_prePathHint,  _opts.PreHookPath);
            UpdatePathHint(_postPathHint, _opts.PostHookPath);
        }

        private void UpdatePreEnabledState()
        {
            bool en = _opts.PreHookEnabled;
            _prePath?.SetEnabled(en); _preArgs?.SetEnabled(en);
            _preTimeout?.SetEnabled(en); _preWait?.SetEnabled(en);
            _root?.Q<Button>("btn-pre-browse")    ?.SetEnabled(en);
            _root?.Q<Button>("btn-hook-test-pre") ?.SetEnabled(en);
        }

        private void UpdatePostEnabledState()
        {
            bool en = _opts.PostHookEnabled;
            _postPath?.SetEnabled(en); _postArgs?.SetEnabled(en);
            _postTimeout?.SetEnabled(en); _postOnlySuccess?.SetEnabled(en);
            _root?.Q<Button>("btn-post-browse")   ?.SetEnabled(en);
            _root?.Q<Button>("btn-hook-test-post")?.SetEnabled(en);
        }

        private static void UpdatePathHint(Label hint, string path)
        {
            if (hint == null) return;
            if (string.IsNullOrWhiteSpace(path)) { hint.text = ""; return; }
            hint.text = Path.GetExtension(path).ToLowerInvariant() == ".exe"
                ? "Windows executable"
                : "Executable program";
        }

        private void BrowsePath(TextField t)
        {
            if (t == null) return;
            string filter = NativePlatform.FileDialog.CreateFilter(
                "Programs (*.exe)", "*.exe",
                "All files (*.*)", "*.*");
            string path = NativePlatform.FileDialog.OpenFilePanel("Select program", filter, null, GetInitDir(t.value));
            if (!string.IsNullOrEmpty(path)) t.value = path;
        }

        private static string GetInitDir(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            try { string d = Path.GetDirectoryName(p); return Directory.Exists(d) ? d : ""; }
            catch { return ""; }
        }

        // ── 测试按钮（UI 手动测试）──────────────────────────
        private void TestHook(bool isPost)
        {
            var    lbl     = isPost ? _postStatusLabel : _preStatusLabel;
            string tag     = isPost ? "post" : "pre";
            string path    = isPost ? _opts.PostHookPath    : _opts.PreHookPath;
            string args    = isPost ? _opts.PostHookArgs    : _opts.PreHookArgs;
            int    timeout = isPost ? _opts.PostHookTimeoutSec : _opts.PreHookTimeoutSec;

            SetStatus(lbl, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            LogController?.AppendLog("[HOOK] Testing " + tag + " hook: " + path);

            int    code = ProcessMgr.Run(path, args, timeout, "[HOOK-TEST]");
            string msg  = ProcessMgr.DescribeExitCode(code);
            bool   ok   = ProcessMgr.IsSuccess(code);

            SetStatus(lbl, msg, ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] " + tag + " result: " + msg);
            if (ok) ToastManager.Success(tag + " hook OK");
            else    ToastManager.Error(tag + " hook: " + msg);
        }

        // ── 供 MainWindowController / ScheduleManager 调用 ──

        /// <summary>运行前钩子，返回是否应继续测速。</summary>
        public bool RunPreHook()
        {
            if (!_opts.PreHookEnabled) return true;
            LogController?.AppendLog("[HOOK] Running pre-hook: " + _opts.PreHookPath);
            SetStatus(_preStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");

            int    code = ProcessMgr.Run(_opts.PreHookPath, _opts.PreHookArgs,
                                         _opts.PreHookTimeoutSec, "[PRE-HOOK]");
            string msg  = ProcessMgr.DescribeExitCode(code);
            bool   ok   = ProcessMgr.IsSuccess(code);

            SetStatus(_preStatusLabel, msg,
                ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running",
                ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Pre-hook done: " + msg);

            if (!ok && _opts.PreHookWait) { ToastManager.Warning("Pre-hook failed, cancelled"); return false; }
            return true;
        }

        /// <summary>运行后钩子。</summary>
        public void RunPostHook(int testExitCode)
        {
            if (!_opts.PostHookEnabled) return;
            if (_opts.PostHookOnlySuccess && testExitCode != 0) return;
            LogController?.AppendLog("[HOOK] Running post-hook: " + _opts.PostHookPath);
            SetStatus(_postStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");

            int    code = ProcessMgr.Run(_opts.PostHookPath, _opts.PostHookArgs,
                                          _opts.PostHookTimeoutSec, "[POST-HOOK]");
            string msg  = ProcessMgr.DescribeExitCode(code);
            bool   ok   = ProcessMgr.IsSuccess(code);

            SetStatus(_postStatusLabel, msg,
                ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running",
                ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Post-hook done: " + msg);
        }

        private static void SetStatus(Label label, string text, string add, string r1 = null, string r2 = null)
        {
            if (label == null) return;
            label.text = text;
            if (r1 != null) label.RemoveFromClassList(r1);
            if (r2 != null) label.RemoveFromClassList(r2);
            label.AddToClassList(add);
        }
    }
}
