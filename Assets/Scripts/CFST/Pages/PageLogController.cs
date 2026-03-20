using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    /// <summary>
    /// 独立日志页面控制器。
    /// 日志写入由 MainWindowController 通过 AppendLog() 分发，
    /// PageOtherController.AppendLog() 同样转发到此处。
    /// </summary>
    public class PageLogController : MonoBehaviour
    {
        private VisualElement _root;

        private ScrollView    _logScroll;
        private VisualElement _logContainer;
        private Label         _logStat;
        private Toggle        _autoScrollToggle;

        private readonly StringBuilder _logBuffer = new StringBuilder();
        private readonly Queue<Label>  _logLines  = new Queue<Label>();
        private bool   _autoScroll  = true;
        private string _filterLevel = null;   // null = 全部
        private const int MAX_LOG_LINES = 500;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root         = root;
            _logScroll    = root.Q<ScrollView>("log-scroll");
            _logContainer = _logScroll?.contentContainer;
            _logStat      = root.Q<Label>("log-stat");
            _autoScrollToggle = root.Q<Toggle>("toggle-log-autoscroll");

            if (_autoScrollToggle != null)
            {
                _autoScrollToggle.SetValueWithoutNotify(true);
                _autoScrollToggle.RegisterValueChangedCallback(e => _autoScroll = e.newValue);
            }

            root.Q<Button>("btn-log-copy") ?.RegisterCallback<ClickEvent>(_ => CopyLog());
            root.Q<Button>("btn-log-clear")?.RegisterCallback<ClickEvent>(_ => ClearLog());

            BindFilterBtn(root, "btn-filter-all",   null);
            BindFilterBtn(root, "btn-filter-info",  "[INFO]");
            BindFilterBtn(root, "btn-filter-warn",  "[WARN]");
            BindFilterBtn(root, "btn-filter-error", "[ERROR]");
            BindFilterBtn(root, "btn-filter-cmd",   "[CMD]");

            _logScroll?.RegisterCallback<WheelEvent>(_ => OnManualScroll());

            AppendLog("[INFO] 日志模块已就绪");
        }

        // ── 过滤按钮绑定 ─────────────────────────────────────
        private static readonly string[] FilterBtnNames =
        {
            "btn-filter-all", "btn-filter-info",
            "btn-filter-warn", "btn-filter-error", "btn-filter-cmd"
        };

        private void BindFilterBtn(VisualElement root, string btnName, string level)
        {
            var btn = root.Q<Button>(btnName);
            if (btn == null) return;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                _filterLevel = level;
                foreach (var n in FilterBtnNames)
                    _root?.Q<Button>(n)?.RemoveFromClassList("log-filter-btn--active");
                btn.AddToClassList("log-filter-btn--active");
                ApplyFilter();
            });
        }

        private void ApplyFilter()
        {
            if (_logContainer == null) return;
            foreach (var child in _logContainer.Children())
            {
                if (child is Label lbl)
                {
                    bool show = _filterLevel == null || lbl.text.Contains(_filterLevel);
                    lbl.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private void OnManualScroll()
        {
            if (_logScroll == null) return;
            float max = _logScroll.verticalScroller.highValue;
            bool atBottom = max <= 0f || _logScroll.verticalScroller.value >= max - 4f;
            if (!atBottom)
            {
                _autoScroll = false;
                if (_autoScrollToggle != null)
                    _autoScrollToggle.SetValueWithoutNotify(false);
            }
        }

        // ── 公共写入接口 ──────────────────────────────────────
        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            string time      = System.DateTime.Now.ToString("HH:mm:ss");
            string formatted = "[" + time + "] " + line;
            _logBuffer.AppendLine(formatted);

            if (_logContainer == null) return;

            // 超出上限时移除最老一行
            while (_logLines.Count >= MAX_LOG_LINES)
            {
                var oldest = _logLines.Dequeue();
                oldest.RemoveFromHierarchy();
            }

            var label = new Label(formatted);
            label.AddToClassList("log-line");

            if      (line.StartsWith("[DEBUG]") || line.StartsWith("[INFO]"))
                label.AddToClassList("log-line--info");
            else if (line.StartsWith("[WARN]"))
                label.AddToClassList("log-line--warn");
            else if (line.StartsWith("[ERROR]"))
                label.AddToClassList("log-line--error");
            else if (line.StartsWith("[CMD]"))
                label.AddToClassList("log-line--cmd");
            else if (line.StartsWith("[HOOK]"))
                label.AddToClassList("log-line--hook");

            // 过滤显示
            if (_filterLevel != null && !formatted.Contains(_filterLevel))
                label.style.display = DisplayStyle.None;

            _logContainer.Add(label);
            _logLines.Enqueue(label);

            // 更新统计
            if (_logStat != null)
                _logStat.text = "共 " + _logLines.Count + " 条";

            if (_autoScroll)
                _logScroll?.schedule.Execute(() =>
                    _logScroll.ScrollTo(label)).StartingIn(10);
        }

        private void ClearLog()
        {
            _logBuffer.Clear();
            _logLines.Clear();
            _logContainer?.Clear();
            if (_logStat != null) _logStat.text = "共 0 条";
            ToastManager.Info("日志已清空");
        }

        private void CopyLog()
        {
            NativePlatform.SetClipboard(_logBuffer.ToString());
            ToastManager.Success("日志已复制到剪贴板");
            var btn = _root?.Q<Button>("btn-log-copy");
            if (btn == null) return;
            string orig = btn.text;
            btn.text = "已复制 ✓";
            _root?.schedule.Execute(() => btn.text = orig).StartingIn(1500);
        }
    }
}
