using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Gate.Managers;
using Gate.Models;

namespace AIGate.UI
{
    /// <summary>Preset panel controller (PresetPanel.uxml)</summary>
    public class PresetPanelController
    {
        private readonly VisualElement _root;
        private ListView  _presetList;
        private Label     _detailName, _detailCreated, _detailUpdated, _detailHttp, _detailAppCount;
        private Button    _btnApply, _btnSetDefault, _btnExport, _btnDelete, _btnNewPreset, _btnSave;
        private TextField _saveNameInput;
        private Label     _feedback;
        private VisualElement _newOverlay;
        private TextField _newPresetName, _newPresetDesc;
        private Button    _btnNewCancel, _btnNewSave;

        private List<string> _profiles = new();
        private string _selectedName;

        public PresetPanelController(VisualElement root)
        {
            _root = root;
            Bind(); RegisterCallbacks();
        }

        private void Bind()
        {
            _presetList     = _root.Q<ListView>("preset-list");
            _detailName     = _root.Q<Label>("detail-name");
            _detailCreated  = _root.Q<Label>("detail-created");
            _detailUpdated  = _root.Q<Label>("detail-updated");
            _detailHttp     = _root.Q<Label>("detail-http");
            _detailAppCount = _root.Q<Label>("detail-app-count");
            _btnApply       = _root.Q<Button>("btn-apply-preset");
            _btnSetDefault  = _root.Q<Button>("btn-set-default");
            _btnExport      = _root.Q<Button>("btn-export-preset");
            _btnDelete      = _root.Q<Button>("btn-delete-preset");
            _btnNewPreset   = _root.Q<Button>("btn-new-preset");
            _saveNameInput  = _root.Q<TextField>("save-name-input");
            _btnSave        = _root.Q<Button>("btn-save-preset");
            _feedback       = _root.Q<Label>("preset-feedback");
            _newOverlay     = _root.Q<VisualElement>("new-preset-overlay");
            _newPresetName  = _root.Q<TextField>("new-preset-name");
            _newPresetDesc  = _root.Q<TextField>("new-preset-desc");
            _btnNewCancel   = _root.Q<Button>("btn-new-cancel");
            _btnNewSave     = _root.Q<Button>("btn-new-save");
        }

        private void RegisterCallbacks()
        {
            _btnApply?.RegisterCallback<ClickEvent>(_ => ApplySelected());
            _btnSetDefault?.RegisterCallback<ClickEvent>(_ => SetDefault());
            _btnDelete?.RegisterCallback<ClickEvent>(_ => DeleteSelected());
            _btnNewPreset?.RegisterCallback<ClickEvent>(_ => OpenNewOverlay());
            _btnSave?.RegisterCallback<ClickEvent>(_ => SaveCurrent());
            _btnNewCancel?.RegisterCallback<ClickEvent>(_ => CloseNewOverlay());
            _btnNewSave?.RegisterCallback<ClickEvent>(_ => CreateNew());

            if (_presetList != null)
            {
                _presetList.makeItem = () => { var l = new Label(); l.AddToClassList("list-row"); return l; };
                _presetList.bindItem = (el, i) =>
                {
                    var lbl = el as Label;
                    if (lbl == null) return;
                    var name = _profiles[i];
                    var def  = ProfileManager.GetDefaultProfile();
                    lbl.text = name == def ? $"{name}  (default)" : name;
                    lbl.userData = name;
                };
                _presetList.selectionChanged += objs =>
                {
                    foreach (var o in objs)
                        if (o is string s) { _selectedName = s; ShowDetail(s); break; }
                };
                _presetList.itemsSource = _profiles;
            }
        }

        public void Refresh()
        {
            _profiles = ProfileManager.List();
            _presetList?.Rebuild();
            if (_feedback != null) _feedback.text = "";
        }

        private void ShowDetail(string name)
        {
            var profile = ProfileManager.Load(name);
            if (profile == null) return;
            if (_detailName    != null) _detailName.text     = profile.Name;
            if (_detailCreated != null) _detailCreated.text  = profile.CreatedAt.ToShortDateString();
            if (_detailUpdated != null) _detailUpdated.text  = profile.UpdatedAt.ToShortDateString();
            if (_detailHttp    != null) _detailHttp.text     = profile.EnvVars?.HttpProxy ?? "(not set)";
            if (_detailAppCount!= null) _detailAppCount.text = $"{profile.ToolConfigs.Count} apps";
        }

        private void ApplySelected()
        {
            if (string.IsNullOrEmpty(_selectedName)) return;
            var p = ProfileManager.Load(_selectedName);
            if (p == null) { ShowFeedback($"Preset not found: {_selectedName}", true); return; }
            EnvVarManager.SetProxyForCurrentProcess(p.EnvVars);
            ShowFeedback($"Preset '{_selectedName}' applied.", false);
        }

        private void SetDefault()
        {
            if (string.IsNullOrEmpty(_selectedName)) return;
            ProfileManager.SetDefaultProfile(_selectedName);
            ShowFeedback($"Default set to '{_selectedName}'.", false);
            Refresh();
        }

        private void DeleteSelected()
        {
            if (string.IsNullOrEmpty(_selectedName)) return;
            ProfileManager.Delete(_selectedName);
            ShowFeedback($"Deleted '{_selectedName}'.", false);
            _selectedName = null;
            Refresh();
        }

        private void SaveCurrent()
        {
            var name = _saveNameInput?.value?.Trim();
            if (string.IsNullOrEmpty(name)) { ShowFeedback("Enter a name.", true); return; }
            var p = new Profile { Name = name, EnvVars = EnvVarManager.GetProxyConfig(EnvLevel.User) };
            foreach (var t in ToolRegistry.GetAllTools())
            { var c = t.GetCurrentConfig(); if (c != null) p.ToolConfigs[t.ToolName] = c; }
            ProfileManager.Save(p);
            if (_saveNameInput != null) _saveNameInput.value = "";
            ShowFeedback($"Preset '{name}' saved.", false);
            Refresh();
        }

        private void OpenNewOverlay()
        { if (_newOverlay != null) _newOverlay.style.display = DisplayStyle.Flex; }
        private void CloseNewOverlay()
        { if (_newOverlay != null) _newOverlay.style.display = DisplayStyle.None; }

        private void CreateNew()
        {
            var name = _newPresetName?.value?.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var p = new Profile
            {
                Name        = name,
                Description = _newPresetDesc?.value ?? "",
                EnvVars     = EnvVarManager.GetProxyConfig(EnvLevel.User)
            };
            foreach (var t in ToolRegistry.GetAllTools())
            { var c = t.GetCurrentConfig(); if (c != null) p.ToolConfigs[t.ToolName] = c; }
            ProfileManager.Save(p);
            CloseNewOverlay();
            ShowFeedback($"Preset '{name}' created.", false);
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
