using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Gate.Configurators;
using Gate.Managers;

namespace AIGate.UI
{
    /// <summary>
    /// 工具路径自定义面板控制器
    /// 对应 ToolPathPanel.uxml
    ///
    /// 功能：
    ///   - 显示所有工具，标注哪些有自定义路径
    ///   - 弹窗编辑工具的可执行文件路径和配置文件路径
    ///   - 持久化保存到 gate_tool_paths.json
    /// </summary>
    public class ToolPathPanelController
    {
        private readonly VisualElement _root;

        private TextField _searchField;
        private Toggle    _customOnlyToggle;
        private ListView  _list;
        private Label     _feedback;
        private Button    _btnResetAll;

        private VisualElement _overlay;
        private Label         _overlayTitle;
        private TextField     _execPathInput;
        private TextField     _configPathInput;
        private Button        _btnSave, _btnCancel, _btnClear;

        private List<ToolConfiguratorBase> _allTools;
        private List<ToolConfiguratorBase> _filtered;
        private ToolConfiguratorBase       _editingTool;

        public ToolPathPanelController(VisualElement root)
        {
            _root     = root;
            _allTools = ToolRegistry.GetAllTools().ToList();
            _filtered = new List<ToolConfiguratorBase>(_allTools);
            Bind();
            RegisterCallbacks();
            BuildList();
        }

        private void Bind()
        {
            _searchField      = _root.Q<TextField>("path-search");
            _customOnlyToggle = _root.Q<Toggle>("custom-only-toggle");
            _list             = _root.Q<ListView>("path-tool-list");
            _feedback         = _root.Q<Label>("path-feedback");
            _btnResetAll      = _root.Q<Button>("btn-reset-all");

            _overlay         = _root.Q<VisualElement>("path-edit-overlay");
            _overlayTitle    = _root.Q<Label>("path-edit-title");
            _execPathInput   = _root.Q<TextField>("exec-path-input");
            _configPathInput = _root.Q<TextField>("config-path-input");
            _btnSave         = _root.Q<Button>("btn-path-save");
            _btnCancel       = _root.Q<Button>("btn-path-cancel");
            _btnClear        = _root.Q<Button>("btn-path-clear");
        }

        private void RegisterCallbacks()
        {
            _searchField?.RegisterValueChangedCallback(_ => ApplyFilter());
            _customOnlyToggle?.RegisterValueChangedCallback(_ => ApplyFilter());
            _btnResetAll?.RegisterCallback<ClickEvent>(_ => ResetAll());
            _btnSave?.RegisterCallback<ClickEvent>(_ => SaveEdit());
            _btnCancel?.RegisterCallback<ClickEvent>(_ => CloseOverlay());
            _btnClear?.RegisterCallback<ClickEvent>(_ => ClearEdit());
        }

        private void BuildList()
        {
            if (_list == null) return;

            _list.makeItem = () =>
            {
                var row = new VisualElement(); row.AddToClassList("list-row");

                var name   = new VisualElement(); name.AddToClassList("col-name");
                var lName  = new Label(); lName.name = "lName"; lName.AddToClassList("list-row-name");
                var lCat   = new Label(); lCat.name  = "lCat";  lCat.AddToClassList("list-row-category");
                name.Add(lName); name.Add(lCat);

                var status = new VisualElement(); status.AddToClassList("col-status");
                var lStatus = new Label(); lStatus.name = "lStatus"; lStatus.style.fontSize = 11;
                status.Add(lStatus);

                var actions = new VisualElement(); actions.AddToClassList("list-row-actions");
                var btnEdit = new Button { text = "配置路径", name = "btnEdit" };
                btnEdit.AddToClassList("btn"); btnEdit.AddToClassList("btn-secondary"); btnEdit.AddToClassList("btn-sm");
                actions.Add(btnEdit);

                row.Add(name); row.Add(status); row.Add(actions);
                return row;
            };

            _list.bindItem = (el, i) =>
            {
                var tool = _filtered[i];
                el.Q<Label>("lName").text = tool.ToolName;
                el.Q<Label>("lCat").text  = tool.Category;

                var hasCustom = ToolPathConfig.HasCustomPath(tool.ToolName);
                var lStatus   = el.Q<Label>("lStatus");
                if (hasCustom)
                {
                    lStatus.text = "已自定义";
                    lStatus.style.color = new UnityEngine.UIElements.StyleColor(
                        UnityEngine.ColorUtility.TryParseHtmlString("#4f8ef7", out var c) ? c : UnityEngine.Color.white);
                }
                else
                {
                    lStatus.text = tool.IsInstalled() ? "自动检测" : "未安装";
                    lStatus.style.color = new UnityEngine.UIElements.StyleColor(
                        UnityEngine.ColorUtility.TryParseHtmlString(
                            tool.IsInstalled() ? "#64748b" : "#334155", out var c2) ? c2 : UnityEngine.Color.gray);
                }

                var btnEdit = el.Q<Button>("btnEdit");
                btnEdit.clicked -= () => {};
                var captured = tool;
                btnEdit.clicked += () => OpenOverlay(captured);
            };

            _list.itemsSource = _filtered;
            _list.Rebuild();
        }

        public void Refresh()
        {
            ApplyFilter();
            if (_feedback != null) _feedback.text = "";
        }

        private void ApplyFilter()
        {
            var search     = _searchField?.value?.ToLower() ?? "";
            var customOnly = _customOnlyToggle?.value ?? false;

            _filtered = _allTools
                .Where(t =>
                    (string.IsNullOrEmpty(search) || t.ToolName.ToLower().Contains(search))
                    && (!customOnly || ToolPathConfig.HasCustomPath(t.ToolName)))
                .ToList();

            if (_list != null) { _list.itemsSource = _filtered; _list.Rebuild(); }
        }

        private void OpenOverlay(ToolConfiguratorBase tool)
        {
            _editingTool = tool;
            if (_overlayTitle    != null) _overlayTitle.text    = $"配置路径: {tool.ToolName}";

            var entry = ToolPathConfig.Get(tool.ToolName);
            if (_execPathInput   != null) _execPathInput.SetValueWithoutNotify(entry?.executablePath ?? "");
            if (_configPathInput != null) _configPathInput.SetValueWithoutNotify(entry?.configFilePath ?? "");

            if (_overlay != null) _overlay.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        }

        private void CloseOverlay()
        {
            if (_overlay != null) _overlay.style.display = UnityEngine.UIElements.DisplayStyle.None;
            _editingTool = null;
        }

        private void SaveEdit()
        {
            if (_editingTool == null) return;
            var execPath   = _execPathInput?.value ?? "";
            var configPath = _configPathInput?.value ?? "";
            ToolPathConfig.Set(_editingTool.ToolName, execPath, configPath);
            ShowFeedback($"{_editingTool.ToolName} 路径已保存", false);
            CloseOverlay();
            Refresh();
        }

        private void ClearEdit()
        {
            if (_editingTool == null) return;
            ToolPathConfig.Clear(_editingTool.ToolName);
            ShowFeedback($"{_editingTool.ToolName} 已恢复自动检测", false);
            CloseOverlay();
            Refresh();
        }

        private void ResetAll()
        {
            foreach (var t in _allTools)
                ToolPathConfig.Clear(t.ToolName);
            ShowFeedback("已重置所有工具路径", false);
            Refresh();
        }

        private void ShowFeedback(string msg, bool isError)
        {
            if (_feedback == null) return;
            _feedback.text = msg;
            _feedback.EnableInClassList("feedback-label--error",   isError);
            _feedback.EnableInClassList("feedback-label--success", !isError);
        }
    }
}
