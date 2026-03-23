using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainWindowLayoutBootstrap : MonoBehaviour
    {
        [Header("Visual Trees")]
        [SerializeField] private VisualTreeAsset desktopWindow;
        [SerializeField] private VisualTreeAsset mobileWindow;

        [Header("Preview")]
        [SerializeField] private MainWindowLayoutPreset layoutPreset = MainWindowLayoutPreset.Auto;

        private UIDocument _document;
        private MainWindowLayoutKind _currentKind;
        private bool _bootstrapped;

        public MainWindowLayoutKind CurrentLayoutKind => _currentKind;
        public bool IsMobileStructure { get; private set; }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            ApplyLayout();
        }

        private void OnEnable()
        {
            ApplyRuntimeClasses();
        }

        private void Update()
        {
            if (!_bootstrapped || !IsMobileStructure)
                return;

            var next = MainWindowLayoutResolver.Resolve(layoutPreset, desktopWindow, mobileWindow);
            if (next.LayoutKind != _currentKind)
            {
                _currentKind = next.LayoutKind;
                ApplyRuntimeClasses();
            }
        }

        private void ApplyLayout()
        {
            if (_document == null)
                return;

            var decision = MainWindowLayoutResolver.Resolve(layoutPreset, desktopWindow, mobileWindow);
            _currentKind = decision.LayoutKind;
            IsMobileStructure = decision.IsMobileStructure;

            if (decision.WindowAsset != null)
                _document.visualTreeAsset = decision.WindowAsset;

            _bootstrapped = true;
        }

        private void ApplyRuntimeClasses()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
                return;

            root.EnableInClassList("layout-desktop", _currentKind == MainWindowLayoutKind.Desktop);
            root.EnableInClassList("layout-phone", _currentKind == MainWindowLayoutKind.Phone);
            root.EnableInClassList("layout-tablet", _currentKind == MainWindowLayoutKind.Tablet);
            root.EnableInClassList("layout-mobile", IsMobileStructure);
        }
    }
}
