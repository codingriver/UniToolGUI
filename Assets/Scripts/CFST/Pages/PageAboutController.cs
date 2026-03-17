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

            // cfst.dll 版本
            var labelDll = root.Q<Label>("label-dll-version");
            if (labelDll != null)
            {
                try
                {
                    var dllAsm = typeof(CloudflareST.CfstRunner).Assembly;
                    var dllVer = dllAsm.GetName().Version;
                    labelDll.text = dllVer != null ? dllVer.ToString() : "unknown";
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
