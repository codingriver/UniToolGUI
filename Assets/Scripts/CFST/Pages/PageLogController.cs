// ============================================================
// PageLogController.cs  —  运行日志页面控制器
// 优化：批量 UI 更新（帧限流）+ 文件写入开关 + 队列防卡死
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    public class PageLogController : MonoBehaviour
    {
        private VisualElement _root;
        private ScrollView    _logScroll;
        private VisualElement _logContainer;
        private Label         _logStat;
        private Toggle        _autoScrollToggle;

        // ── 日志缓冲区（线程安全队列，供多线程写入）────────────
        private readonly Queue<string> _pendingLines = new Queue<string>();
        private readonly object       _pendingLock  = new object();

        // UI 行队列（主线程）
        private readonly Queue<Label>  _uiLines = new Queue<Label>();

        // 全量文本缓冲（用于复制）
        private readonly StringBuilder _logBuffer = new StringBuilder();

        // ── 配置 ─────────────────────────────────────────────
        private const int MAX_UI_LINES    = 300;   // UI 中最多保留行数
        private const int MAX_FLUSH_PER_FRAME = 30; // 每帧最多处理行数，防止卡帧
        private const int FILE_FLUSH_INTERVAL = 50; // 每积累 N 行才 flush 一次文件

        private bool   _autoScroll  = true;
        private string _filterLevel = null;
        private bool   _logToFile   = false;
        private string _logFilePath;
        private int    _pendingFileLines = 0;
        private StreamWriter _fileWriter;

        private Coroutine _flushCoroutine;

        private static readonly string[] FilterBtnNames =
        {
            "btn-filter-all","btn-filter-info",
            "btn-filter-warn","btn-filter-error","btn-filter-cmd"
        };

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root         = root;
            _logScroll    = root.Q<ScrollView>("log-scroll");
            _logContainer = _logScroll?.contentContainer;
            _logStat      = root.Q<Label>("log-stat");
            _autoScrollToggle = root.Q<Toggle>("toggle-log-autoscroll");

            _logFilePath = Path.Combine(
                Environment.CurrentDirectory, "cfst_log.txt");

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

            // 初始化文件写入状态
            SetLogToFile(opts?.LogToFile ?? false);

            // 启动帧刷新协程
            _flushCoroutine = StartCoroutine(FlushCoroutine());

            AppendLog("[INFO] 日志模块已就绪");
        }

        private void OnDestroy()
        {
            if (_flushCoroutine != null) StopCoroutine(_flushCoroutine);
            CloseFileWriter();
        }

        // ── 文件写入开关（由 PageOtherController 调用）────────
        public void SetLogToFile(bool enabled)
        {
            _logToFile = enabled;
            if (enabled)
            {
                try
                {
                    _fileWriter = new StreamWriter(_logFilePath, append: true,
                        encoding: new UTF8Encoding(false));
                    _fileWriter.AutoFlush = false;
                }
                catch (Exception ex)
                {
                    _logToFile = false;
                    Debug.LogWarning("[LOG] 无法打开日志文件: " + ex.Message);
                    ToastManager.Error("日志文件打开失败: " + ex.Message);
                }
            }
            else
            {
                CloseFileWriter();
            }
        }

        private void CloseFileWriter()
        {
            try { _fileWriter?.Flush(); _fileWriter?.Close(); }
            catch { }
            finally { _fileWriter = null; }
        }

        // ── 公共写入接口（线程安全）────────────────────────────
        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            string formatted = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line;
            lock (_pendingLock)
                _pendingLines.Enqueue(formatted);
        }

        // ── 帧刷新协程（主线程，每帧最多处理 N 行）───────────
        private IEnumerator FlushCoroutine()
        {
            while (true)
            {
                yield return null; // 等下一帧

                int count = 0;
                while (count < MAX_FLUSH_PER_FRAME)
                {
                    string line;
                    lock (_pendingLock)
                    {
                        if (_pendingLines.Count == 0) break;
                        line = _pendingLines.Dequeue();
                    }
                    ProcessLine(line);
                    count++;
                }

                // 如果还有积压，下帧继续（不 yield，但协程已 yield 过一次）
                // 更新统计
                if (count > 0 && _logStat != null)
                    _logStat.text = "共 " + _uiLines.Count + " 条";

                // 文件定期 flush
                if (_logToFile && _fileWriter != null && _pendingFileLines >= FILE_FLUSH_INTERVAL)
                {
                    try { _fileWriter.Flush(); } catch { }
                    _pendingFileLines = 0;
                }
            }
        }

        // ── 单行处理（主线程）─────────────────────────────────
        private void ProcessLine(string formatted)
        {
            // 写文件
            if (_logToFile && _fileWriter != null)
            {
                try
                {
                    _fileWriter.WriteLine(formatted);
                    _pendingFileLines++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[LOG] 文件写入失败: " + ex.Message);
                    CloseFileWriter();
                    _logToFile = false;
                }
            }

            // 写文本缓冲（复制用）
            _logBuffer.AppendLine(formatted);

            if (_logContainer == null) return;

            // 超出上限移除最老行
            while (_uiLines.Count >= MAX_UI_LINES)
            {
                var oldest = _uiLines.Dequeue();
                oldest.RemoveFromHierarchy();
            }

            // 创建 UI 行
            var label = new Label(formatted);
            label.AddToClassList("log-line");
            string raw = formatted.Length > 10 ? formatted.Substring(10) : formatted;
            if      (raw.StartsWith("[DEBUG]") || raw.StartsWith("[INFO]"))  label.AddToClassList("log-line--info");
            else if (raw.StartsWith("[WARN]"))                               label.AddToClassList("log-line--warn");
            else if (raw.StartsWith("[ERROR]"))                              label.AddToClassList("log-line--error");
            else if (raw.StartsWith("[CMD]"))                                label.AddToClassList("log-line--cmd");
            else if (raw.StartsWith("[HOOK]"))                               label.AddToClassList("log-line--hook");

            if (_filterLevel != null && !formatted.Contains(_filterLevel))
                label.style.display = DisplayStyle.None;

            _logContainer.Add(label);
            _uiLines.Enqueue(label);

            if (_autoScroll)
                _logScroll?.schedule.Execute(() =>
                    _logScroll.ScrollTo(label)).StartingIn(16);
        }

        // ── 过滤 ─────────────────────────────────────────────
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
                if (child is Label lbl)
                    lbl.style.display = (_filterLevel == null || lbl.text.Contains(_filterLevel))
                        ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnManualScroll()
        {
            if (_logScroll == null) return;
            float max = _logScroll.verticalScroller.highValue;
            bool atBottom = max <= 0f || _logScroll.verticalScroller.value >= max - 4f;
            if (!atBottom)
            {
                _autoScroll = false;
                if (_autoScrollToggle != null) _autoScrollToggle.SetValueWithoutNotify(false);
            }
        }

        // ── 清空 / 复制 ───────────────────────────────────────
        private void ClearLog()
        {
            lock (_pendingLock) _pendingLines.Clear();
            _logBuffer.Clear();
            _uiLines.Clear();
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
