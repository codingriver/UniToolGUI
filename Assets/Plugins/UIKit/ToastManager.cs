using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UIKit
{
    /// <summary>
    /// Toast 轻量提示系统。
    /// - 无需手动挂载，RuntimeInitializeOnLoadMethod 自动创建。
    /// - 线程安全：可从任意线程调用 Show/Info/Success/Warning/Error。
    /// - 时序安全：在 UIDocument 就绪前调用的 Toast 会在就绪后自动补显。
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        public static ToastManager Instance { get; private set; }

        private VisualElement _toastContainer;
        private bool          _ready;

        // 线程安全队列
        private readonly Queue<(string msg, ToastType type, float dur)> _pending =
            new Queue<(string, ToastType, float)>();
        private readonly object _lock = new object();

        public enum ToastType { Info, Success, Warning, Error }

        // ── 自动创建（无需场景手动挂载）────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[ToastManager]");
            go.AddComponent<ToastManager>();
            DontDestroyOnLoad(go);
            Debug.Log("[Toast] Auto-created instance.");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine(InitContainer());
        }

        // 等待 UIDocument 就绪（最多等 60 帧）
        private IEnumerator InitContainer()
        {
            UIDocument doc = null;
            int tries = 0;
            while (doc == null && tries < 60)
            {
                doc = FindObjectOfType<UIDocument>();
                if (doc == null) { yield return null; tries++; }
            }

            if (doc == null)
            {
                Debug.LogWarning("[Toast] UIDocument not found after 60 frames.");
                yield break;
            }

            // 等根元素就绪
            VisualElement root = null;
            tries = 0;
            while ((root = doc.rootVisualElement) == null && tries < 10)
            {
                yield return null;
                tries++;
            }

            if (root == null)
            {
                Debug.LogWarning("[Toast] rootVisualElement is null.");
                yield break;
            }

            // 复用或新建容器
            _toastContainer = root.Q<VisualElement>("toast-container");
            if (_toastContainer == null)
            {
                _toastContainer = new VisualElement();
                _toastContainer.name        = "toast-container";
                _toastContainer.pickingMode = PickingMode.Ignore;
                _toastContainer.style.position      = Position.Absolute;
                _toastContainer.style.bottom        = 48;
                _toastContainer.style.right         = 16;
                _toastContainer.style.flexDirection = FlexDirection.Column;
                _toastContainer.style.alignItems    = Align.FlexEnd;
                root.Add(_toastContainer);
            }

            _ready = true;
            Debug.Log("[Toast] Container ready.");
            FlushPending();
        }

        private void Update()
        {
            if (_ready) FlushPending();
        }

        private void FlushPending()
        {
            while (true)
            {
                (string msg, ToastType type, float dur) item;
                lock (_lock)
                {
                    if (_pending.Count == 0) break;
                    item = _pending.Dequeue();
                }
                StartCoroutine(ShowCoroutine(item.msg, item.type, item.dur));
            }
        }

        // ── 公开 API ─────────────────────────────────────────
        public static void Show(string message, ToastType type = ToastType.Info, float durationSec = 2.5f)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (Instance == null)
            {
                Debug.LogWarning("[Toast] Instance null, msg=" + message);
                return;
            }

            Debug.Log("[Toast] Enqueue: " + message);
            lock (Instance._lock)
                Instance._pending.Enqueue((message, type, durationSec));
        }

        public static void Info   (string msg, float dur = 2.5f) => Show(msg, ToastType.Info,    dur);
        public static void Success(string msg, float dur = 2.5f) => Show(msg, ToastType.Success, dur);
        public static void Warning(string msg, float dur = 3.0f) => Show(msg, ToastType.Warning, dur);
        public static void Error  (string msg, float dur = 4.0f) => Show(msg, ToastType.Error,   dur);

        // ── 显示协程 ─────────────────────────────────────────
        private IEnumerator ShowCoroutine(string message, ToastType type, float durationSec)
        {
            // 等容器
            int w = 0;
            while (_toastContainer == null && w++ < 30) yield return null;
            if (_toastContainer == null)
            {
                Debug.LogWarning("[Toast] Container still null, drop: " + message);
                yield break;
            }

            Debug.Log("[Toast] Show: " + message);

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
            toast.style.opacity = 0f;
            yield return null;
            toast.style.transitionProperty = new StyleList<StylePropertyName>(
                new List<StylePropertyName> { new StylePropertyName("opacity") });
            toast.style.transitionDuration = new StyleList<TimeValue>(
                new List<TimeValue> { new TimeValue(300, TimeUnit.Millisecond) });
            toast.style.opacity = 1f;

            yield return new WaitForSeconds(durationSec);

            // 淡出
            toast.style.opacity = 0f;
            yield return new WaitForSeconds(0.35f);

            toast.RemoveFromHierarchy();
        }

        private static string GetIcon(ToastType t)
        {
            switch (t)
            {
                case ToastType.Success: return "\u2713";
                case ToastType.Warning: return "\u26a0";
                case ToastType.Error:   return "\u2717";
                default:                return "\u2139";
            }
        }
    }
}
