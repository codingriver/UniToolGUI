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
        private VisualElement    _root;
        private CfstOptions      _opts;
        private Toggle           _preEnabled;
        private RadioButtonGroup _preType;
        private TextField        _preScript;
        private TextField        _preProgram;
        private TextField        _preProgramArgs;
        private IntegerField     _preTimeout;
        private Toggle           _preWait;
        private VisualElement    _preScriptRow;
        private VisualElement    _preProgramRow;
        private Label            _preStatusLabel;
        private Toggle           _postEnabled;
        private RadioButtonGroup _postType;
        private TextField        _postScript;
        private TextField        _postProgram;
        private TextField        _postProgramArgs;
        private IntegerField     _postTimeout;
        private Toggle           _postOnlySuccess;
        private VisualElement    _postScriptRow;
        private VisualElement    _postProgramRow;
        private Label            _postStatusLabel;
        public PageLogController LogController { get; set; }

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root; _opts = opts;
            _preEnabled     = root.Q<Toggle>("toggle-pre-enabled");
            _preType        = root.Q<RadioButtonGroup>("radio-pre-type");
            _preScript      = root.Q<TextField>("field-pre-script");
            _preProgram     = root.Q<TextField>("field-pre-program");
            _preProgramArgs = root.Q<TextField>("field-pre-program-args");
            _preTimeout     = root.Q<IntegerField>("field-pre-timeout");
            _preWait        = root.Q<Toggle>("toggle-pre-wait");
            _preScriptRow   = root.Q<VisualElement>("pre-script-row");
            _preProgramRow  = root.Q<VisualElement>("pre-program-row");
            _preStatusLabel = root.Q<Label>("label-pre-status");
            _postEnabled     = root.Q<Toggle>("toggle-post-enabled");
            _postType        = root.Q<RadioButtonGroup>("radio-post-type");
            _postScript      = root.Q<TextField>("field-post-script");
            _postProgram     = root.Q<TextField>("field-post-program");
            _postProgramArgs = root.Q<TextField>("field-post-program-args");
            _postTimeout     = root.Q<IntegerField>("field-post-timeout");
            _postOnlySuccess = root.Q<Toggle>("toggle-post-only-success");
            _postScriptRow   = root.Q<VisualElement>("post-script-row");
            _postProgramRow  = root.Q<VisualElement>("post-program-row");
            _postStatusLabel = root.Q<Label>("label-post-status");
            RestoreFromOptions();
            _preEnabled?.RegisterValueChangedCallback(e => { _opts.PreHookEnabled = e.newValue; UpdatePreEnabledState(); });
            _preType?.RegisterValueChangedCallback(e => { _opts.PreHookIsProgram = e.newValue == 1; UpdatePreTypeRows(); });
            _preScript?.RegisterValueChangedCallback(e      => _opts.PreHookScript      = e.newValue);
            _preProgram?.RegisterValueChangedCallback(e     => _opts.PreHookProgram     = e.newValue);
            _preProgramArgs?.RegisterValueChangedCallback(e => _opts.PreHookProgramArgs = e.newValue);
            _preTimeout?.RegisterValueChangedCallback(e     => _opts.PreHookTimeoutSec  = e.newValue < 0 ? 0 : e.newValue);
            _preWait?.RegisterValueChangedCallback(e        => _opts.PreHookWait        = e.newValue);
            _postEnabled?.RegisterValueChangedCallback(e => { _opts.PostHookEnabled = e.newValue; UpdatePostEnabledState(); });
            _postType?.RegisterValueChangedCallback(e => { _opts.PostHookIsProgram = e.newValue == 1; UpdatePostTypeRows(); });
            _postScript?.RegisterValueChangedCallback(e      => _opts.PostHookScript      = e.newValue);
            _postProgram?.RegisterValueChangedCallback(e     => _opts.PostHookProgram     = e.newValue);
            _postProgramArgs?.RegisterValueChangedCallback(e => _opts.PostHookProgramArgs = e.newValue);
            _postTimeout?.RegisterValueChangedCallback(e     => _opts.PostHookTimeoutSec  = e.newValue < 0 ? 0 : e.newValue);
            _postOnlySuccess?.RegisterValueChangedCallback(e => _opts.PostHookOnlySuccess = e.newValue);
            root.Q<Button>("btn-pre-browse-script")  ?.RegisterCallback<ClickEvent>(_ => BrowseScript(_preScript));
            root.Q<Button>("btn-pre-browse-program") ?.RegisterCallback<ClickEvent>(_ => BrowseProgram(_preProgram));
            root.Q<Button>("btn-post-browse-script") ?.RegisterCallback<ClickEvent>(_ => BrowseScript(_postScript));
            root.Q<Button>("btn-post-browse-program")?.RegisterCallback<ClickEvent>(_ => BrowseProgram(_postProgram));
            root.Q<Button>("btn-hook-test-pre") ?.RegisterCallback<ClickEvent>(_ => TestHook(false));
            root.Q<Button>("btn-hook-test-post")?.RegisterCallback<ClickEvent>(_ => TestHook(true));
            UpdatePreEnabledState(); UpdatePostEnabledState();
        }
        private void RestoreFromOptions()
        {
            _preEnabled    ?.SetValueWithoutNotify(_opts.PreHookEnabled);
            _preType       ?.SetValueWithoutNotify(_opts.PreHookIsProgram ? 1 : 0);
            _preScript     ?.SetValueWithoutNotify(_opts.PreHookScript      ?? "");
            _preProgram    ?.SetValueWithoutNotify(_opts.PreHookProgram     ?? "");
            _preProgramArgs?.SetValueWithoutNotify(_opts.PreHookProgramArgs ?? "");
            _preTimeout    ?.SetValueWithoutNotify(_opts.PreHookTimeoutSec);
            _preWait       ?.SetValueWithoutNotify(_opts.PreHookWait);
            _postEnabled    ?.SetValueWithoutNotify(_opts.PostHookEnabled);
            _postType       ?.SetValueWithoutNotify(_opts.PostHookIsProgram ? 1 : 0);
            _postScript     ?.SetValueWithoutNotify(_opts.PostHookScript      ?? "");
            _postProgram    ?.SetValueWithoutNotify(_opts.PostHookProgram     ?? "");
            _postProgramArgs?.SetValueWithoutNotify(_opts.PostHookProgramArgs ?? "");
            _postTimeout    ?.SetValueWithoutNotify(_opts.PostHookTimeoutSec);
            _postOnlySuccess?.SetValueWithoutNotify(_opts.PostHookOnlySuccess);
        }
        private void UpdatePreEnabledState()
        {
            bool en = _opts.PreHookEnabled;
            if (_preType        != null) _preType.SetEnabled(en);
            if (_preTimeout     != null) _preTimeout.SetEnabled(en);
            if (_preWait        != null) _preWait.SetEnabled(en);
            // script/program rows visibility handled separately; always keep fields enabled when row is visible
            UpdatePreTypeRows();
        }
        private void UpdatePostEnabledState()
        {
            bool en = _opts.PostHookEnabled;
            if (_postType        != null) _postType.SetEnabled(en);
            if (_postTimeout     != null) _postTimeout.SetEnabled(en);
            if (_postOnlySuccess != null) _postOnlySuccess.SetEnabled(en);
            UpdatePostTypeRows();
        }
        private void UpdatePreTypeRows()
        {
            SetRowVisible(_preScriptRow,  !_opts.PreHookIsProgram);
            SetRowVisible(_preProgramRow,  _opts.PreHookIsProgram);
        }
        private void UpdatePostTypeRows()
        {
            SetRowVisible(_postScriptRow,  !_opts.PostHookIsProgram);
            SetRowVisible(_postProgramRow,  _opts.PostHookIsProgram);
        }
        private static void SetRowVisible(VisualElement r, bool v)
        {
            if (r == null) return;
            if (v) r.RemoveFromClassList("hook-row--hidden");
            else   r.AddToClassList("hook-row--hidden");
        }
        private void BrowseScript(TextField t)
        {
            if (t == null) return;
            string filter = NativePlatform.FileDialog.CreateFilter(
                "Script files (*.ps1;*.bat;*.sh)", "*.ps1;*.bat;*.sh",
                "All files (*.*)", "*.*");
            string initDir = GetInitDir(t.value);
            string path = NativePlatform.FileDialog.OpenFilePanel("Select script file", filter, null);
            if (!string.IsNullOrEmpty(path))
            {
                t.value = path;   // triggers RegisterValueChangedCallback -> opts sync
            }
        }
        private void BrowseProgram(TextField t)
        {
            if (t == null) return;
            string filter = NativePlatform.FileDialog.CreateFilter(
                "Executable files (*.exe)", "*.exe", "*.*");
            string initDir = GetInitDir(t.value);
            string path = NativePlatform.FileDialog.OpenFilePanel("Select program", filter, "exe", initDir);
            if (!string.IsNullOrEmpty(path))
            {
                t.value = path;   // triggers RegisterValueChangedCallback -> opts sync
            }
        }
        private static string GetInitDir(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath)) return "";
            try
            {
                string dir = System.IO.Path.GetDirectoryName(currentPath);
                return System.IO.Directory.Exists(dir) ? dir : "";
            }
            catch { return ""; }
        }
        private void TestHook(bool isPost)
        {
            var    lbl = isPost ? _postStatusLabel : _preStatusLabel;
            string tag = isPost ? "post" : "pre";
            SetStatus(lbl, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            LogController?.AppendLog("[HOOK] Testing " + tag + " hook");
            bool   isProg  = isPost ? _opts.PostHookIsProgram  : _opts.PreHookIsProgram;
            string script  = isPost ? _opts.PostHookScript      : _opts.PreHookScript;
            string prog    = isPost ? _opts.PostHookProgram     : _opts.PreHookProgram;
            string args    = isPost ? _opts.PostHookProgramArgs : _opts.PreHookProgramArgs;
            int    timeout = isPost ? _opts.PostHookTimeoutSec  : _opts.PreHookTimeoutSec;
            int    code = RunHookSync(isProg, script, prog, args, timeout);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(lbl, msg, ok ? "hook-status--ok" : "hook-status--fail", "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] " + tag + " result: " + msg);
            if (ok) ToastManager.Success(tag + " hook OK"); else ToastManager.Error(tag + " hook: " + msg);
        }
        public bool RunPreHook()
        {
            if (!_opts.PreHookEnabled) return true;
            LogController?.AppendLog("[HOOK] Running pre-hook...");
            SetStatus(_preStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            int    code = RunHookSync(_opts.PreHookIsProgram, _opts.PreHookScript, _opts.PreHookProgram, _opts.PreHookProgramArgs, _opts.PreHookTimeoutSec);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(_preStatusLabel, msg, ok ? "hook-status--ok" : "hook-status--fail", "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Pre-hook done: " + msg);
            if (!ok && _opts.PreHookWait) { ToastManager.Warning("Pre-hook failed, cancelled"); return false; }
            return true;
        }
        public void RunPostHook(int testExitCode)
        {
            if (!_opts.PostHookEnabled) return;
            if (_opts.PostHookOnlySuccess && testExitCode != 0) return;
            LogController?.AppendLog("[HOOK] Running post-hook...");
            SetStatus(_postStatusLabel, "Running...", "hook-status--running", "hook-status--ok", "hook-status--fail");
            int    code = RunHookSync(_opts.PostHookIsProgram, _opts.PostHookScript, _opts.PostHookProgram, _opts.PostHookProgramArgs, _opts.PostHookTimeoutSec);
            string msg  = FormatExitMsg(code);
            bool   ok   = code == 0;
            SetStatus(_postStatusLabel, msg, ok ? "hook-status--ok" : "hook-status--fail", "hook-status--running", ok ? "hook-status--fail" : "hook-status--ok");
            LogController?.AppendLog("[HOOK] Post-hook done: " + msg);
        }
        private static int RunHookSync(bool isProgram, string script, string program, string args, int timeoutSec)
        {
            string target = isProgram ? program : script;
            if (string.IsNullOrWhiteSpace(target)) return -1;
            if (!File.Exists(target)) return 99;
            ProcessStartInfo psi;
            if (isProgram)
                psi = new ProcessStartInfo { FileName = target, Arguments = args ?? "", UseShellExecute = false, CreateNoWindow = true };
            else
            {
                string ext = Path.GetExtension(target).ToLowerInvariant();
                string interp, iArgs;
                switch (ext)
                {
                    case ".ps1": interp = "powershell.exe"; iArgs = "-NonInteractive -ExecutionPolicy Bypass -File \"" + target + "\""; break;
                    case ".sh":  interp = "bash";            iArgs = "\"" + target + "\""; break;
                    default:     interp = "cmd.exe";         iArgs = "/c \"" + target + "\""; break;
                }
                psi = new ProcessStartInfo { FileName = interp, Arguments = iArgs, UseShellExecute = false, CreateNoWindow = true };
            }
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
            c == 0 ? "OK (exit 0)" : c == -1 ? "Not configured" : c == -2 ? "Timed out" :
            c == 97 ? "Launch exception" : c == 98 ? "Process start failed" : c == 99 ? "File not found" : "Failed (exit " + c + ")";
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
