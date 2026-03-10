using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Gate.Managers;
using Gate.Models;

namespace AIGate.UI
{
    /// <summary>
    /// 状态总览面板控制器
    /// 对应 StatusPanel.uxml
    /// </summary>
    public class StatusPanelController
    {
        private readonly VisualElement _root;

        private Label     _statusHttp, _statusHttps, _statusNoProxy;
        private Button    _btnEditHttp, _btnEditHttps, _btnEditNoProxy;
        private Label     _configuredCount;
        private VisualElement _toolList;
        private Label     _currentPresetLabel;
        private Button    _btnRefresh;

        public StatusPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
            RegisterCallbacks();
        }

        private void BindElements()
        {
            _statusHttp         = _root.Q<Label>("status-http");
            _statusHttps        = _root.Q<Label>("status-https");
            _statusNoProxy      = _root.Q<Label>("status-noproxy");
            _btnEditHttp        = _root.Q<Button>("btn-edit-http");
            _btnEditHttps       = _root.Q<Button>("btn-edit-https");
            _btnEditNoProxy     = _root.Q<Button>("btn-edit-noproxy");
            _configuredCount    = _root.Q<Label>("configured-count");
            _toolList           = _root.Q<VisualElement>("status-tool-list");
            _currentPresetLabel = _root.Q<Label>("current-preset-label");
            _btnRefresh         = _root.Q<Button>("btn-refresh-status");
        }

        private void RegisterCallbacks()
        {
            _btnRefresh?.RegisterCallback<ClickEvent>(_ => Refresh());

            // Quick-edit env var buttons — navigate to Global panel via event
            _btnEditHttp?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[StatusPanel] Navigate to Global panel to edit HTTP_PROXY"));
            _btnEditHttps?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[StatusPanel] Navigate to Global panel to edit HTTPS_PROXY"));
            _btnEditNoProxy?.RegisterCallback<ClickEvent>(_ =>
                Debug.Log("[StatusPanel] Navigate to Global panel to edit NO_PROXY"));
        }

        public void Refresh()
        {
            RefreshEnvVars();
            RefreshToolList();
            RefreshPreset();
        }

        private void RefreshEnvVars()
        {
            var cfg = EnvVarManager.GetProxyConfig(EnvLevel.User);
            SetStatusLabel(_statusHttp,    cfg.HttpProxy);
            SetStatusLabel(_statusHttps,   cfg.HttpsProxy);
            SetStatusLabel(_statusNoProxy, cfg.NoProxy);
        }

        private void RefreshToolList()
        {
            if (_toolList == null) return;
            _toolList.Clear();

            var configured = new List<(string cat, string name, string proxy, bool installed)>();
            foreach (var cat in ToolRegistry.GetCategories())
            {
                foreach (var tool in ToolRegistry.GetByCategory(cat))
                {
                    var cfg = tool.GetCurrentConfig();
                    if (cfg == null || cfg.IsEmpty) continue;
                    var proxy = cfg.HttpProxy ?? cfg.HttpsProxy ?? cfg.ToString();
                    configured.Add((cat, tool.ToolName, proxy, tool.IsInstalled()));
                }
            }

            if (_configuredCount != null)
                _configuredCount.text = $"已配置 {configured.Count} 个";

            // Group by category
            var groups = configured.GroupBy(t => t.cat);
            foreach (var grp in groups)
            {
                var groupLabel = new Label { text = grp.Key };
                groupLabel.AddToClassList("status-group-title");
                _toolList.Add(groupLabel);

                foreach (var (_, name, proxy, installed) in grp)
                {
                    var row = new VisualElement();
                    row.AddToClassList("status-tool-row");

                    var dot = new VisualElement();
                    dot.AddToClassList(installed ? "status-dot-on" : "status-dot-off");

                    var nameLabel = new Label { text = name };
                    nameLabel.AddToClassList("status-tool-name");

                    var proxyLabel = new Label { text = proxy };
                    proxyLabel.AddToClassList("status-tool-proxy");

                    row.Add(dot);
                    row.Add(nameLabel);
                    row.Add(proxyLabel);
                    _toolList.Add(row);
                }
            }

            if (!configured.Any())
            {
                var emptyLabel = new Label { text = "暂无应用代理配置" };
                emptyLabel.AddToClassList("status-value--empty");
                _toolList.Add(emptyLabel);
            }
        }

        private void RefreshPreset()
        {
            if (_currentPresetLabel == null) return;
            var def = ProfileManager.GetDefaultProfile();
            _currentPresetLabel.text = string.IsNullOrEmpty(def) ? "(无)" : def;
        }

        private static void SetStatusLabel(Label label, string? value)
        {
            if (label == null) return;
            if (string.IsNullOrEmpty(value))
            {
                label.text = "(未设置)";
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
    }
}
