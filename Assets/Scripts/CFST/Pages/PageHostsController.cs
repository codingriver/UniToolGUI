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
        private TextField     _domainsField;
        private IntegerField  _ipRankField;
        private TextField     _hostsFileField;
        private Label         _hintHostsFile;
        private Toggle        _dryRunToggle;
        private Label         _hintDryRun;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _permInfoBar    = root.Q<VisualElement>("perm-infobar");
            _permText       = root.Q<Label>("perm-text");
            _enableToggle   = root.Q<Toggle>("toggle-hosts-enable");
            _hostsParams    = root.Q<VisualElement>("hosts-params");
            _domainsField   = root.Q<TextField>("field-hostsdomains");
            _ipRankField    = root.Q<IntegerField>("field-hostsiprank");
            _hostsFileField = root.Q<TextField>("field-hostsfile");
            _hintHostsFile  = root.Q<Label>("hint-hostsfile");
            _dryRunToggle   = root.Q<Toggle>("toggle-hostsdryrun");
            _hintDryRun     = root.Q<Label>("hint-dryrun");

            root.Q<Button>("btn-browse-hosts")?.RegisterCallback<ClickEvent>(_ => BrowseHostsFile());

            // 权限提示
            UpdatePermHint();

            _enableToggle?.RegisterValueChangedCallback(e =>
            {
                SetParamsEnabled(e.newValue);
                if (!e.newValue) _opts.HostsDomains = null;
            });

            _domainsField?.RegisterValueChangedCallback(e =>
                _opts.HostsDomains = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim());

            _ipRankField?.RegisterValueChangedCallback(e =>
                _opts.HostsIpRank = e.newValue < 1 ? 1 : e.newValue);

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

            // 初始禁用子参数
            SetParamsEnabled(false);
        }

        private void SetParamsEnabled(bool enabled)
        {
            _hostsParams?.SetEnabled(enabled);
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
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            _permText.text = "Windows 需以管理员身份运行；权限不足时内容将输出到 hosts-pending.txt";
#else
            _permText.text = "需要 root 用户或 sudo 运行；权限不足时内容将输出到 hosts-pending.txt";
#endif
        }
    }
}
