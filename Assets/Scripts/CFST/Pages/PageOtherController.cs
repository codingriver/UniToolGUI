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
    }
}
