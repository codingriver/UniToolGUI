using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageAboutController : MonoBehaviour
    {
        private VisualElement _root;
        private MainWindowController _main;
        private int _guiVersionTapCount;
        private const int DebugUnlockTapCount = 5;

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageAboutController.Init root is null");
                return;
            }

            _root = root;
            _main = GetComponent<MainWindowController>() ?? FindObjectOfType<MainWindowController>();

            // GUI 版本
            var labelGui = root.Q<Label>("label-gui-version");
            if (labelGui != null)
            {
                labelGui.text = _main != null ? _main.AboutGuiVersionText : labelGui.text;
                labelGui.pickingMode = PickingMode.Position;
                labelGui.UnregisterCallback<ClickEvent>(OnGuiVersionClicked);
                labelGui.RegisterCallback<ClickEvent>(OnGuiVersionClicked);
            }

            // cfst.dll 版本（读取 ProductVersion，含语义版本+commit hash）
            var labelDll = root.Q<Label>("label-dll-version");
            if (labelDll != null)
                labelDll.text = _main != null ? _main.AboutDllVersionText : labelDll.text;

            // 运行平台（不显示 Unity 版本）
            var labelPlatform = root.Q<Label>("label-platform");
            if (labelPlatform != null)
                labelPlatform.text = Application.platform.ToString();

            // 按钮链接
            var repoBtn = root.Q<Button>("btn-gui-repo");
            if (repoBtn != null)
            {
                if (_main != null && !string.IsNullOrWhiteSpace(_main.AboutRepoButtonText))
                    repoBtn.text = _main.AboutRepoButtonText;
                repoBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    string url = _main != null ? _main.AboutRepoUrl : "https://github.com/codingriver/UniToolGUI";
                    NativePlatform.Shell.OpenUrl(url);
                });
            }

            var labelLicense = root.Q<Label>("label-license");
            if (labelLicense != null && _main != null && !string.IsNullOrWhiteSpace(_main.AboutLicenseText))
                labelLicense.text = _main.AboutLicenseText;
        }

        private void OnGuiVersionClicked(ClickEvent evt)
        {
            if (AppState.Instance.DebugUiUnlocked)
                return;

            _guiVersionTapCount++;
            if (_guiVersionTapCount >= DebugUnlockTapCount)
                AppState.Instance.DebugUiUnlocked = true;
        }
    }
}
