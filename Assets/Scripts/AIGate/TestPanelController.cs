using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Gate.Managers;
using Gate.Models;

namespace AIGate.UI
{
    /// <summary>
    /// 代理连通性测试面板控制器
    /// 对应 TestPanel.uxml
    /// </summary>
    public class TestPanelController
    {
        private readonly VisualElement _root;

        private TextField     _proxyInput, _urlInput;
        private Button        _btnUseCurrent, _btnRunTest;
        private VisualElement _resultSuccess, _resultFail;
        private Label         _resultTime, _resultUrl, _resultError;
        private ListView      _historyList;

        private readonly List<TestHistoryEntry> _history = new();

        private struct TestHistoryEntry
        {
            public string Proxy;
            public bool   Success;
            public int    Ms;
            public string Time;
        }

        public TestPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
            RegisterCallbacks();
            BuildHistoryList();
        }

        private void BindElements()
        {
            _proxyInput    = _root.Q<TextField>("test-proxy-input");
            _urlInput      = _root.Q<TextField>("test-url-input");
            _btnUseCurrent = _root.Q<Button>("btn-use-current");
            _btnRunTest    = _root.Q<Button>("btn-run-test");
            _resultSuccess = _root.Q<VisualElement>("test-result-success");
            _resultFail    = _root.Q<VisualElement>("test-result-fail");
            _resultTime    = _root.Q<Label>("result-time");
            _resultUrl     = _root.Q<Label>("result-url");
            _resultError   = _root.Q<Label>("result-error");
            _historyList   = _root.Q<ListView>("test-history-list");
        }

        private void RegisterCallbacks()
        {
            _btnUseCurrent?.RegisterCallback<ClickEvent>(_ => FillCurrentProxy());
            _btnRunTest?.RegisterCallback<ClickEvent>(_ => RunTest());
        }

        private void BuildHistoryList()
        {
            if (_historyList == null) return;

            _historyList.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("list-row");

                var dot = new VisualElement();
                dot.name = "hDot";

                var proxy = new Label();
                proxy.name = "hProxy";
                proxy.AddToClassList("list-row-name");

                var time = new Label();
                time.name = "hTime";
                time.AddToClassList("list-row-category");

                var ms = new Label();
                ms.name = "hMs";
                ms.AddToClassList("status-value");

                row.Add(dot); row.Add(proxy); row.Add(time); row.Add(ms);
                return row;
            };

            _historyList.bindItem = (el, i) =>
            {
                var entry = _history[i];
                var dot   = el.Q<VisualElement>("hDot");
                dot.RemoveFromClassList("status-dot-on");
                dot.RemoveFromClassList("status-dot-off");
                dot.AddToClassList(entry.Success ? "status-dot-on" : "status-dot-off");
                el.Q<Label>("hProxy").text = entry.Proxy;
                el.Q<Label>("hTime").text  = entry.Time;
                el.Q<Label>("hMs").text    = entry.Success ? $"{entry.Ms}ms" : "failed";
            };

            _historyList.itemsSource = _history;
        }

        public void Refresh()
        {
            // Nothing to auto-refresh; user triggers tests manually.
        }

        private void FillCurrentProxy()
        {
            var cfg = EnvVarManager.GetProxyConfig(EnvLevel.User);
            var proxy = cfg.HttpProxy ?? cfg.HttpsProxy ?? "";
            if (_proxyInput != null)
                _proxyInput.value = proxy;
        }

        private async void RunTest()
        {
            var proxy = _proxyInput?.value?.Trim();
            if (string.IsNullOrEmpty(proxy))
            {
                // Fall back to current env proxy
                var cfg = EnvVarManager.GetProxyConfig(EnvLevel.User);
                proxy = cfg.HttpProxy ?? cfg.HttpsProxy;
            }

            if (string.IsNullOrEmpty(proxy))
            {
                ShowResult(new ProxyTestResult
                {
                    Success      = false,
                    ErrorMessage = "未指定代理地址，且环境变量中无代理配置。"
                }, proxy ?? "");
                return;
            }

            // Disable button while testing
            if (_btnRunTest != null) _btnRunTest.SetEnabled(false);

            var url = _urlInput?.value?.Trim();
            ProxyTestResult result;
            try
            {
                result = await ProxyTester.TestProxyAsync(proxy, string.IsNullOrEmpty(url) ? null : url);
            }
            catch (Exception ex)
            {
                result = new ProxyTestResult { Success = false, ErrorMessage = ex.Message };
            }

            if (_btnRunTest != null) _btnRunTest.SetEnabled(true);

            ShowResult(result, proxy);
            AddHistory(proxy, result);
        }

        private void ShowResult(ProxyTestResult result, string proxy)
        {
            // Hide both first
            if (_resultSuccess != null) _resultSuccess.style.display = DisplayStyle.None;
            if (_resultFail    != null) _resultFail.style.display    = DisplayStyle.None;

            if (result.Success)
            {
                if (_resultSuccess != null) _resultSuccess.style.display = DisplayStyle.Flex;
                if (_resultTime != null)    _resultTime.text = $"响应时间: {result.ResponseTimeMs} ms";
                if (_resultUrl  != null)    _resultUrl.text  = $"目标: {result.TestUrl}";
            }
            else
            {
                if (_resultFail  != null) _resultFail.style.display  = DisplayStyle.Flex;
                if (_resultError != null) _resultError.text = result.ErrorMessage ?? "未知错误";
            }
        }

        private void AddHistory(string proxy, ProxyTestResult result)
        {
            _history.Insert(0, new TestHistoryEntry
            {
                Proxy   = proxy,
                Success = result.Success,
                Ms      = result.ResponseTimeMs,
                Time    = DateTime.Now.ToString("HH:mm:ss")
            });

            // Keep at most 50 entries
            if (_history.Count > 50)
                _history.RemoveAt(_history.Count - 1);

            _historyList?.Rebuild();
        }
    }
}
