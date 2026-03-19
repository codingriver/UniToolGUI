using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    /// <summary>
    /// Toast 轻量提示系统。挂载到场景中任意 GameObject 上，需要引用 UIDocument。
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        private VisualElement _toastContainer;
        private UIDocument    _doc;

        public enum ToastType { Info, Success, Warning, Error }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = FindObjectOfType<UIDocument>();
            if (_doc == null) { Debug.LogWarning("[Toast] UIDocument not found"); return; }

            _toastContainer = new VisualElement();
            _toastContainer.name = "toast-container";
            _toastContainer.pickingMode = PickingMode.Ignore;
            _toastContainer.style.position   = Position.Absolute;
            _toastContainer.style.bottom      = 48; // 状态栏上方
            _toastContainer.style.right       = 16;
            _toastContainer.style.flexDirection = FlexDirection.Column;
            _toastContainer.style.alignItems    = Align.FlexEnd;
            _doc.rootVisualElement.Add(_toastContainer);
        }

        /// <summary>显示一条 Toast。可从任意线程调用。</summary>
        public static void Show(string message, ToastType type = ToastType.Info, float durationSec = 2.5f)
        {
            if (Instance == null) { Debug.LogWarning("[Toast] " + message); return; }
            Instance.StartCoroutine(Instance.ShowCoroutine(message, type, durationSec));
        }

        public static void Info   (string msg, float dur = 2.5f) => Show(msg, ToastType.Info,    dur);
        public static void Success(string msg, float dur = 2.5f) => Show(msg, ToastType.Success, dur);
        public static void Warning(string msg, float dur = 3.0f) => Show(msg, ToastType.Warning, dur);
        public static void Error  (string msg, float dur = 4.0f) => Show(msg, ToastType.Error,   dur);

        private IEnumerator ShowCoroutine(string message, ToastType type, float durationSec)
        {
            if (_toastContainer == null) yield break;

            var toast = new VisualElement();
            toast.AddToClassList("toast");
            toast.AddToClassList("toast--" + type.ToString().ToLower());
            toast.pickingMode = PickingMode.Ignore;

            var icon = new Label(GetIcon(type));
            icon.AddToClassList("toast__icon");

            var text = new Label(message);
            text.AddToClassList("toast__text");
            text.style.whiteSpace = WhiteSpace.Normal;

            toast.Add(icon);
            toast.Add(text);
            _toastContainer.Add(toast);

            // 淡入
            toast.style.opacity = 0;
            yield return null;
            toast.style.opacity = 1;
            toast.style.transitionProperty   = new StyleList<StylePropertyName>(new System.Collections.Generic.List<StylePropertyName> { new StylePropertyName("opacity") });
            toast.style.transitionDuration   = new StyleList<TimeValue>(new System.Collections.Generic.List<TimeValue> { new TimeValue(300, TimeUnit.Millisecond) });

            yield return new WaitForSeconds(durationSec);

            // 淡出
            toast.style.opacity = 0;
            yield return new WaitForSeconds(0.35f);

            toast.RemoveFromHierarchy();
        }

        private static string GetIcon(ToastType t)
        {
            switch (t)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "⚠";
                case ToastType.Error:   return "✗";
                default:                return "ℹ";
            }
        }
    }
}
