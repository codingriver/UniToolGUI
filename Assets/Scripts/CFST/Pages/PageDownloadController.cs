using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageDownloadController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private Toggle        _disableDownloadToggle;
        private VisualElement _downloadParams;
        private TextField     _urlField;
        private Label         _hintUrl;
        private IntegerField  _portField;
        private IntegerField  _countField;
        private IntegerField  _timeoutField;
        private FloatField    _speedMinField;

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageDownloadController.Init root is null");
                return;
            }

            _root = root;
            _opts = opts;

            _disableDownloadToggle = root.Q<Toggle>("toggle-disabledownload");
            _downloadParams        = root.Q<VisualElement>("download-params");
            _urlField              = root.Q<TextField>("field-downloadurl");
            _hintUrl               = root.Q<Label>("hint-url");
            _portField             = root.Q<IntegerField>("field-downloadport");
            _countField            = root.Q<IntegerField>("field-downloadcount");
            _timeoutField          = root.Q<IntegerField>("field-downloadtimeout");
            _speedMinField         = root.Q<FloatField>("field-speedmin");

            _disableDownloadToggle?.RegisterValueChangedCallback(e =>
            {
                _opts.DisableDownload = e.newValue;
                SetParamsEnabled(!e.newValue);
                UIKit.ToastManager.Info(e.newValue ? "已禁用下载测速" : "已启用下载测速");
            });

            _urlField?.RegisterValueChangedCallback(e =>
            {
                _opts.DownloadUrl = e.newValue;
                ValidateUrl(e.newValue);
            });

            _portField?   .RegisterValueChangedCallback(e => _opts.DownloadPort    = Clamp(e.newValue, 1, 65535));
            _countField?  .RegisterValueChangedCallback(e => _opts.DownloadCount   = Clamp(e.newValue, 1, 100));
            _timeoutField?.RegisterValueChangedCallback(e => _opts.DownloadTimeout = Clamp(e.newValue, 1, 120));
            _speedMinField?.RegisterValueChangedCallback(e =>
                _opts.SpeedMin = e.newValue < 0 ? 0 : e.newValue);

            SetParamsEnabled(true);
        }

        private void SetParamsEnabled(bool enabled)
        {
            if (_downloadParams == null) return;
            _downloadParams.SetEnabled(enabled);
        }

        private void ValidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            bool valid = System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri);
            _urlField?.EnableInClassList("field-text--error", !valid);
            if (_hintUrl == null) return;
            if (!valid)
            {
                _hintUrl.text = "URL 格式不合法";
                return;
            }
            if (uri.Scheme == "http")
                _hintUrl.text = "HTTP 协议，建议将端口改为 80";
            else if (uri.Scheme == "https")
                _hintUrl.text = "HTTPS 协议，建议端口使用 443";
            else
                _hintUrl.text = "";
        }

        private static int Clamp(int v, int min, int max) =>
            v < min ? min : v > max ? max : v;
    }
}
