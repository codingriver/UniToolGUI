using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NativeKit;

namespace CloudflareST.GUI
{
    public class PageHostsController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private VisualElement _permInfoBar;
        private Label         _permText;
        private Toggle        _enableToggle;
        private VisualElement _hostsParams;
        private VisualElement _entriesList;
        private Label         _entriesEmpty;
        private TextField     _hostsFileField;
        private Label         _hintHostsFile;
        private Toggle        _dryRunToggle;
        private Label         _hintDryRun;
        private bool          _isAdmin;

        private readonly List<HostDomainEntry> _entries = new List<HostDomainEntry>();

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageHostsController.Init root is null");
                return;
            }

            _root = root;
            _opts = opts;

            _permInfoBar    = root.Q<VisualElement>("perm-infobar");
            _permText       = root.Q<Label>("perm-text");
            _enableToggle   = root.Q<Toggle>("toggle-hosts-enable");
            _hostsParams    = root.Q<VisualElement>("hosts-params");
            _entriesList    = root.Q<VisualElement>("hosts-entries-list");
            _entriesEmpty   = root.Q<Label>("hosts-entries-empty");
            _hostsFileField = root.Q<TextField>("field-hostsfile");
            _hintHostsFile  = root.Q<Label>("hint-hostsfile");
            _dryRunToggle   = root.Q<Toggle>("toggle-hostsdryrun");
            _hintDryRun     = root.Q<Label>("hint-dryrun");

            root.Q<Button>("btn-browse-hosts")?.RegisterCallback<ClickEvent>(_ => BrowseHostsFile());
            root.Q<Button>("btn-add-entry")   ?.RegisterCallback<ClickEvent>(_ => AddEntry("", 1));

            _isAdmin = false;
            try { _isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }

            var permRestartRow = root.Q<VisualElement>("perm-restart-row");
            var permIcon       = root.Q<Label>("perm-icon");
            var restartAdminBtn = root.Q<Button>("btn-restart-admin");

            if (_isAdmin)
            {
                _permInfoBar?.RemoveFromClassList("info-bar--warning");
                _permInfoBar?.AddToClassList("info-bar--success");
                if (permIcon   != null) permIcon.text  = "✓";
                if (_permText  != null) _permText.text = GetPermissionHintText(true);
                if (permRestartRow != null) permRestartRow.style.display = DisplayStyle.None;
            }
            else
            {
                _permInfoBar?.RemoveFromClassList("info-bar--warning");
                _permInfoBar?.AddToClassList("info-bar--error");
                if (permIcon   != null) permIcon.text  = "✕";
                if (_permText  != null) _permText.text = GetPermissionHintText(false);
#if UNITY_STANDALONE_OSX
                if (permRestartRow != null) permRestartRow.style.display = DisplayStyle.None;
#else
                if (permRestartRow != null) permRestartRow.style.display = DisplayStyle.Flex;
                restartAdminBtn?.RegisterCallback<ClickEvent>(_ =>
                {
                    bool ok = false;
                    try { ok = WindowsAdmin.RestartAsAdmin(); } catch { }
                    if (!ok)
                        NativePlatform.MessageBox.Warning(
                            "提升权限失败，请手动以管理员身份运行本程序。",
                            "权限不足");
                });
#endif
            }

            UpdatePermHint();

#if UNITY_STANDALONE_OSX
            if (!_isAdmin)
                FileLogger.Log("[Hosts] macOS 当前采用按操作提权写入模式，不提供整应用管理员重启");
#endif

            _enableToggle?.RegisterValueChangedCallback(e =>
            {
                _hostsParams?.SetEnabled(e.newValue);
                if (!e.newValue)
                {
                    _entries.Clear();
                    FlushToOpts();
                    RebuildRows();
                }
                else
                {
                    RebuildEntriesFromOpts();
                }
            });

            _hostsFileField?.RegisterValueChangedCallback(e =>
            {
                _opts.HostsFile = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim();
                ValidateHostsFile(e.newValue);
            });

            _dryRunToggle?.RegisterValueChangedCallback(e =>
            {
                _opts.HostsDryRun = e.newValue;
                if (_hintDryRun != null)
                    _hintDryRun.text = e.newValue
                        ? "测速完成后仅在结果页预览待写入内容，不修改系统 hosts"
                        : "";
                if (e.newValue)
                    UIKit.ToastManager.Info("已启用预览模式，不会写入 Hosts");
                else
                    UIKit.ToastManager.Warning("预览模式已关闭，将实际写入 Hosts");
            });

            _hostsParams?.SetEnabled(false);

            string defaultHostsPath;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            defaultHostsPath = @"C:\Windows\System32\drivers\etc\hosts";
#else
            defaultHostsPath = "/etc/hosts";
