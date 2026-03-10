using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Gate.Managers;
using Gate.Configurators;

namespace AIGate.UI
{
    /// <summary>App proxy panel controller (AppPanel.uxml)</summary>
    public class AppPanelController
    {
        private readonly VisualElement _root;
        private TextField _searchField;
        private DropdownField _categoryFilter;
        private Toggle _installedOnlyToggle;
        private ListView _appList;
        private Label _batchCount, _feedback;
        private TextField _batchProxyInput;
        private Button _btnSelectInstalled, _btnBatchSet, _btnBatchClear;
        private VisualElement _editOverlay;
        private Label _editAppName;
        private TextField _editProxyInput;
        private Button _btnEditCancel, _btnEditSave;

        private List<ToolConfiguratorBase> _allTools;
        private List<ToolConfiguratorBase> _filteredTools;
        private readonly HashSet<string> _selected = new();
        private ToolConfiguratorBase _editingTool;

        public AppPanelController(VisualElement root)
        {
            _root = root;
            _allTools = ToolRegistry.GetAllTools().ToList();
            _filteredTools = new List<ToolConfiguratorBase>(_allTools);
            Bind(); PopulateCategories(); RegisterCallbacks(); BuildList();
        }

        private void Bind()
        {
            _searchField         = _root.Q<TextField>("app-search");
            _categoryFilter      = _root.Q<DropdownField>("category-filter");
            _installedOnlyToggle = _root.Q<Toggle>("installed-only-toggle");
            _appList             = _root.Q<ListView>("app-list");
            _batchCount          = _root.Q<Label>("batch-count");
            _batchProxyInput     = _root.Q<TextField>("batch-proxy-input");
            _btnSelectInstalled  = _root.Q<Button>("btn-select-installed");
            _btnBatchSet         = _root.Q<Button>("btn-batch-set");
            _btnBatchClear       = _root.Q<Button>("btn-batch-clear");
            _feedback            = _root.Q<Label>("app-feedback");
            _editOverlay         = _root.Q<VisualElement>("edit-overlay");
            _editAppName         = _root.Q<Label>("edit-app-name");
            _editProxyInput      = _root.Q<TextField>("edit-proxy-input");
            _btnEditCancel       = _root.Q<Button>("btn-edit-cancel");
            _btnEditSave         = _root.Q<Button>("btn-edit-save");
        }

        private void PopulateCategories()
        {
            if (_categoryFilter == null) return;
            var cats = new List<string> { "All" };
            cats.AddRange(ToolRegistry.GetCategories());
            _categoryFilter.choices = cats;
            _categoryFilter.value   = "All";
        }

        private void RegisterCallbacks()
        {
            _searchField?.RegisterValueChangedCallback(_ => ApplyFilter());
            _categoryFilter?.RegisterValueChangedCallback(_ => ApplyFilter());
            _installedOnlyToggle?.RegisterValueChangedCallback(_ => ApplyFilter());
            _btnSelectInstalled?.RegisterCallback<ClickEvent>(_ => SelectAllInstalled());
            _btnBatchSet?.RegisterCallback<ClickEvent>(_ => BatchSet());
            _btnBatchClear?.RegisterCallback<ClickEvent>(_ => BatchClear());
            _btnEditCancel?.RegisterCallback<ClickEvent>(_ => CloseEdit());
            _btnEditSave?.RegisterCallback<ClickEvent>(_ => SaveEdit());
        }

