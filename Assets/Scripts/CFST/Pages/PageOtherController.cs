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
        private VisualElement _logContainer;  // 每行一个 Label 的容器
        private ScrollView    _logScroll;
        private Label         _versionLabel;

        // 用于复制全部日志
        private readonly StringBuilder _logBuffer = new StringBuilder();
        private bool _autoScroll = true;

        // 单个 Label 最多约 300 字符安全，每行独立 Label，最多保留 300 行
        private const int MAX_LOG_LINES = 300;
        private readonly Queue<Label> _logLines = new Queue<Label>();

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _debugToggle  = root.Q<Toggle>("toggle-debug");
            _logScroll    = root.Q<ScrollView>("log-scroll");
            _versionLabel = root.Q<Label>("label-version");

            // 用 log-scroll 的 contentContainer 作为行容器
            // UXML 里的 log-label 不再使用，改为动态添加子 Label
            _logContainer = _logScroll?.contentContainer;

            // 移除旧的静态 log-label（如果存在）
            var oldLabel = root.Q<Label>("log-label");
            oldLabel?.RemoveFromHierarchy();

            _debugToggle?.RegisterValueChangedCallback(e => _opts.Debug = e.newValue);

            root.Q<Button>("btn-clear-log")?.RegisterCallback<ClickEvent>(_ => ClearLog());
            root.Q<Button>("btn-copy-log")?.RegisterCallback<ClickEvent>(_ => CopyLog());
            root.Q<Button>("btn-homepage")?.RegisterCallback<ClickEvent>(_ =>
                NativePlatform.Shell.OpenUrl("https://github.com/XIU2/CloudflareSpeedTest"));

            // 版本号
            if (_versionLabel != null)
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                _versionLabel.text = ver != null ? ver.ToString() : "1.0.0";
            }

            // 自动滚动检测：用户向上滚动时暂停，滚回底部时恢复
            _logScroll?.RegisterCallback<WheelEvent>(_ => CheckAutoScroll());
            _logScroll?.verticalScroller.RegisterCallback<ChangeEvent<float>>(_ => CheckAutoScroll());
        }

        private void CheckAutoScroll()
        {
            if (_logScroll == null) return;
            float max = _logScroll.verticalScroller.highValue;
            _autoScroll = max <= 0f || _logScroll.verticalScroller.value >= max - 4f;
        }

        /// <summary>由 MainWindowController 在主线程调用，追加一行日志</summary>
        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            var time = System.DateTime.Now.ToString("HH:mm:ss");
            string formatted = $"[{time}] {line}";

            // 追加到复制缓冲区
            _logBuffer.AppendLine(formatted);

            if (_logContainer == null) return;

            // 超出行数限制：移除最旧的行
            while (_logLines.Count >= MAX_LOG_LINES)
            {
                var oldest = _logLines.Dequeue();
                oldest.RemoveFromHierarchy();
            }

            // 创建新行 Label（每行独立，不会超顶点限制）
            var label = new Label(formatted);
            label.AddToClassList("log-line");

            // DEBUG 行用灰色区分
            if (line.StartsWith("[DEBUG]") || line.StartsWith("[INFO]"))
                label.AddToClassList("log-line--info");
            else if (line.StartsWith("[ERROR]") || line.StartsWith("[CFST] 找不到"))
                label.AddToClassList("log-line--error");
            else if (line.StartsWith("[CMD]"))
                label.AddToClassList("log-line--cmd");

            _logContainer.Add(label);
            _logLines.Enqueue(label);

            // 自动滚动到底部
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
            var orig = btn.text;
            btn.text = "已复制 ✓";
            _root?.schedule.Execute(() => btn.text = orig).StartingIn(1500);
        }
    }
}
