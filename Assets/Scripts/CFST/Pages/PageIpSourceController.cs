using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageIpSourceController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private TextField     _ipv4Field;
        private TextField     _ipv6Field;
        private Label         _hintIpv4;
        private Label         _hintIpv6;
        private TextField     _ipRangesField;
        private IntegerField  _ipLoadLimitField;
        private Toggle        _allIpToggle;
        private Label         _hintAllIp;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _ipv4Field        = root.Q<TextField>("field-ipv4");
            _ipv6Field        = root.Q<TextField>("field-ipv6");
            _hintIpv4         = root.Q<Label>("hint-ipv4");
            _hintIpv6         = root.Q<Label>("hint-ipv6");
            _ipRangesField    = root.Q<TextField>("field-ipranges");
            _ipLoadLimitField = root.Q<IntegerField>("field-iploadlimit");
            _allIpToggle      = root.Q<Toggle>("toggle-allip");
            _hintAllIp        = root.Q<Label>("hint-allip");

            // 浏览按钮
            root.Q<Button>("btn-browse-ipv4")?.RegisterCallback<ClickEvent>(_ => BrowseFile(_ipv4Field));
            root.Q<Button>("btn-browse-ipv6")?.RegisterCallback<ClickEvent>(_ => BrowseFile(_ipv6Field));

            // 绑定控件 -> opts
            _ipv4Field?.RegisterValueChangedCallback(e =>
            {
                _opts.IPv4File = e.newValue;
                ValidatePath(e.newValue, _ipv4Field, _hintIpv4);
                UpdateFileHints();
            });
            _ipv6Field?.RegisterValueChangedCallback(e =>
            {
                _opts.IPv6File = e.newValue;
                ValidatePath(e.newValue, _ipv6Field, _hintIpv6);
                UpdateFileHints();
            });
            _ipRangesField?.RegisterValueChangedCallback(e =>
            {
                _opts.IpRanges = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim();
                UpdateFileHints();
            });
            _ipLoadLimitField?.RegisterValueChangedCallback(e => _opts.IpLoadLimit = e.newValue);
            _allIpToggle?.RegisterValueChangedCallback(e =>
            {
                _opts.AllIp = e.newValue;
                if (_hintAllIp != null)
                    _hintAllIp.text = e.newValue
                        ? "⚠ 全量扫描大幅增加扫描量，耗时可能超过10分钟，建议配合IP数量上限使用"
                        : "";
                if (e.newValue)
                    UIKit.ToastManager.Warning("已启用全量扫描，耗时可能超过10分钟");
            });

            // ── 回填持久化值到界面 ────────────────────────────
            if (_ipv4Field        != null) _ipv4Field.SetValueWithoutNotify(
                string.IsNullOrEmpty(_opts.IPv4File) ? SettingsStorage.GetDefaultIpv4File() : _opts.IPv4File);
            if (_ipv6Field        != null) _ipv6Field.SetValueWithoutNotify(
                string.IsNullOrEmpty(_opts.IPv6File) ? SettingsStorage.GetDefaultIpv6File() : _opts.IPv6File);
            if (_ipRangesField    != null) _ipRangesField.SetValueWithoutNotify(_opts.IpRanges ?? "");
            if (_ipLoadLimitField != null) _ipLoadLimitField.SetValueWithoutNotify(_opts.IpLoadLimit);
            if (_allIpToggle      != null) _allIpToggle.SetValueWithoutNotify(_opts.AllIp);

            // 确保 opts 也同步为默认值（首次运行时 SettingsStorage.Load 已设，此处兜底）
            if (string.IsNullOrEmpty(_opts.IPv4File)) _opts.IPv4File = SettingsStorage.GetDefaultIpv4File();
            if (string.IsNullOrEmpty(_opts.IPv6File)) _opts.IPv6File = SettingsStorage.GetDefaultIpv6File();
        }

        private void BrowseFile(TextField target)
        {
            string filter = NativePlatform.FileDialog.CreateFilter("文本文件(*.txt)", "*.txt", "所有文件(*.*)", "*.*");
            string path   = NativePlatform.FileDialog.OpenFilePanel("选择 IP 文件", filter, "txt");
            if (!string.IsNullOrEmpty(path)) target.value = path;
        }

        private void ValidatePath(string path, TextField field, Label hint)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            bool exists = System.IO.File.Exists(path);
            if (!exists)
            {
                field?.AddToClassList("field-text--error");
                if (hint != null) hint.text = "⚠ 文件不存在，将在运行时报错";
            }
            else
            {
                field?.RemoveFromClassList("field-text--error");
                if (hint != null) hint.text = "";
            }
        }

        private void UpdateFileHints()
        {
            bool hasRanges = !string.IsNullOrWhiteSpace(_opts.IpRanges);
            string ignored = hasRanges ? "当前使用直接 IP 段，文件配置已忽略" : "";
            if (_hintIpv4 != null && string.IsNullOrEmpty(_hintIpv4.text))
                _hintIpv4.text = ignored;
            if (_hintIpv6 != null && string.IsNullOrEmpty(_hintIpv6.text))
                _hintIpv6.text = ignored;
        }
    }
}
