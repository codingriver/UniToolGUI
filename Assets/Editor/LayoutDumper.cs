// LayoutDumper.cs - 输出技能编辑器及运行时UIDocument布局信息
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SkillEditor.Editor
{
    public static class LayoutDumper
    {
        // ── 菜单入口 ─────────────────────────────────────────────

        [MenuItem("LayoutDumper/输出布局信息 - 所有EditorWindow")]
        public static void DumpAllEditorWindows()
        {
            var wins = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var sb = new StringBuilder();
            sb.AppendLine("[LayoutDump] ===== 所有 EditorWindow 布局 =====");
            sb.AppendLine($"共找到 {wins.Length} 个窗口");
            sb.AppendLine();
            foreach (var win in wins)
            {
                if (win.rootVisualElement == null) continue;
                sb.AppendLine($"--- {win.GetType().Name} | 标题: {win.titleContent.text} | 尺寸: {win.position.width:F0}x{win.position.height:F0} ---");
                DumpElement(win.rootVisualElement, sb, 0, 4);
                sb.AppendLine();
            }
            WriteAndLog(sb, "./layout/layout_dump_all_windows.txt");
        }

        [MenuItem("LayoutDumper/输出布局信息 - 运行时UIDocument")]
        public static void DumpRuntimeUIDocuments()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LayoutDump] 运行时UIDocument需要在Play模式下使用");
                return;
            }
            var docs = Object.FindObjectsOfType<UIDocument>();
            var sb = new StringBuilder();
            sb.AppendLine("[LayoutDump] ===== 运行时 UIDocument 布局 =====");
            sb.AppendLine($"共找到 {docs.Length} 个 UIDocument");
            sb.AppendLine();
            foreach (var doc in docs)
            {
                if (doc.rootVisualElement == null) continue;
                sb.AppendLine($"--- UIDocument: {doc.gameObject.name} | 面板排序: {doc.sortingOrder} ---");
                DumpElement(doc.rootVisualElement, sb, 0, 6);
                sb.AppendLine();
            }
            WriteAndLog(sb, "./layout/layout_dump_runtime.txt");
        }

        // ── 核心递归转储 ──────────────────────────────────────────

        private static readonly HashSet<string> _keyNames = new HashSet<string>
        {
            "skill-editor-root", "toolbar", "main-split-container",
            "main-content", "track-list-panel", "timeline-panel",
            "inspector-panel", "inspector-content", "inspector-header",
            "status-bar-placeholder", "ruler-placeholder", "phase-bar-placeholder",
            "track-content-placeholder", "zoombar-placeholder",
            "track-list-placeholder", "track-list-header",
        };

        private static void DumpElement(VisualElement el, StringBuilder sb, int depth, int maxDepth)
        {
            if (el == null || depth > maxDepth) return;

            var indent  = new string(' ', depth * 2);
            var layout  = el.layout;
            var rs      = el.resolvedStyle;
            var name    = string.IsNullOrEmpty(el.name) ? "(unnamed)" : el.name;
            var type    = el.GetType().Name;
            var classes = string.Join(" ", el.GetClasses());

            bool visible = rs.display != DisplayStyle.None
                        && rs.visibility != Visibility.Hidden;

            // 基础行
            sb.Append(indent);
            sb.Append($"[{type}] ");
            if (!string.IsNullOrEmpty(el.name)) sb.Append($"#{name} ");
            if (!string.IsNullOrEmpty(classes))  sb.Append($".{classes.Replace(" ", ".")} ");
            sb.AppendLine();

            // 布局数据
            sb.AppendLine($"{indent}  layout : pos=({layout.x:F0},{layout.y:F0}) size=({layout.width:F0} x {layout.height:F0})");
            sb.AppendLine($"{indent}  visible: {visible}  opacity={rs.opacity:F2}  display={rs.display}");

            // 关键元素输出详细flex信息
            if (_keyNames.Contains(el.name) || depth <= 2)
            {
                sb.AppendLine($"{indent}  flex   : dir={rs.flexDirection} grow={rs.flexGrow:F1} shrink={rs.flexShrink:F1} wrap={rs.flexWrap}");
                sb.AppendLine($"{indent}  align  : items={rs.alignItems} self={rs.alignSelf} justify={rs.justifyContent}");
                sb.AppendLine($"{indent}  margin : L={rs.marginLeft:F0} R={rs.marginRight:F0} T={rs.marginTop:F0} B={rs.marginBottom:F0}");
                sb.AppendLine($"{indent}  padding: L={rs.paddingLeft:F0} R={rs.paddingRight:F0} T={rs.paddingTop:F0} B={rs.paddingBottom:F0}");
                sb.AppendLine($"{indent}  border : L={rs.borderLeftWidth:F0} R={rs.borderRightWidth:F0} T={rs.borderTopWidth:F0} B={rs.borderBottomWidth:F0}");
                sb.AppendLine($"{indent}  minSize: w={rs.minWidth} h={rs.minHeight}  maxSize: w={rs.maxWidth} h={rs.maxHeight}");
                sb.AppendLine($"{indent}  bgColor: {rs.backgroundColor}");
            }

            int childCount = 0;
            foreach (var _ in el.Children()) childCount++;
            if (childCount > 0)
                sb.AppendLine($"{indent}  children: {childCount}");

            foreach (var child in el.Children())
                DumpElement(child, sb, depth + 1, maxDepth);
        }

        // ── 输出工具 ──────────────────────────────────────────────

        private static void WriteAndLog(StringBuilder sb, string filePath)
        {
            var output = sb.ToString();
            Debug.Log(output);
            try
            {
                string dirPath= Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                    Debug.Log($"[LayoutDumper] 创建目录: {dirPath}");
                }
                
                System.IO.File.WriteAllText(filePath, output, System.Text.Encoding.UTF8);
                Debug.Log($"[LayoutDumper] 已保存到: {filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LayoutDumper] 写入文件失败: {e.Message}");
            }
        }
    }
}
