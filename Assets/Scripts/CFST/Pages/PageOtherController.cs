using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageOtherController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private Toggle        _debugToggle;
        private VisualElement _logContainer;
        private ScrollView    _logScroll;

        private readonly StringBuilder _logBuffer  = new StringBuilder();
        private readonly Queue<Label>   _logLines   = new Queue<Label>();
        private bool _autoScroll = true;
        private const int MAX_LOG_LINES = 300;

        // 开机自启 Toggle 引用，供事件回调刷新
        private Toggle _startupToggle;
        private Label  _startupHint;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _debugToggle  = root.Q<Toggle>("toggle-debug");
            _logScroll    = root.Q<ScrollView>("log-scroll");

            _logContainer = _logScroll?.contentContainer;

            // 移除旧静态 log-label（如果存在）
            root.Q<Label>("log-label")?.RemoveFromHierarchy();

            _debugToggle?.RegisterValueChangedCallback(e => _opts.Debug = e.newValue);

            root.Q<Button>("btn-clear-log")?.RegisterCallback<ClickEvent>(_ => ClearLog());
            root.Q<Button>("btn-copy-log") ?.RegisterCallback<ClickEvent>(_ => CopyLog());

            _logScroll?.RegisterCallback<WheelEvent>(_ => CheckAutoScroll());
            _logScroll?.verticalScroller.RegisterCallback<ChangeEvent<float>>(_ => CheckAutoScroll());

            // ── 开机自启 ─────────────────────────────────────────
            _startupToggle = root.Q<Toggle>("toggle-startup");
            _startupHint   = root.Q<Label>("hint-startup");
            var startupToggle = _startupToggle;
            var hintStartup   = _startupHint;
            if (startupToggle != null)
            {
                bool startupEnabled = false;
                try { startupEnabled = WindowsStartup.IsStartupEnabled(); }
                catch (System.Exception ex) { Debug.LogWarning("[Startup] IsStartupEnabled 失败: " + ex.Message); }
                startupToggle.value = startupEnabled;
                UpdateStartupHint(hintStartup, startupToggle.value);

                startupToggle.RegisterValueChangedCallback(e =>
                {
                    bool ok = false;
                    try
                    {
                        string exePath = WindowsStartup.GetCurrentExePath();
                        ok = e.newValue
                            ? WindowsStartup.EnableStartup(exePath)
                            : WindowsStartup.DisableStartup();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning("[Startup] 切换失败: " + ex.Message);
                        ok = false;
                    }

                    if (!ok)
                    {
                        startupToggle.SetValueWithoutNotify(!e.newValue);
                        AppendLog("[WARN] 开机自启设置失败，请检查权限");
                    }
                    else
                    {
                        AppendLog(e.newValue ? "[INFO] 已启用开机自启" : "[INFO] 已禁用开机自启");
                    }
                    UpdateStartupHint(hintStartup, startupToggle.value);
                });

                // 订阅全局事件：托盘菜单改变开机自启时同步刷新此 Toggle
                WindowsStartup.OnStartupChanged += OnStartupChangedExternal;
            }

            // ── 最小化到托盘开关 ──────────────────────────────────
            var trayMinToggle = root.Q<Toggle>("toggle-tray-minimize");
            if (trayMinToggle != null)
            {
                trayMinToggle.value = CfstTrayManager.MinimizeToTray;
                trayMinToggle.RegisterValueChangedCallback(e =>
                    CfstTrayManager.MinimizeToTray = e.newValue);
            }

            // 启动提示
            AppendLog("[INFO] CFST 已就绪");
        }

        private void CheckAutoScroll()
        {
            if (_logScroll == null) return;
            float max = _logScroll.verticalScroller.highValue;
            _autoScroll = max <= 0f || _logScroll.verticalScroller.value >= max - 4f;
        }

        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            string time      = System.DateTime.Now.ToString("HH:mm:ss");
            string formatted = "[" + time + "] " + line;
            _logBuffer.AppendLine(formatted);

            if (_logContainer == null) return;

            while (_logLines.Count >= MAX_LOG_LINES)
            {
                var oldest = _logLines.Dequeue();
                oldest.RemoveFromHierarchy();
            }

            var label = new Label(formatted);
            label.AddToClassList("log-line");

            if (line.StartsWith("[DEBUG]") || line.StartsWith("[INFO]"))
                label.AddToClassList("log-line--info");
            else if (line.StartsWith("[ERROR]"))
                label.AddToClassList("log-line--error");
            else if (line.StartsWith("[CMD]"))
                label.AddToClassList("log-line--cmd");

            _logContainer.Add(label);
            _logLines.Enqueue(label);

            if (_autoScroll)
                _logScroll?.schedule.Execute(() =>
                    _logScroll.ScrollTo(label)).StartingIn(10);
        }

        private void ClearLog()
        {
            _logBuffer.Clear();
            _logLines.Clear();
            _logContainer?.Clear();
        }

        private void CopyLog()
        {
            NativePlatform.SetClipboard(_logBuffer.ToString());
            var btn = _root?.Q<Button>("btn-copy-log");
            if (btn == null) return;
            string orig = btn.text;
            btn.text = "已复制 ✓";
            _root?.schedule.Execute(() => btn.text = orig).StartingIn(1500);
        }

        private void UpdateStartupHint(Label hint, bool enabled)
        {
            if (hint == null) return;
            hint.text = enabled ? "✓ 已设置开机自启" : "";
        }

        private void OnDestroy()
        {
            WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
        }

        /// <summary>
        /// 托盘菜单或其他地方修改了开机自启状态后，同步刷新界面 Toggle。
        /// WindowsStartup.OnStartupChanged 在主线程触发，可直接操作 UI。
        /// </summary>
        private void OnStartupChangedExternal(bool enabled)
        {
            if (_startupToggle == null) return;
            _startupToggle.SetValueWithoutNotify(enabled);
            UpdateStartupHint(_startupHint, enabled);
            AppendLog(enabled ? "[INFO] 开机自启已启用（外部变更）" : "[INFO] 开机自启已禁用（外部变更）");
        }
    }
}