        private void BuildList()
        {
            if (_appList == null) return;
            _appList.makeItem = () =>
            {
                var row = new VisualElement(); row.AddToClassList("list-row");
                var name = new Label(); name.AddToClassList("list-row-name"); name.name = "rName";
                var cat  = new Label(); cat.AddToClassList("list-row-category"); cat.name = "rCat";
                var dot  = new VisualElement(); dot.name = "rDot";
                var sts  = new VisualElement(); sts.AddToClassList("list-row-status"); sts.Add(dot);
                var acts = new VisualElement(); acts.AddToClassList("list-row-actions");
                var btnSet   = new Button { text = "Set",   name = "rBtnSet" }; btnSet.AddToClassList("btn"); btnSet.AddToClassList("btn-primary"); btnSet.AddToClassList("btn-sm");
                var btnClear = new Button { text = "Clear", name = "rBtnClr" }; btnClear.AddToClassList("btn"); btnClear.AddToClassList("btn-danger"); btnClear.AddToClassList("btn-sm");
                acts.Add(btnSet); acts.Add(btnClear);
                row.Add(name); row.Add(cat); row.Add(sts); row.Add(acts);
                return row;
            };
            _appList.bindItem = (el, i) =>
            {
                var tool = _filteredTools[i];
                el.Q<Label>("rName").text = tool.ToolName;
                el.Q<Label>("rCat").text  = tool.Category;
                var dot = el.Q<VisualElement>("rDot");
                var cfg = tool.GetCurrentConfig();
                var configured = cfg != null && !cfg.IsEmpty;
                dot.RemoveFromClassList("status-dot-on"); dot.RemoveFromClassList("status-dot-off");
                dot.AddToClassList(configured ? "status-dot-on" : "status-dot-off");
                var btnSet   = el.Q<Button>("rBtnSet");
                var btnClear = el.Q<Button>("rBtnClr");
                btnSet.clicked   -= () => {};
                btnClear.clicked -= () => {};
                var captured = tool;
                btnSet.clicked   += () => OpenEdit(captured);
                btnClear.clicked += () => { captured.ClearProxy(); Refresh(); };
            };
            _appList.itemsSource = _filteredTools;
            _appList.Rebuild();
        }

        public void Refresh()
        {
            ApplyFilter();
            if (_feedback != null) _feedback.text = "";
        }

        private void ApplyFilter()
        {
            var search   = _searchField?.value?.ToLower() ?? "";
            var cat      = _categoryFilter?.value ?? "All";
            var instOnly = _installedOnlyToggle?.value ?? false;
            _filteredTools = _allTools
                .Where(t => (string.IsNullOrEmpty(search) || t.ToolName.ToLower().Contains(search))
                         && (cat == "All" || t.Category == cat)
                         && (!instOnly || t.IsInstalled()))
                .ToList();
            if (_appList != null) { _appList.itemsSource = _filteredTools; _appList.Rebuild(); }
        }

        private void SelectAllInstalled()
        {
            _selected.Clear();
            foreach (var t in _filteredTools.Where(t => t.IsInstalled()))
                _selected.Add(t.ToolName);
            if (_batchCount != null) _batchCount.text = $"Selected {_selected.Count}";
        }

        private void BatchSet()
        {
            var proxy = _batchProxyInput?.value;
            if (string.IsNullOrEmpty(proxy)) { ShowFeedback("Enter proxy address first.", true); return; }
            int ok = 0;
            foreach (var n in _selected)
            {
                var t = ToolRegistry.GetByName(n);
                if (t != null && t.IsInstalled() && t.SetProxy(proxy)) ok++;
            }
            ShowFeedback($"Set proxy for {ok} apps.", false);
            Refresh();
        }

        private void BatchClear()
        {
            int ok = 0;
            foreach (var n in _selected)
            { var t = ToolRegistry.GetByName(n); if (t != null && t.IsInstalled() && t.ClearProxy()) ok++; }
            ShowFeedback($"Cleared proxy for {ok} apps.", false);
            Refresh();
        }

        private void OpenEdit(ToolConfiguratorBase tool)
        {
            _editingTool = tool;
            if (_editAppName  != null) _editAppName.text = $"Edit: {tool.ToolName}";
            var cfg = tool.GetCurrentConfig();
            if (_editProxyInput != null)
                _editProxyInput.value = cfg?.HttpProxy ?? "";
            if (_editOverlay != null)
                _editOverlay.style.display = DisplayStyle.Flex;
        }

        private void CloseEdit()
        {
            if (_editOverlay != null) _editOverlay.style.display = DisplayStyle.None;
            _editingTool = null;
        }

        private void SaveEdit()
        {
            if (_editingTool == null) return;
            var proxy = _editProxyInput?.value ?? "";
            if (!string.IsNullOrEmpty(proxy))
                _editingTool.SetProxy(proxy);
            CloseEdit();
            ShowFeedback($"{_editingTool?.ToolName ?? "App"}: proxy saved.", false);
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