#endif
            if (_hostsFileField != null)
                _hostsFileField.SetValueWithoutNotify(
                    string.IsNullOrEmpty(_opts.HostsFile) ? defaultHostsPath : _opts.HostsFile);
            if (_dryRunToggle != null)
                _dryRunToggle.SetValueWithoutNotify(_opts.HostsDryRun);
            if (_enableToggle != null)
            {
                bool enabled = _opts.HostsDomains != null && _opts.HostsDomains.Count > 0;
                _enableToggle.SetValueWithoutNotify(enabled);
                _hostsParams?.SetEnabled(enabled);
                if (enabled) RebuildEntriesFromOpts();
            }

            RefreshEmptyHint();
        }

        private void AddEntry(string domain, int rank)
        {
            _entries.Add(new HostDomainEntry
            {
                Domain = domain,
                IpRank = rank < 1 ? 1 : rank
            });
            int idx = _entries.Count - 1;
            var row = BuildEntryRow(idx, domain, rank);
            _entriesList?.Add(row);
            RefreshEmptyHint();
            FlushToOpts();
        }

        private VisualElement BuildEntryRow(int idx, string domain, int rank)
        {
            var row = new VisualElement();
            row.name = "entry-row-" + idx;
            row.style.flexDirection = FlexDirection.Column;
            row.style.marginBottom  = 8;

            var hint = new Label("支持通配符域名（如 *.example.com）或普通域名（如 www.example.com）");
            hint.style.fontSize    = 11;
            hint.style.color       = new UnityEngine.UIElements.StyleColor(new UnityEngine.Color(0.35f, 0.55f, 0.65f));
            hint.style.marginBottom = 3;
            hint.style.whiteSpace  = WhiteSpace.Normal;

            var inputRow = new VisualElement();
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems    = Align.Center;

            var domainField = new TextField();
            domainField.value = domain;
            domainField.style.flexGrow = 1;
            domainField.AddToClassList("field-text");
            domainField.RegisterValueChangedCallback(e =>
            {
                if (idx < _entries.Count)
                    _entries[idx].Domain = e.newValue.Trim();
                FlushToOpts();
            });

            var rankField = new IntegerField();
            rankField.value = rank;
            rankField.style.width = 70;
            rankField.style.marginLeft = 6;
            rankField.AddToClassList("field-int");
            rankField.RegisterValueChangedCallback(e =>
            {
                if (idx < _entries.Count)
                    _entries[idx].IpRank = e.newValue < 1 ? 1 : e.newValue;
                FlushToOpts();
            });

            var delBtn = new Button(() => RemoveEntry(row, idx));
            delBtn.text = "✕";
            delBtn.style.width      = 28;
            delBtn.style.height     = 28;
            delBtn.style.marginLeft = 4;
            delBtn.AddToClassList("btn-sm");

            inputRow.Add(domainField);
            inputRow.Add(rankField);
            inputRow.Add(delBtn);

            row.Add(hint);
            row.Add(inputRow);
            return row;
        }

        private void RemoveEntry(VisualElement row, int idx)
        {
            if (idx < _entries.Count)
                _entries.RemoveAt(idx);
            row.RemoveFromHierarchy();
            RebuildRows();
            RefreshEmptyHint();
            FlushToOpts();
        }

        private void RebuildRows()
        {
            _entriesList?.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                _entriesList?.Add(BuildEntryRow(i, entry.Domain, entry.IpRank));
            }
        }

        private void RebuildEntriesFromOpts()
        {
            _entries.Clear();
            _entriesList?.Clear();

            if (_opts.HostsDomains != null)
            {
                foreach (var entry in _opts.HostsDomains)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Domain)) continue;
                    AddEntry(entry.Domain, entry.IpRank);
                }
            }
            RefreshEmptyHint();
        }

        private void FlushToOpts()
        {
            var entries = new List<HostDomainEntry>();
            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Domain)) continue;
                entries.Add(new HostDomainEntry
                {
                    Domain = entry.Domain.Trim(),
                    IpRank = entry.IpRank < 1 ? 1 : entry.IpRank
                });
            }
            _opts.HostsDomains = entries;
        }

        private void RefreshEmptyHint()
        {
            if (_entriesEmpty == null) return;
            _entriesEmpty.style.display =
                (_entries.Count == 0) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BrowseHostsFile()
        {
            string filter = NativePlatform.FileDialog.CreateFilter("所有文件(*.*)", "*.*");
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string init = @"C:\Windows\System32\drivers\etc";
#else
            string init = "/etc";
#endif
            string path = NativePlatform.FileDialog.OpenFilePanel("选择 hosts 文件", filter, "", init);
            if (!string.IsNullOrEmpty(path) && _hostsFileField != null)
                _hostsFileField.value = path;
        }

        private void ValidateHostsFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || _hintHostsFile == null) return;
            if (!System.IO.File.Exists(path))
            {
                _hostsFileField?.AddToClassList("field-text--warning");
                _hintHostsFile.text = "⚠ 路径不存在，运行时将尝试创建";
            }
            else
            {
                _hostsFileField?.RemoveFromClassList("field-text--warning");
                _hintHostsFile.text = "";
            }
        }

        private void UpdatePermHint()
        {
            if (_permText == null) return;
            _permText.text = GetPermissionHintText(_isAdmin);
        }

        private static string GetPermissionHintText(bool isAdmin)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return isAdmin
                ? "当前已是管理员账户，Hosts 更新功能完整可用"
                : "Windows 需以管理员身份运行；权限不足时内容将输出到 hosts-pending.txt";
#else
            return isAdmin
                ? "当前已具备 root 权限，Hosts 更新功能完整可用"
                : "macOS/Linux 将在实际写入 Hosts 时单独申请权限；未授权时内容会输出到 hosts-pending.txt";
#endif
        }
    }
}
