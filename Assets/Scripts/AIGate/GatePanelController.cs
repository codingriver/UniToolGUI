using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIGate.UI
{
    /// <summary>
    /// Gate Unity GUI 主控制器，管理侧边导航切换和面板加载
    /// 对应 GateMainWindow.uxml
    /// </summary>
    public class GatePanelController : MonoBehaviour
    {
        [Header("UI Document")]
        public UIDocument uiDocument;

        [Header("Panel Assets")]
        public VisualTreeAsset globalPanelAsset;
        public VisualTreeAsset appPanelAsset;
        public VisualTreeAsset presetPanelAsset;
        public VisualTreeAsset statusPanelAsset;
        public VisualTreeAsset testPanelAsset;
        public VisualTreeAsset toolPathPanelAsset;

        private VisualElement _root;
        private VisualElement _contentArea;

        // Sub-controllers
        private GlobalPanelController  _globalCtrl;
        private AppPanelController     _appCtrl;
        private PresetPanelController  _presetCtrl;
        private StatusPanelController  _statusCtrl;
        private TestPanelController    _testCtrl;
        private ToolPathPanelController _toolPathCtrl;

        private enum Panel { Global, App, Preset, Status, Test, ToolPath }
        private Panel _activePanel = Panel.Global;

        private readonly Dictionary<Panel, Button>        _navButtons = new();
        private readonly Dictionary<Panel, VisualElement> _panels     = new();

        void OnEnable()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[GatePanelController] UIDocument is not assigned!");
                return;
            }

            // ── Fix UIDocument root: Unity injects a wrapper VisualElement
            //    that has no background and no stretch by default.
            //    We must force it to fill the screen and apply dark bg.
            var docRoot = uiDocument.rootVisualElement;
            docRoot.style.flexGrow    = 1;
            docRoot.style.flexShrink  = 0;
            docRoot.style.width       = new StyleLength(new Length(100f, LengthUnit.Percent));
            docRoot.style.height      = new StyleLength(new Length(100f, LengthUnit.Percent));
            docRoot.style.backgroundColor = new StyleColor(new Color(0.059f, 0.067f, 0.090f)); // #0f1117

            _root        = docRoot;
            _contentArea = _root.Q<VisualElement>("content-area");

            // Version label
            var versionLabel = _root.Q<Label>("version-label");
            if (versionLabel != null)
                versionLabel.text = $"v{Application.version}";

            // Wire nav buttons
            BindNav("nav-global",  Panel.Global);
            BindNav("nav-app",     Panel.App);
            BindNav("nav-preset",  Panel.Preset);
            BindNav("nav-status",  Panel.Status);
            BindNav("nav-test",    Panel.Test);
            BindNav("nav-paths",   Panel.ToolPath);

            // Wizard button — CLI only; show hint in Unity context
            var wizardBtn = _root.Q<Button>("nav-wizard");
            if (wizardBtn != null)
                wizardBtn.clicked += OnWizardClicked;

            // Build and mount panels
            BuildPanels();

            // Show default panel
            SwitchPanel(Panel.Global);
        }

        void OnDisable()
        {
            // Unregister nav button callbacks to prevent leaks
            foreach (var kv in _navButtons)
                kv.Value.clicked -= () => SwitchPanel(kv.Key);
        }

        // ── Navigation ──────────────────────────────────────────────────────

        private void BindNav(string buttonName, Panel panel)
        {
            var btn = _root.Q<Button>(buttonName);
            if (btn == null)
            {
                Debug.LogWarning($"[GatePanelController] Nav button not found: {buttonName}");
                return;
            }
            _navButtons[panel] = btn;
            btn.clicked += () => SwitchPanel(panel);
        }

        private void SwitchPanel(Panel panel)
        {
            // Update nav active state
            foreach (var kv in _navButtons)
            {
                kv.Value.RemoveFromClassList("nav-item--active");
                if (kv.Key == panel)
                    kv.Value.AddToClassList("nav-item--active");
            }

            // Show/hide panels
            foreach (var kv in _panels)
                kv.Value.style.display = (kv.Key == panel)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            _activePanel = panel;

            // Refresh data on panel show
            switch (panel)
            {
                case Panel.Global:   _globalCtrl?.Refresh();   break;
                case Panel.App:      _appCtrl?.Refresh();      break;
                case Panel.Preset:   _presetCtrl?.Refresh();   break;
                case Panel.Status:   _statusCtrl?.Refresh();   break;
                case Panel.Test:     _testCtrl?.Refresh();     break;
                case Panel.ToolPath: _toolPathCtrl?.Refresh(); break;
            }
        }

        // ── Panel Construction ───────────────────────────────────────────────

        private void BuildPanels()
        {
            _panels[Panel.Global]   = InstantiatePanel(globalPanelAsset,   "GlobalPanel");
            _panels[Panel.App]      = InstantiatePanel(appPanelAsset,      "AppPanel");
            _panels[Panel.Preset]   = InstantiatePanel(presetPanelAsset,   "PresetPanel");
            _panels[Panel.Status]   = InstantiatePanel(statusPanelAsset,   "StatusPanel");
            _panels[Panel.Test]     = InstantiatePanel(testPanelAsset,     "TestPanel");
            _panels[Panel.ToolPath] = InstantiatePanel(toolPathPanelAsset, "ToolPathPanel");

            // Attach sub-controllers
            _globalCtrl   = new GlobalPanelController(_panels[Panel.Global]);
            _appCtrl      = new AppPanelController(_panels[Panel.App]);
            _presetCtrl   = new PresetPanelController(_panels[Panel.Preset]);
            _statusCtrl   = new StatusPanelController(_panels[Panel.Status]);
            _testCtrl     = new TestPanelController(_panels[Panel.Test]);
            _toolPathCtrl = new ToolPathPanelController(_panels[Panel.ToolPath]);

            // Add all to content area (hidden by default)
            foreach (var kv in _panels)
            {
                _contentArea.Add(kv.Value);
                kv.Value.style.display = DisplayStyle.None;
            }
        }

        private VisualElement InstantiatePanel(VisualTreeAsset asset, string panelName)
        {
            if (asset == null)
            {
                Debug.LogWarning($"[GatePanelController] Panel asset is null: {panelName}");
                var placeholder = new VisualElement { style = { flexGrow = 1 } };
                var label = new Label { text = $"{panelName} — asset not assigned" };
                label.style.color = new UnityEngine.UIElements.StyleColor(new UnityEngine.Color(1f, 0.4f, 0.4f));
                placeholder.Add(label);
                return placeholder;
            }
            var container = new VisualElement { style = { flexGrow = 1 } };
            asset.CloneTree(container);
            return container;
        }

        // ── Wizard ──────────────────────────────────────────────────────────

        private void OnWizardClicked()
        {
            Debug.Log("[Gate] 向导功能为 CLI 专属，请在终端运行: gate wizard");
        }
    }
}
