// UTF-8
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// CloudflareST Unity GUI 主控制器
    /// 管理侧边导航切换和各面板加载
    /// 对应 CfstMainWindow.uxml
    /// </summary>
    public class CfstWindowController : MonoBehaviour
    {
        [Header("UI Document")]
        public UIDocument uiDocument;

        [Header("Panel Assets")]
        public VisualTreeAsset configPanelAsset;
        public VisualTreeAsset runPanelAsset;
        public VisualTreeAsset resultPanelAsset;
        public VisualTreeAsset schedulePanelAsset;
        public VisualTreeAsset historyPanelAsset;
        public VisualTreeAsset aboutPanelAsset;

        private VisualElement _root;
        private VisualElement _content;

        // Sub-controllers
        private CfstConfigPanelController   _configCtrl;
        private CfstRunPanelController      _runCtrl;
        private CfstResultPanelController   _resultCtrl;
        private CfstSchedulePanelController _scheduleCtrl;
        private CfstHistoryPanelController  _historyCtrl;
        private CfstAboutPanelController    _aboutCtrl;

        private enum Panel { Config, Run, Result, Schedule, History, About }
        private Panel _activePanel = Panel.Config;

        private readonly Dictionary<Panel, Button>        _navBtns = new();
        private readonly Dictionary<Panel, VisualElement> _panels  = new();

        void OnEnable()
        {
            if (uiDocument == null) { Debug.LogError("[CfstWindow] UIDocument not assigned!"); return; }

            _root    = uiDocument.rootVisualElement;
            _content = _root.Q<VisualElement>("cfst-content");

            var versionLabel = _root.Q<Label>("cfst-version-label");
            if (versionLabel != null) versionLabel.text = $"v{Application.version}";

            BindNav("cfst-nav-config",   Panel.Config);
            BindNav("cfst-nav-run",      Panel.Run);
            BindNav("cfst-nav-result",   Panel.Result);
            BindNav("cfst-nav-schedule", Panel.Schedule);
            BindNav("cfst-nav-history",  Panel.History);
            BindNav("cfst-nav-about",    Panel.About);

            BuildPanels();
            SwitchPanel(Panel.Config);
        }

        private void BindNav(string btnName, Panel panel)
        {
            var btn = _root.Q<Button>(btnName);
            if (btn == null) { Debug.LogWarning($"[CfstWindow] Nav button not found: {btnName}"); return; }
            _navBtns[panel] = btn;
            btn.clicked += () => SwitchPanel(panel);
        }

        private void SwitchPanel(Panel panel)
        {
            foreach (var kv in _navBtns)
            {
                kv.Value.RemoveFromClassList("cfst-nav-item--active");
                if (kv.Key == panel) kv.Value.AddToClassList("cfst-nav-item--active");
            }
            foreach (var kv in _panels)
                kv.Value.style.display = kv.Key == panel ? DisplayStyle.Flex : DisplayStyle.None;

            _activePanel = panel;

            switch (panel)
            {
                case Panel.Config:   _configCtrl?.Refresh();   break;
                case Panel.Run:      _runCtrl?.Refresh();      break;
                case Panel.Result:   _resultCtrl?.Refresh();   break;
                case Panel.Schedule: _scheduleCtrl?.Refresh(); break;
                case Panel.History:  _historyCtrl?.Refresh();  break;
                case Panel.About:    _aboutCtrl?.Refresh();    break;
            }
        }

        private void BuildPanels()
        {
            _panels[Panel.Config]   = CreatePanel(configPanelAsset,   "ConfigPanel");
            _panels[Panel.Run]      = CreatePanel(runPanelAsset,       "RunPanel");
            _panels[Panel.Result]   = CreatePanel(resultPanelAsset,    "ResultPanel");
            _panels[Panel.Schedule] = CreatePanel(schedulePanelAsset,  "SchedulePanel");
            _panels[Panel.History]  = CreatePanel(historyPanelAsset,   "HistoryPanel");
            _panels[Panel.About]    = CreatePanel(aboutPanelAsset,     "AboutPanel");

            _configCtrl   = new CfstConfigPanelController(_panels[Panel.Config]);
            _runCtrl      = new CfstRunPanelController(_panels[Panel.Run]);
            _resultCtrl   = new CfstResultPanelController(_panels[Panel.Result]);
            _scheduleCtrl = new CfstSchedulePanelController(_panels[Panel.Schedule]);
            _historyCtrl  = new CfstHistoryPanelController(_panels[Panel.History]);
            _aboutCtrl    = new CfstAboutPanelController(_panels[Panel.About]);

            // Wire config into run panel so it can read current settings
            _runCtrl.ConfigCtrl = _configCtrl;

            // Wire result callback from run controller
            _runCtrl.OnTestComplete += result =>
            {
                _resultCtrl.SetResult(result);
                _historyCtrl.AddRecord(result);
                SwitchPanel(Panel.Result);
            };

            foreach (var kv in _panels)
            {
                _content.Add(kv.Value);
                kv.Value.style.display = DisplayStyle.None;
            }
        }

        private VisualElement CreatePanel(VisualTreeAsset asset, string name)
        {
            var container = new VisualElement { name = name, style = { flexGrow = 1 } };
            if (asset != null)
                asset.CloneTree(container);
            else
            {
                var lbl = new Label { text = $"{name} — asset not assigned" };
                lbl.style.color = new StyleColor(new Color(1f, 0.4f, 0.4f));
                container.Add(lbl);
            }
            return container;
        }
    }
}
