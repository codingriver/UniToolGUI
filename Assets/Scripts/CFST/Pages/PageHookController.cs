using System;
using System.Diagnostics;
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
            _preEnabled?.RegisterValueChangedCallback(e => { _opts.PreHookEnabled = e.newValue; UpdatePreEnabledState(); });
            _prePath?.RegisterValueChangedCallback(e => { _opts.PreHookPath = e.newValue; UpdatePathHint(_prePathHint, e.newValue); });
            _preArgs?.RegisterValueChangedCallback(e    => _opts.PreHookArgs       = e.newValue);
            _preTimeout?.RegisterValueChangedCallback(e => _opts.PreHookTimeoutSec = e.newValue < 0 ? 0 : e.newValue);
            _preWait?.RegisterValueChangedCallback(e    => _opts.PreHookWait       = e.newValue);
            _postEnabled?.RegisterValueChangedCallback(e => { _opts.PostHookEnabled = e.newValue; UpdatePostEnabledState(); });
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
        private void TestHook(bool isPost)
        {
            var    lbl     = isPost ? _postStatusLabel : _preStatusLabel;
            string tag     = isPost ? "post" : "pre";
            string path    = isPost ? _opts.PostHookPath    : _opts.PreHookPath;
            string args    = isPost ? _opts.PostHookArgs    : _opts.PreHookArgs;
            int    timeout = isPost ? _opts.PostHookTimeoutSec : _opts.PreHookTimeoutSec;
            SetStatus(lbl, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            LogController?.AppendLog("[HOOK] Testing " + tag + " hook: " + path);
            int    code = RunHookSync(path, args, timeout);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(lbl, msg, ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] " + tag + " result: " + msg);
            if (ok) ToastManager.Success(tag + " hook OK");
            else    ToastManager.Error(tag + " hook: " + msg);
        }

        public bool RunPreHook()
        {
            if (!_opts.PreHookEnabled) return true;
            LogController?.AppendLog("[HOOK] Running pre-hook: " + _opts.PreHookPath);
            SetStatus(_preStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            int    code = RunHookSync(_opts.PreHookPath, _opts.PreHookArgs, _opts.PreHookTimeoutSec);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(_preStatusLabel, msg, ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Pre-hook done: " + msg);
            if (!ok && _opts.PreHookWait) { ToastManager.Warning("Pre-hook failed, cancelled"); return false; }
            return true;
        }

        public void RunPostHook(int testExitCode)
        {
            if (!_opts.PostHookEnabled) return;
            if (_opts.PostHookOnlySuccess && testExitCode != 0) return;
            LogController?.AppendLog("[HOOK] Running post-hook: " + _opts.PostHookPath);
            SetStatus(_postStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            int    code = RunHookSync(_opts.PostHookPath, _opts.PostHookArgs, _opts.PostHookTimeoutSec);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(_postStatusLabel, msg, ok ? "hook-status--ok" : "hook-status--fail",
                "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Post-hook done: " + msg);
        }

        // Direct execution: path is treated as a program, args passed as-is.
        private static int RunHookSync(string path, string args, int timeoutSec)
        {
            if (string.IsNullOrWhiteSpace(path)) return -1;
            if (!File.Exists(path)) return 99;
            var psi = new ProcessStartInfo { FileName = path, Arguments = args ?? "",
                UseShellExecute = false, CreateNoWindow = true };
            try
            {
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return 98;
                    int ms = timeoutSec > 0 ? timeoutSec * 1000 : -1;
                    bool exited = proc.WaitForExit(ms);
                    if (!exited) { try { proc.Kill(); } catch { } return -2; }
                    return proc.ExitCode;
                }
            }
            catch (Exception ex) { UnityEngine.Debug.LogError("[HOOK] " + ex.Message); return 97; }
        }

        private static string FormatExitMsg(int c) =>
            c ==  0 ? "OK (exit 0)" :
            c == -1 ? "Not configured" :
            c == -2 ? "Timed out" :
            c == 97 ? "Launch exception" :
            c == 98 ? "Process start failed" :
            c == 99 ? "File not found" :
            "Failed (exit " + c + ")";

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