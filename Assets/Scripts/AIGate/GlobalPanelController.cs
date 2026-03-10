using System;
using UnityEngine;
using UnityEngine.UIElements;
using Gate.Managers;
using Gate.Models;

namespace AIGate.UI
{
    /// <summary>
    /// 全局代理环境变量面板控制器
    /// 对应 GlobalPanel.uxml
    /// </summary>
    public class GlobalPanelController
    {
        private readonly VisualElement _root;

        private Label     _httpStatus, _httpsStatus, _noProxyStatus;
        private TextField _proxyInput, _httpInput, _httpsInput, _noProxyInput;
        private Toggle    _verifyToggle;
        private Button    _btnClear, _btnRefresh, _btnApply;
        private Label     _feedback;

        public GlobalPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
            RegisterCallbacks();
        }

        private void BindElements()
        {
            _httpStatus    = _root.Q<Label>("global-http-status");
            _httpsStatus   = _root.Q<Label>("global-https-status");
            _noProxyStatus = _root.Q<Label>("global-noproxy-status");
            _proxyInput    = _root.Q<TextField>("proxy-input");
            _httpInput     = _root.Q<TextField>("http-input");
            _httpsInput    = _root.Q<TextField>("https-input");
            _noProxyInput  = _root.Q<TextField>("noproxy-input");
            _verifyToggle  = _root.Q<Toggle>("verify-toggle");
            _btnClear      = _root.Q<Button>("btn-clear");
            _btnRefresh    = _root.Q<Button>("btn-refresh");
            _btnApply      = _root.Q<Button>("btn-apply");
            _feedback      = _root.Q<Label>("global-feedback");
        }

        private void RegisterCallbacks()
        {
            _btnApply?.RegisterCallback<ClickEvent>(_ => OnApply());
            _btnClear?.RegisterCallback<ClickEvent>(_ => OnClear());
            _btnRefresh?.RegisterCallback<ClickEvent>(_ => Refresh());

            // --proxy fills both http and https
            _proxyInput?.RegisterValueChangedCallback(evt =>
            {
                if (_httpInput != null  && string.IsNullOrEmpty(_httpInput.value))
                    _httpInput.SetValueWithoutNotify(evt.newValue);
                if (_httpsInput != null && string.IsNullOrEmpty(_httpsInput.value))
                    _httpsInput.SetValueWithoutNotify(evt.newValue);
            });
        }

        public void Refresh()
        {
            var cfg = EnvVarManager.GetProxyConfig(EnvLevel.User);
            SetStatus(_httpStatus,    cfg.HttpProxy);
            SetStatus(_httpsStatus,   cfg.HttpsProxy);
            SetStatus(_noProxyStatus, cfg.NoProxy);
            ClearFeedback();
        }

        private void OnApply()
        {
            var http    = string.IsNullOrEmpty(_proxyInput?.value) ? _httpInput?.value  : _proxyInput.value;
            var https   = string.IsNullOrEmpty(_proxyInput?.value) ? _httpsInput?.value : _proxyInput.value;
            var noProxy = _noProxyInput?.value;

            var cfg = new ProxyConfig
            {
                HttpProxy  = http,
                HttpsProxy = https ?? http,
                NoProxy    = noProxy
            };

            var validation = ConfigValidator.ValidateProxyConfig(cfg);
            if (!validation.IsValid)
            {
                ShowFeedback($"Validation failed: {validation.ErrorMessage}", FeedbackType.Error);
                return;
            }

            EnvVarManager.SetProxyForCurrentProcess(cfg);
            ShowFeedback("Global proxy applied.", FeedbackType.Success);
            Refresh();
        }

        private void OnClear()
        {
            EnvVarManager.SetProxyForCurrentProcess(new ProxyConfig());
            _proxyInput?.SetValueWithoutNotify("");
            _httpInput?.SetValueWithoutNotify("");
            _httpsInput?.SetValueWithoutNotify("");
            _noProxyInput?.SetValueWithoutNotify("");
            ShowFeedback("Global proxy cleared.", FeedbackType.Info);
            Refresh();
        }

        private static void SetStatus(Label label, string value)
        {
            if (label == null) return;
            if (string.IsNullOrEmpty(value))
            {
                label.text = "(not set)";
                label.RemoveFromClassList("status-value");
                label.AddToClassList("status-value--empty");
            }
            else
            {
                label.text = value;
                label.RemoveFromClassList("status-value--empty");
                label.AddToClassList("status-value");
            }
        }

        private enum FeedbackType { Success, Error, Info }
        private void ShowFeedback(string msg, FeedbackType type)
        {
            if (_feedback == null) return;
            _feedback.text = msg;
            _feedback.RemoveFromClassList("feedback-label--success");
            _feedback.RemoveFromClassList("feedback-label--error");
            _feedback.RemoveFromClassList("feedback-label--info");
            _feedback.AddToClassList(type switch
            {
                FeedbackType.Success => "feedback-label--success",
                FeedbackType.Error   => "feedback-label--error",
                _                   => "feedback-label--info"
            });
        }
        private void ClearFeedback() { if (_feedback != null) _feedback.text = ""; }
    }
}
