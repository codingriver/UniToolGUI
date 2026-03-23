using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    [RequireComponent(typeof(UIDocument))]
    public class UISafeAreaAdapter : MonoBehaviour
    {
        [SerializeField] private string targetElementName = "safe-area";

        private UIDocument _document;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastResolution.x != Screen.width || _lastResolution.y != Screen.height || _lastSafeArea != Screen.safeArea)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (_document == null)
                return;

            var root = _document.rootVisualElement;
            if (root == null)
                return;

            var target = string.IsNullOrEmpty(targetElementName)
                ? root
                : root.Q<VisualElement>(targetElementName) ?? root;

            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (!Application.isMobilePlatform)
            {
                target.style.paddingTop = 0;
                target.style.paddingRight = 0;
                target.style.paddingBottom = 0;
                target.style.paddingLeft = 0;
                return;
            }

            float left = safeArea.xMin;
            float right = Screen.width - safeArea.xMax;
            float bottom = safeArea.yMin;
            float top = Screen.height - safeArea.yMax;

            target.style.paddingLeft = left;
            target.style.paddingRight = right;
            target.style.paddingTop = top;
            target.style.paddingBottom = bottom;
        }
    }
}
