using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIGate.UI
{
    /// <summary>
    /// 系统字体加载器 - 不引入任何字体文件
    ///
    /// 实现机制：
    ///   Unity UIToolkit 的 VisualElement.style.unityFont 接受
    ///   Font 对象（Legacy Font）。Font.CreateDynamicFontFromOSFont()
    ///   可以按名称从操作系统字体目录创建动态字体，无需打包字体文件。
    ///
    ///   回退链（按顺序尝试）：
    ///   Windows: Microsoft YaHei UI > Microsoft YaHei > SimHei > NSimSun
    ///   macOS:   PingFang SC > Hiragino Sans GB > STHeiti
    ///   Linux:   WenQuanYi Micro Hei > Noto Sans CJK SC > AR PL UMing CN
    ///   最终回退: Unity 内置 LiberationSans（无中文，但不会崩溃）
    ///
    /// 使用方式：
    ///   将此脚本挂载到与 GatePanelController 相同的 GameObject。
    ///   无需在 Inspector 中配置任何内容。
    ///
    /// 修复说明（动态面板问题）：
    ///   原实现只在 OnEnable/OnFirstLayout 时走一次树，若 GatePanelController
    ///   在之后才调用 BuildPanels()（CloneTree 动态注入面板），新增节点不会
    ///   获得字体。现在新增 public ApplyFont(VisualElement) 方法，
    ///   供 GatePanelController 在 BuildPanels() 完成后主动调用。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ChineseFontLoader : MonoBehaviour
    {
        // 系统字体候选列表（按优先级排列）
        private static readonly string[] FontCandidates =
        {
            // Windows
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "SimHei",
            "NSimSun",
            "SimSun",
            // macOS
            "PingFang SC",
            "Hiragino Sans GB",
            "STHeiti",
            "Heiti SC",
            // Linux
            "WenQuanYi Micro Hei",
            "Noto Sans CJK SC",
            "AR PL UMing CN",
            "Droid Sans Fallback",
        };

        private UIDocument _uiDocument;
        private Font _resolvedFont;

        void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            _resolvedFont = ResolveSystemFont();
        }

        void OnEnable()
        {
            if (_uiDocument == null) return;
            var root = _uiDocument.rootVisualElement;
            if (root == null) return;

            // Apply to whatever is currently in the tree.
            // GatePanelController.OnEnable may run before or after this;
            // ApplyFont() below covers panels added afterwards.
            if (root.resolvedStyle.width > 0)
                ApplyFontToTree(root);
            else
                root.RegisterCallback<GeometryChangedEvent>(OnFirstLayout);
        }

        void OnDisable()
        {
            if (_uiDocument?.rootVisualElement != null)
                _uiDocument.rootVisualElement
                    .UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
        }

        private void OnFirstLayout(GeometryChangedEvent evt)
        {
            var root = _uiDocument.rootVisualElement;
            root.UnregisterCallback<GeometryChangedEvent>(OnFirstLayout);
            ApplyFontToTree(root);
        }

        /// <summary>
        /// 对指定子树应用中文字体。
        /// GatePanelController 在 BuildPanels() 完成、所有面板 CloneTree 注入后
        /// 调用此方法，确保动态添加的面板节点也获得正确字体。
        /// </summary>
        public void ApplyFont(VisualElement subtree)
        {
            if (subtree == null) return;
            if (_resolvedFont == null)
            {
                Debug.LogWarning("[ChineseFontLoader] No CJK OS font available; skipping ApplyFont.");
                return;
            }
            SetFontRecursive(subtree, _resolvedFont);
            Debug.Log($"[ChineseFontLoader] ApplyFont({subtree.name}) with {_resolvedFont.name}");
        }

        // ── Internal ────────────────────────────────────────────────────────

        private void ApplyFontToTree(VisualElement root)
        {
            if (_resolvedFont != null)
            {
                SetFontRecursive(root, _resolvedFont);
                Debug.Log($"[ChineseFontLoader] Applied OS font to full tree: {_resolvedFont.name}");
            }
            else
            {
                Debug.LogWarning("[ChineseFontLoader] No CJK OS font found. " +
                    "Chinese text may appear as blank squares. " +
                    "Install a CJK font on the OS.");
            }
        }

        private static void SetFontRecursive(VisualElement el, Font font)
        {
            el.style.unityFont = new StyleFont(font);
            foreach (var child in el.Children())
                SetFontRecursive(child, font);
        }

        /// <summary>
        /// 从操作系统字体中解析第一个可用的 CJK 字体
        /// </summary>
        private static Font ResolveSystemFont()
        {
            var installedFonts = new HashSet<string>(
                Font.GetOSInstalledFontNames(),
                System.StringComparer.OrdinalIgnoreCase
            );

            foreach (var candidate in FontCandidates)
            {
                if (!installedFonts.Contains(candidate)) continue;

                var font = Font.CreateDynamicFontFromOSFont(candidate, 14);
                if (font != null)
                {
                    Debug.Log($"[ChineseFontLoader] Found OS font: {candidate}");
                    return font;
                }
            }

            // 最终回退：尝试任意包含 CJK 关键字的字体
            foreach (var name in Font.GetOSInstalledFontNames())
            {
                if (!ContainsCJKKeyword(name)) continue;
                var font = Font.CreateDynamicFontFromOSFont(name, 14);
                if (font != null)
                {
                    Debug.Log($"[ChineseFontLoader] Fallback OS font: {name}");
                    return font;
                }
            }

            return null;
        }

        private static bool ContainsCJKKeyword(string name)
        {
            var n = name.ToLowerInvariant();
            return n.Contains("cjk") || n.Contains("chinese") ||
                   n.Contains("han") || n.Contains("heiti") ||
                   n.Contains("pingfang") || n.Contains("wenquanyi") ||
                   n.Contains("noto") || n.Contains("yahei") ||
                   n.Contains("simhei") || n.Contains("simsun");
        }
    }
}
