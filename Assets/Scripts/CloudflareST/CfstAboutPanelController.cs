// UTF-8
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 关于面板控制器，对应 CfstAboutPanel.uxml
    /// </summary>
    public class CfstAboutPanelController
    {
        private readonly VisualElement _root;

        public CfstAboutPanelController(VisualElement root)
        {
            _root = root;
            var vl = _root.Q<Label>("about-version");
            if (vl != null) vl.text = $"v{Application.version}";
        }

        public void Refresh() { }
    }
}
