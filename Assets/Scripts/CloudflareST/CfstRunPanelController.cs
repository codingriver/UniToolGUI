// UTF-8
using System;
using System.Threading;
using CloudflareST.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 测速运行面板控制器，对应 CfstRunPanel.uxml
    /// </summary>
    public class CfstRunPanelController
    {
        public event Action<CfstTestRecord> OnTestComplete;

        private readonly VisualElement _root;
        private readonly ICoreService  _core = new CoreService();
        private CancellationTokenSource _cts;
        private DateTime _startTime;

        private Label        _statusBadge;
        private Label        _infoProtocol;
        private Label        _infoConcurrency;
        private Label        _infoIpSource;
        private Label        _infoUrl;
        private Label        _pingCount;
        private VisualElement _pingFill;
        private Label        _dlCount;
        private VisualElement _dlFill;
        private Label        _elapsed;
        private Label        _currentIp;
        private ScrollView   _logScroll;
        private Label        _logText;
        private Button       _btnStart;
        private Button       _btnCancel;

        public CfstConfigPanelController ConfigCtrl { get; set; }

        public CfstRunPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
        }

        public void Refresh() => UpdateInfoSummary();

        private void BindElements()
        {
            _statusBadge     = _root.Q<Label>("cfst-run-status-badge");
            _infoProtocol    = _root.Q<Label>("run-info-protocol");
            _infoConcurrency = _root.Q<Label>("run-info-concurrency");
            _infoIpSource    = _root.Q<Label>("run-info-ip-source");
            _infoUrl         = _root.Q<Label>("run-info-url");
            _pingCount       = _root.Q<Label>("run-ping-count");
            _pingFill        = _root.Q<VisualElement>("run-ping-fill");
            _dlCount         = _root.Q<Label>("run-dl-count");
            _dlFill          = _root.Q<VisualElement>("run-dl-fill");
            _elapsed         = _root.Q<Label>("run-elapsed");
            _currentIp       = _root.Q<Label>("run-current-ip");
            _logScroll       = _root.Q<ScrollView>("run-log-scroll");
            _logText         = _root.Q<Label>("run-log-text");
            _btnStart        = _root.Q<Button>("run-btn-start");
            _btnCancel       = _root.Q<Button>("run-btn-cancel");

            _btnStart?.RegisterCallback<ClickEvent>(_ => StartTest());
            _btnCancel?.RegisterCallback<ClickEvent>(_ => CancelTest());
            SetButtonState(idle: true);
        }

        private void UpdateInfoSummary()
        {
            if (ConfigCtrl == null) return;
            var cfg = ConfigCtrl.BuildConfig();
            if (_infoProtocol    != null) _infoProtocol.text    = cfg.UseHttping ? "HTTPing" : cfg.UseTcping ? "TCPing" : "ICMP Ping";
            if (_infoConcurrency != null) _infoConcurrency.text = cfg.Concurrency.ToString();
            if (_infoIpSource    != null) _infoIpSource.text    = string.Join(", ", cfg.IpSourceFiles);
            if (_infoUrl         != null) _infoUrl.text         = cfg.Url?.Length > 50 ? cfg.Url.Substring(0, 50) + "..." : cfg.Url;
        }

        private async void StartTest()
        {
            var cfg = ConfigCtrl?.BuildConfig() ?? new TestConfig();
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _startTime = DateTime.Now;

            SetStatus("running", "测速中...");
            SetButtonState(idle: false);
            AppendLog($"[{_startTime:HH:mm:ss}] 开始测速...");
            AppendLog($"协议: {(cfg.UseHttping ? "HTTPing" : cfg.UseTcping ? "TCPing" : "ICMP Ping")}");

            try
            {
                var result   = await _core.RunTestAsync(cfg, _cts.Token);
                var duration = DateTime.Now - _startTime;
                if (result.Success)
                {
                    SetStatus("done", "完成");
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] 完成 — {result.Summary}");
                    SetProgress(_pingFill, 1f, "done");
                    SetProgress(_dlFill,   1f, "done");
                    OnTestComplete?.Invoke(new CfstTestRecord
                    {
                        Timestamp = _startTime,
                        Duration  = duration,
                        Protocol  = cfg.UseHttping ? "HTTPing" : cfg.UseTcping ? "TCPing" : "ICMP",
                        Summary   = result.Summary,
                        Success   = true,
                        Config    = cfg
                    });
                }
                else
                {
                    SetStatus("error", "失败");
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] 失败 — {result.Summary}");
                    SetProgress(_pingFill, 0f, "error");
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("idle", "已取消");
                AppendLog($"[{DateTime.Now:HH:mm:ss}] 已取消");
                SetProgress(_pingFill, 0f, "idle");
            }
            catch (Exception ex)
            {
                SetStatus("error", "错误");
                AppendLog($"[{DateTime.Now:HH:mm:ss}] 错误: {ex.Message}");
                Debug.LogException(ex);
            }
            finally { SetButtonState(idle: true); }
        }

        private void CancelTest()
        {
            _cts?.Cancel();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] 正在取消...");
        }

        private void AppendLog(string line)
        {
            if (_logText == null) return;
            _logText.text = string.IsNullOrEmpty(_logText.text) || _logText.text == "等待开始..."
                ? line : _logText.text + "\n" + line;
            _logScroll?.ScrollTo(_logText);
        }

        private void SetStatus(string kind, string text)
        {
            if (_statusBadge == null) return;
            _statusBadge.text = text;
            foreach (var c in new[] { "idle", "running", "done", "error" })
                _statusBadge.RemoveFromClassList($"cfst-badge--{c}");
            _statusBadge.AddToClassList($"cfst-badge--{kind}");
        }

        private static void SetProgress(VisualElement fill, float t, string state)
        {
            if (fill == null) return;
            fill.style.width = new StyleLength(new Length(t * 100f, LengthUnit.Percent));
            foreach (var c in new[] { "running", "done", "error" })
                fill.RemoveFromClassList($"cfst-progress-fill--{c}");
            fill.AddToClassList($"cfst-progress-fill--{state}");
        }

        private void SetButtonState(bool idle)
        {
            if (_btnStart  != null) _btnStart.SetEnabled(idle);
            if (_btnCancel != null) _btnCancel.SetEnabled(!idle);
        }
    }
}
