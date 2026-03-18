using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageAboutController : MonoBehaviour
    {
        private VisualElement _root;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;

            // GUI 版本
            var labelGui = root.Q<Label>("label-gui-version");
            if (labelGui != null)
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                var ver = asm.GetName().Version;
                labelGui.text = ver != null ? ver.ToString() : "1.0.0";
            }

            // cfst.dll 版本（读取 ProductVersion，含语义版本+commit hash）
            var labelDll = root.Q<Label>("label-dll-version");
            if (labelDll != null)
            {
                try
                {
                    var dllPath = typeof(CloudflareST.CfstRunner).Assembly.Location;
                    var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(dllPath);
                    var productVer = fvi.ProductVersion;
                    if (!string.IsNullOrEmpty(productVer))
                    {
                        // 截取 '+' 之前的语义版本（如 "1.0.0"），去掉 commit hash
                        var plusIdx = productVer.IndexOf('+');
                        labelDll.text = plusIdx > 0 ? productVer.Substring(0, plusIdx) : productVer;
                    }
                    else
                    {
                        var dllVer = typeof(CloudflareST.CfstRunner).Assembly.GetName().Version;
                        labelDll.text = dllVer != null ? dllVer.ToString() : "unknown";
                    }
                }
                catch { labelDll.text = "unknown"; }
            }

            // 运行平台
            var labelPlatform = root.Q<Label>("label-platform");
            if (labelPlatform != null)
                labelPlatform.text = Application.platform.ToString()
                    + "  Unity " + Application.unityVersion;

            // 按钮链接
            root.Q<Button>("btn-gui-repo")?.RegisterCallback<ClickEvent>(_ =>
                NativePlatform.Shell.OpenUrl("https://github.com/codingriver/UniToolGUI"));
        }
    }
}
