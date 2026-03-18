using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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

        // (domain, ipRank) pairs bound to CfstOptions.HostEntries via CfstConfigBuilder
        private readonly List<(string domain, int rank)> _entries = new List<(string, int)>();

        public void Init(VisualElement root, CfstOptions opts)
        {
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

            // ── 管理员权限检测 ───────────────────────────────────
            var restartAdminBtn = root.Q<Button>("btn-restart-admin");
            if (restartAdminBtn != null)
            {
                bool isAdmin = WindowsAdmin.IsRunningAsAdmin();
                // 已是管理员则隐藏重启按钮，同时将提示条改为 info 样式
                if (isAdmin)
                {
                    restartAdminBtn.style.display = DisplayStyle.None;
                    _permInfoBar?.RemoveFromClassList("info-bar--warning");
                    _permInfoBar?.AddToClassList("info-bar--info");
                    if (_permText != null) _permText.text = "✓ 已以管理员身份运行，Hosts 更新功能完整可用";
                }
                else
                {
                    restartAdminBtn.RegisterCallback<ClickEvent>(_ =>
                    {
                        bool ok = WindowsAdmin.RestartAsAdmin();
                        if (!ok)
                            NativePlatform.MessageBox.Warning(
                                "提升权限失败，请手动以管理员身份运行本程序。",
                                "权限不足");
                    });
                }
            }

            UpdatePermHint();

            _enableToggle?.RegisterValueChangedCallback(e =>
            {
                _hostsParams?.SetEnabled(e.newValue);
                if (!e.newValue) _entries.Clear();
                RebuildEntriesFromOpts();
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
            });

            _hostsParams?.SetEnabled(false);
            RefreshEmptyHint();
        }

        // ── 添加一条条目 ──────────────────────────────────────
        private void AddEntry(string domain, int rank)
        {
            _entries.Add((domain, rank));
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
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginBottom  = 4;

            var domainField = new TextField();
            domainField.value = domain;
            domainField.style.flexGrow = 1;
            domainField.AddToClassList("field-text");
            // placeholder is set via UXML placeholder-text attribute (Unity 2022 compatible)
            domainField.RegisterValueChangedCallback(e =>
            {
                if (idx < _entries.Count)
                    _entries[idx] = (e.newValue.Trim(), _entries[idx].rank);
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
                    _entries[idx] = (_entries[idx].domain, e.newValue < 1 ? 1 : e.newValue);
                FlushToOpts();
            });

            var delBtn = new Button(() => RemoveEntry(row, idx));
            delBtn.text = "✕";
            delBtn.style.width      = 28;
            delBtn.style.height     = 28;
            delBtn.style.marginLeft = 4;
            delBtn.AddToClassList("btn-sm");

            row.Add(domainField);
            row.Add(rankField);
            row.Add(delBtn);
            return row;
        }

        private void RemoveEntry(VisualElement row, int idx)
        {
            if (idx < _entries.Count)
                _entries.RemoveAt(idx);
            row.RemoveFromHierarchy();
            // Rebuild remaining rows so indices stay correct
            RebuildRows();
            RefreshEmptyHint();
            FlushToOpts();
        }

        private void RebuildRows()
        {
            _entriesList?.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                var (domain, rank) = _entries[i];
                _entriesList?.Add(BuildEntryRow(i, domain, rank));
            }
        }

        private void RebuildEntriesFromOpts()
        {
            _entries.Clear();
            _entriesList?.Clear();
            // Parse existing HostsDomains back into entries (comma-separated domains, single rank)
            if (!string.IsNullOrWhiteSpace(_opts.HostsDomains))
            {
                foreach (var d in _opts.HostsDomains.Split(','))
                {
                    var trimmed = d.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        AddEntry(trimmed, _opts.HostsIpRank);
                }
            }
            RefreshEmptyHint();
        }

        private void FlushToOpts()
        {
            // Build HostsDomains as comma-separated list
            var domains = new System.Collections.Generic.List<string>();
            foreach (var (domain, _) in _entries)
                if (!string.IsNullOrWhiteSpace(domain))
                    domains.Add(domain);
            _opts.HostsDomains = domains.Count > 0
                ? string.Join(",", domains)
                : null;
            // Use rank from first entry for backward compat; CfstConfigBuilder handles per-entry
            _opts.HostsIpRank = _entries.Count > 0 ? _entries[0].rank : 1;
        }

        private void RefreshEmptyHint()
        {
            if (_entriesEmpty == null) return;
            _entriesEmpty.style.display =
                (_entries.Count == 0) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── 浏览 / 验证 ───────────────────────────────────────
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
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _permText.text = "Windows 需以管理员身份运行；权限不足时内容将输出到 hosts-pending.txt";
#else
            _permText.text = "需要 root 用户或 sudo 运行；权限不足时内容将输出到 hosts-pending.txt";
#endif
        }
    }
}
