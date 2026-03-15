using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageOutputController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private TextField    _outputFileField;
        private Label        _hintOutputFile;
        private IntegerField _outputCountField;
        private Toggle       _silentToggle;
        private TextField    _onlyIpFileField;
        private Label        _labelLastFile;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _outputFileField  = root.Q<TextField>("field-outputfile");
            _hintOutputFile   = root.Q<Label>("hint-outputfile");
            _outputCountField = root.Q<IntegerField>("field-outputcount");
            _silentToggle     = root.Q<Toggle>("toggle-silent");
            _onlyIpFileField  = root.Q<TextField>("field-onlyipfile");
            _labelLastFile    = root.Q<Label>("label-lastfile");

            root.Q<Button>("btn-browse-output")?.RegisterCallback<ClickEvent>(_ => BrowseCsv());
            root.Q<Button>("btn-browse-onlyip")?.RegisterCallback<ClickEvent>(_ => BrowseOnlyIp());
            root.Q<Button>("btn-open-output-dir")?.RegisterCallback<ClickEvent>(_ => OpenOutputDir());

            _outputFileField?.RegisterValueChangedCallback(e =>
            {
                _opts.OutputFile = e.newValue;
                ValidateOutputDir(e.newValue);
            });

            _outputCountField?.RegisterValueChangedCallback(e =>
                _opts.OutputCount = e.newValue < 1 ? 1 : e.newValue);

            _silentToggle?.RegisterValueChangedCallback(e =>
            {
                _opts.Silent = e.newValue;
                _onlyIpFileField?.SetEnabled(e.newValue);
            });

            _onlyIpFileField?.RegisterValueChangedCallback(e => _opts.OnlyIpFile = e.newValue);

            // onlyip 初始禁用
            _onlyIpFileField?.SetEnabled(false);

            // 监听结果更新，刷新上次生成文件
            TestResult.Instance.OnResultUpdated += RefreshLastFile;
        }

        private void OnDestroy()
        {
            TestResult.Instance.OnResultUpdated -= RefreshLastFile;
        }

        private void BrowseCsv()
        {
            string filter = NativePlatform.FileDialog.CreateFilter("CSV 文件(*.csv)", "*.csv", "所有文件(*.*)", "*.*");
            string path   = NativePlatform.FileDialog.SaveFilePanel("保存结果文件", filter, "csv", null, "result");
            if (!string.IsNullOrEmpty(path) && _outputFileField != null)
                _outputFileField.value = path;
        }

        private void BrowseOnlyIp()
        {
            string filter = NativePlatform.FileDialog.CreateFilter("文本文件(*.txt)", "*.txt", "所有文件(*.*)", "*.*");
            string path   = NativePlatform.FileDialog.SaveFilePanel("保存 onlyip 文件", filter, "txt", null, "onlyip");
            if (!string.IsNullOrEmpty(path) && _onlyIpFileField != null)
                _onlyIpFileField.value = path;
        }

        private void ValidateOutputDir(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || _hintOutputFile == null) return;
            string dir = System.IO.Path.GetDirectoryName(
                System.IO.Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                _outputFileField?.AddToClassList("field-text--warning");
                _hintOutputFile.text = "⚠ 目录不存在，运行时将自动创建";
            }
            else
            {
                _outputFileField?.RemoveFromClassList("field-text--warning");
                _hintOutputFile.text = "";
            }
        }

        private void OpenOutputDir()
        {
            string path = System.IO.Path.GetFullPath(_opts.OutputFile ?? "result.csv");
            if (!System.IO.File.Exists(path)) return;
            NativePlatform.Shell.OpenFolder(
                System.IO.Path.GetDirectoryName(path), path);
        }

        private void RefreshLastFile()
        {
            if (_labelLastFile == null) return;
            string path = System.IO.Path.GetFullPath(_opts.OutputFile ?? "result.csv");
            if (System.IO.File.Exists(path))
            {
                var info = new System.IO.FileInfo(path);
                _labelLastFile.text =
                    $"{info.Name}  {info.LastWriteTime:yyyy-MM-dd HH:mm}  {info.Length / 1024.0:F1} KB";
            }
            else
            {
                _labelLastFile.text = "尚未生成";
            }
        }
    }
}
