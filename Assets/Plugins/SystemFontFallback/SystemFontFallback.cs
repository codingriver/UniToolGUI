using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using AtlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode;

/// <summary>
/// Automatically loads system fonts and injects into UIDocument PanelSettings.
///
/// UIDocument vs Canvas:
///   UIDocument = UIToolkit's equivalent of UGUI Canvas.
///   Multiple UIDocuments sharing the same PanelSettings share fonts automatically.
///   You do NOT need to merge all UI into one UIDocument.
///   Font injection targets PanelSettings, not UIDocument directly.
///
/// Editor support:
///   [ExecuteAlways] makes Awake() run in Edit Mode so fonts appear in Editor
///   without entering Play Mode.
///
/// External UIDocument support:
///   Assign documents to targetDocuments list in Inspector, or call
///   RegisterDocument(doc) at runtime for dynamically created panels.
///   Leave targetDocuments empty to auto-discover all scene UIDocuments.
/// </summary>
[ExecuteAlways]
public class SystemFontFallback : MonoBehaviour
{
    public enum LanguagePreset
    {
        LatinOnly, ChineseSimplified, ChineseTraditional, ChineseBoth,
        Japanese, Korean, CJKAll, Arabic, Thai, Greek, Cyrillic, Indic, AllLanguages,
    }
    public enum AtlasSizePreset
    {
        Tiny_256 = 256, Small_512 = 512, Medium_1024 = 1024,
        Large_2048 = 2048, Huge_4096 = 4096,
    }
    public enum RenderModePreset { SDFAA, SDF, SDF8, SDF16, SDF32, Bitmap }

    [Header("== Language ==")]
    public LanguagePreset languagePreset = LanguagePreset.ChineseSimplified;

    [Header("== Primary Font Asset (optional) ==")]
    [Tooltip("Leave empty to auto-create from system fonts.")]
    [SerializeField] private FontAsset primaryFontAsset;

    [Header("== Atlas Size ==")]
    public AtlasSizePreset atlasSizePreset = AtlasSizePreset.Small_512;

    [Header("== Render Mode ==")]
    public RenderModePreset renderModePreset = RenderModePreset.SDFAA;

    [Header("== Sampling Point Size ==")]
    [Range(8, 72)] public int samplingPointSize = 36;

    [Header("== Atlas Padding ==")]
    [Range(2, 16)] public int atlasPadding = 6;

    [Header("== PanelSettings Override ==")]
    [Tooltip(
        "Optional: directly specify PanelSettings to inject fonts into.\n" +
        "This is the RECOMMENDED approach — set once, covers ALL UIDocuments\n" +
        "that reference the same PanelSettings asset.\n\n" +
        "Leave EMPTY to auto-discover PanelSettings via scene UIDocuments.\n" +
        "Call RegisterDocument(doc) at runtime for dynamic panels.")]
    [SerializeField] private List<PanelSettings> targetPanelSettings = new List<PanelSettings>();

    [Header("== UIDocument Override (optional) ==")]
    [Tooltip(
        "Fallback: if targetPanelSettings is empty, inject via these UIDocuments.\n" +
        "Leave both lists EMPTY to auto-discover all scene UIDocuments.")]
    [SerializeField] private List<UIDocument> targetDocuments = new List<UIDocument>();

    [Header("== Debug ==")]
    public bool enableLog = true;

    [Header("== Performance Debug ==")]
    [Tooltip(
        "开启后将打印字体纹理创建/销毁、字符渲染、字体查找等详细性能日志。\n" +
        "日志格式: [FontFallback][Perf] {时间戳ms} | {事件}\n" +
        "关闭时零开销，建议仅在排查性能问题时开启。")]
    public bool enablePerformanceLog = false;

    public static SystemFontFallback Instance { get; private set; }
    public bool IsReady { get; private set; }
    public event System.Action OnFontsReady;

    private FontAsset _primary;
    private readonly List<FontAsset> _fallbacks = new List<FontAsset>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        // Edit Mode: no singleton restriction, allow re-init on every Awake
#else
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
#endif
        LoadAndInject();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>Register a UIDocument and immediately inject fonts. Use for runtime-spawned panels.</summary>
    public void RegisterDocument(UIDocument doc)
    {
        if (doc == null) return;
        if (!targetDocuments.Contains(doc)) targetDocuments.Add(doc);
        if (IsReady) InjectIntoDocument(doc, primaryFontAsset ?? _primary);
    }

    /// <summary>Register a PanelSettings directly and immediately inject fonts.</summary>
    public void RegisterPanelSettings(PanelSettings ps)
    {
        if (ps == null) return;
        if (!targetPanelSettings.Contains(ps)) targetPanelSettings.Add(ps);
        if (IsReady) InjectIntoPanelSettings(ps, primaryFontAsset ?? _primary);
    }

    /// <summary>Unregister a UIDocument. Does not undo font injection.</summary>
    public void UnregisterDocument(UIDocument doc) => targetDocuments.Remove(doc);

    /// <summary>Unregister a PanelSettings. Does not undo font injection.</summary>
    public void UnregisterPanelSettings(PanelSettings ps) => targetPanelSettings.Remove(ps);

    /// <summary>Force reload fonts (useful after Inspector changes in Edit Mode).</summary>
    [ContextMenu("Reload Fonts")]
    public void ReloadFonts() => LoadAndInject();

    // -----------------------------------------------------------------------
    // Core
    // -----------------------------------------------------------------------

    private void LoadAndInject()
    {
        Log("[SFF] Start Language=" + languagePreset + " Atlas=" + atlasSizePreset + " Render=" + renderModePreset);
        CleanupDynamicAssets();
        bool isFirst = true;
        foreach (var paths in BuildPathGroups())
        {
            string path = FindFirstExisting(paths);
            if (string.IsNullOrEmpty(path)) continue;
            var fa = CreateFontAsset(path, isFirst);
            if (fa == null) continue;
            if (isFirst) { _primary = fa; isFirst = false; Log("[SFF] Primary: " + fa.name); }
            else { _fallbacks.Add(fa); Log("[SFF] Fallback: " + fa.name); }
        }
        if (_primary == null && primaryFontAsset == null) { Debug.LogWarning("[SFF] No font found!"); return; }
        Inject();
#if UNITY_EDITOR
        if (Application.isPlaying) StartCoroutine(LateInject());
        // Edit Mode: coroutines unavailable, skip LateInject
#else
        StartCoroutine(LateInject());
#endif
        IsReady = true; OnFontsReady?.Invoke(); Log("[SFF] Done");
    }

    /// <summary>
    /// Resolve PanelSettings to inject into, with three-level priority:
    /// 1. targetPanelSettings (explicit, recommended)
    /// 2. targetDocuments PanelSettings (semi-explicit)
    /// 3. Auto-discover all scene UIDocuments PanelSettings (fallback)
    /// </summary>
    private IEnumerable<PanelSettings> ResolvePanelSettings()
    {
        // Priority 1: explicit PanelSettings list
        if (targetPanelSettings != null && targetPanelSettings.Count > 0)
        {
            foreach (var ps in targetPanelSettings)
                if (ps != null) yield return ps;
            yield break;
        }
        // Priority 2: explicit UIDocument list
        if (targetDocuments != null && targetDocuments.Count > 0)
        {
            var seen = new HashSet<PanelSettings>();
            foreach (var d in targetDocuments)
            {
                var ps = d?.panelSettings;
                if (ps != null && seen.Add(ps)) yield return ps;
            }
            yield break;
        }
        // Priority 3: auto-discover all UIDocuments in scene
        {
            var seen = new HashSet<PanelSettings>();
            foreach (var d in FindObjectsOfType<UIDocument>())
            {
                var ps = d?.panelSettings;
                if (ps != null && seen.Add(ps)) yield return ps;
            }
        }
    }

    // Keep for backward-compat / RegisterDocument runtime API
    private IEnumerable<UIDocument> ResolveDocuments()
    {
        if (targetDocuments != null && targetDocuments.Count > 0)
        { foreach (var d in targetDocuments) if (d != null) yield return d; }
        else
        { foreach (var d in FindObjectsOfType<UIDocument>()) if (d != null) yield return d; }
    }

    private void Inject()
    {
        var primary = primaryFontAsset ?? _primary;
        if (primary == null) return;
        if (primary.fallbackFontAssetTable == null)
            primary.fallbackFontAssetTable = new List<FontAsset>();
        foreach (var fb in _fallbacks)
            if (fb != null && fb != primary && !primary.fallbackFontAssetTable.Contains(fb))
                primary.fallbackFontAssetTable.Add(fb);
        // Use three-level priority: PanelSettings > UIDocument > auto-discover
        foreach (var ps in ResolvePanelSettings())
            InjectIntoPanelSettings(ps, primary);
    }

    private void InjectIntoPanelSettings(PanelSettings ps, FontAsset primary)
    {
        if (ps == null) return;
        var ts = ps.textSettings;
        if (ts == null) { Debug.LogWarning("[SFF] textSettings null: " + ps.name); return; }

        var existingDefault = ts.defaultFontAsset;
        ts.defaultFontAsset = primary;
        Log("[SFF] Set " + ps.name + " <- " + primary.name);

        if (existingDefault != null && existingDefault != primary)
        {
            if (primary.fallbackFontAssetTable == null)
                primary.fallbackFontAssetTable = new List<FontAsset>();
            if (!primary.fallbackFontAssetTable.Contains(existingDefault))
            {
                primary.fallbackFontAssetTable.Add(existingDefault);
                Log("[SFF] Append existing default as fallback: " + existingDefault.name);
            }
        }
    }

    // Used by RegisterDocument() runtime API
    private void InjectIntoDocument(UIDocument doc, FontAsset primary)
    {
        var ps = doc?.panelSettings;
        if (ps == null) return;
        InjectIntoPanelSettings(ps, primary);
    }

    private IEnumerator LateInject() { yield return null; Inject(); }

    private void CleanupDynamicAssets()
    {
        if (_primary != null)
        {
            PerfLog("销毁 Primary FontAsset | name=" + _primary.name);
            LogDestroyTextures(_primary);
            DestroyFA(_primary); _primary = null;
        }
        foreach (var fa in _fallbacks)
        {
            if (fa != null)
            {
                PerfLog("销毁 Fallback FontAsset | name=" + fa.name);
                LogDestroyTextures(fa);
                DestroyFA(fa);
            }
        }
        _fallbacks.Clear(); IsReady = false;
    }

    /// <summary>销毁 FontAsset 前打印每张纹理的销毁日志</summary>
    private void LogDestroyTextures(FontAsset fa)
    {
        if (!enablePerformanceLog || fa == null || fa.atlasTextures == null) return;
        int total = fa.atlasTextures.Length;
        for (int i = 0; i < total; i++)
        {
            var tex = fa.atlasTextures[i];
            if (tex != null)
                PerfLog(string.Format("  纹理销毁 [{0}/{1}] | fontAssetName={2} | 尺寸={3}x{4} | instanceID={5} | 销毁后剩余={6}",
                    i, total - 1, fa.name, tex.width, tex.height, tex.GetInstanceID(), total - i - 1));
        }
    }

    private static void DestroyFA(FontAsset fa)
    {
        if (fa == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) DestroyImmediate(fa); else Destroy(fa);
#else
        Destroy(fa);
#endif
    }

    private List<string[]> BuildPathGroups()
    {
        var g = new List<string[]>();
        switch (languagePreset)
        {
            case LanguagePreset.LatinOnly:          g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.ChineseSimplified:  g.Add(PathsCJK_SC); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.ChineseTraditional: g.Add(PathsCJK_TC); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.ChineseBoth:        g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Japanese:           g.Add(PathsJapanese); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Korean:             g.Add(PathsKorean); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.CJKAll:             g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsJapanese); g.Add(PathsKorean); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Arabic:             g.Add(PathsArabic); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Thai:               g.Add(PathsThai); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Greek:              g.Add(PathsGreek); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Cyrillic:           g.Add(PathsCyrillic); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.Indic:              g.Add(PathsIndic); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); break;
            case LanguagePreset.AllLanguages:       g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsJapanese); g.Add(PathsKorean); g.Add(PathsArabic); g.Add(PathsThai); g.Add(PathsLatin); g.Add(PathsSymbols); g.Add(PathsEmoji); g.Add(PathsGreek); g.Add(PathsCyrillic); g.Add(PathsIndic); break;
        }
        return g;
    }

    private FontAsset CreateFontAsset(string path, bool isPrimary)
    {
        PerfLog(string.Format("CreateFontAsset 开始 | isPrimary={0} | path={1}", isPrimary, path));
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

        Font font;
        try { font = new Font(path); }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SFF] Font failed: " + path + " | " + e.Message);
            return null;
        }
        if (font == null) { PerfLog("Font 对象为 null，跳过 | path=" + path); return null; }

        long tFont = System.Diagnostics.Stopwatch.GetTimestamp();
        PerfLog(string.Format("Font 对象创建完成 | 耗时={0:F2}ms | fontName={1} | path={2}",
            TicksToMs(tFont - t0), font.name, path));

        FontAsset fa;
        int atlasW = (int)atlasSizePreset, atlasH = (int)atlasSizePreset;
        int ptSize  = isPrimary ? samplingPointSize : Mathf.Max(8, samplingPointSize - 8);
        int padding = isPrimary ? atlasPadding      : Mathf.Max(2, atlasPadding - 2);
        PerfLog(string.Format("即将创建 FontAsset | fontName={0} | pointSize={1} | padding={2} | atlas={3}x{4} | renderMode={5} | multiAtlas=true",
            font.name, ptSize, padding, atlasW, atlasH, renderModePreset));

        long tFAStart = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            fa = FontAsset.CreateFontAsset(font,
                samplingPointSize: ptSize,
                atlasPadding: padding,
                renderMode: ToGlyphRenderMode(renderModePreset),
                atlasWidth: atlasW, atlasHeight: atlasH,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[SFF] CreateFA failed: " + path + " | " + e.Message);
            return null;
        }
        long tFAEnd = System.Diagnostics.Stopwatch.GetTimestamp();

        if (fa == null) { PerfLog("FontAsset.CreateFontAsset 返回 null | path=" + path); return null; }

        fa.name = "[SysFont]" + Path.GetFileNameWithoutExtension(path);
        if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<FontAsset>();

        // ── 纹理创建日志 ──
        int texCount = fa.atlasTextures != null ? fa.atlasTextures.Length : 0;
        PerfLog(string.Format("FontAsset 创建完成 | 耗时={0:F2}ms | fontAssetName={1} | 初始纹理数量={2} | atlas={3}x{4}",
            TicksToMs(tFAEnd - tFAStart), fa.name, texCount, atlasW, atlasH));
        if (fa.atlasTextures != null)
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                var tex = fa.atlasTextures[i];
                if (tex != null)
                    PerfLog(string.Format("  纹理[{0}] 已创建 | fontAssetName={1} | 尺寸={2}x{3} | instanceID={4}",
                        i, fa.name, tex.width, tex.height, tex.GetInstanceID()));
            }

        PerfLog(string.Format("CreateFontAsset 完成 | 总耗时={0:F2}ms | fontAssetName={1}",
            TicksToMs(tFAEnd - t0), fa.name));
        return fa;
    }

    private string FindFirstExisting(string[] paths)
    {
        if (paths == null) return null;
        foreach (var p in paths)
        {
            if (string.IsNullOrEmpty(p)) continue;
            bool exists = File.Exists(p);
            // ── 字体查找日志：找到或找不到都打印 ──
            if (exists)
                PerfLog(string.Format("字体路径查找 | 找到 | path={0}", p));
            else
                PerfLog(string.Format("字体路径查找 | 未找到 | path={0}", p));
            if (exists) return p;
        }
        return null;
    }

    private static GlyphRenderMode ToGlyphRenderMode(RenderModePreset p)
    {
        switch (p)
        {
            case RenderModePreset.SDF:    return GlyphRenderMode.SDF;
            case RenderModePreset.SDF8:   return GlyphRenderMode.SDF8;
            case RenderModePreset.SDF16:  return GlyphRenderMode.SDF16;
            case RenderModePreset.SDF32:  return GlyphRenderMode.SDF32;
            case RenderModePreset.Bitmap: return GlyphRenderMode.SMOOTH;
            default:                      return GlyphRenderMode.SDFAA;
        }
    }

    private void Log(string msg) { if (enableLog) Debug.Log(msg); }

    /// <summary>
    /// 性能检查日志。仅当 enablePerformanceLog=true 时输出。
    /// 格式: [FontFallback][Perf] {时间戳ms} | {msg}
    /// 在 Console 中使用关键字 "[FontFallback][Perf]" 过滤所有性能日志。
    /// </summary>
    private void PerfLog(string msg)
    {
        if (!enablePerformanceLog) return;
        // Time.realtimeSinceStartup 精度约 1ms，适合粗粒度计时
        // 对于微秒级精度请结合 Stopwatch.GetTimestamp() 在调用处计算差值
        Debug.Log(string.Format("[FontFallback][Perf] {0:F1}ms | {1}",
            Time.realtimeSinceStartup * 1000f, msg));
    }

    /// <summary>将 Stopwatch ticks 转换为毫秒（double）</summary>
    private static double TicksToMs(long ticks)
        => (double)ticks / System.Diagnostics.Stopwatch.Frequency * 1000.0;

    /// <summary>
    /// 字体纹理渲染文字日志。
    /// 在需要逐字符追踪渲染耗时时，从外部（或子类）调用此方法。
    /// 例: SystemFontFallback.Instance?.LogGlyphRender('中', fa);
    /// </summary>
    public void LogGlyphRender(char character, FontAsset fa)
    {
        if (!enablePerformanceLog) return;
        int unicode = character;
        PerfLog(string.Format("字符渲染 | char='{0}' | unicode=U+{1:X4} | fontAsset={2}",
            character, unicode, fa != null ? fa.name : "null"));
    }

    /// <summary>
    /// 字体是否支持某字符的查找日志（在具有字体支持检查逻辑处调用）。
    /// </summary>
    public void LogGlyphSupport(char character, string fontName, bool supported)
    {
        if (!enablePerformanceLog) return;
        int unicode = character;
        if (supported)
            PerfLog(string.Format("字体支持查找 | 找到 | char='{0}' U+{1:X4} | fontName={2}",
                character, unicode, fontName));
        else
            PerfLog(string.Format("字体支持查找 | 未找到 | char='{0}' U+{1:X4} | fontName={2}",
                character, unicode, fontName));
    }

    /// <summary>
    /// 字体纹理动态扩充（多纹理）日志。
    /// 当 FontAsset 动态增加新 Atlas 纹理时调用（需在自定义 FontAsset 回调中触发）。
    /// </summary>
    public void LogAtlasAdded(FontAsset fa, Texture2D newTex, int newIndex)
    {
        if (!enablePerformanceLog) return;
        int total = fa?.atlasTextures?.Length ?? newIndex + 1;
        PerfLog(string.Format("多纹理扩充 | 第{0}张纹理创建 | fontAssetName={1} | 当前纹理总数={2} | 尺寸={3}x{4} | instanceID={5}",
            newIndex, fa != null ? fa.name : "null", total,
            newTex != null ? newTex.width : 0,
            newTex != null ? newTex.height : 0,
            newTex != null ? newTex.GetInstanceID() : 0));
    }

    [ContextMenu("Run Diagnostics")]
    public void RunDiagnostics()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SFF] Language=" + languagePreset + " Atlas=" + atlasSizePreset + " Render=" + renderModePreset);
        sb.AppendLine("IsReady=" + IsReady + " Primary=" + (_primary != null ? _primary.name : "NULL"));
        foreach (var f in _fallbacks) sb.AppendLine("  Fallback: " + (f != null ? f.name : "NULL"));
        sb.AppendLine("TargetDocs: " + (targetDocuments.Count > 0 ? targetDocuments.Count + " explicit" : "auto-discover"));
        var seen = new HashSet<PanelSettings>();
        foreach (var ps in ResolvePanelSettings())
        {
            if (!seen.Add(ps)) continue;
            var def = ps.textSettings?.defaultFontAsset;
            sb.AppendLine("[" + ps.name + "] default=" + (def != null ? def.name : "NULL") +
                          " fallbacks=" + (def?.fallbackFontAssetTable?.Count ?? 0));
        }
        Debug.Log(sb.ToString());
    }

    // Emoji/Symbol font fallback to cover characters not present in primary/CJK fonts
    private static readonly string[] PathsEmoji =
    {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\seguiemj.ttf",
    #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        @"/System/Library/Fonts/Apple Color Emoji.ttc",
    #elif UNITY_ANDROID
        @"/system/fonts/NotoColorEmoji.ttf",
    #elif UNITY_IOS
        @"/System/Library/Fonts/Apple Color Emoji.ttc",
    #endif
    };

    private static readonly string[] PathsSymbols =
    {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\seguisym.ttf", @"C:\Windows\Fonts\symbol.ttf", @"C:\Windows\Fonts\arialuni.ttf",
    #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        @"/System/Library/Fonts/Apple Symbols.ttf", @"/System/Library/Fonts/Supplemental/Symbol.ttf", @"/System/Library/Fonts/Supplemental/Arial Unicode.ttf", @"/System/Library/Fonts/Supplemental/Helvetica.ttc",
    #elif UNITY_ANDROID
        @"/system/fonts/NotoSansSymbols-Regular.ttf", @"/system/fonts/NotoSans-Regular.ttf",
    #elif UNITY_IOS
        @"/System/Library/Fonts/Apple Symbols.ttf", @"/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
    #endif
    };

    #region Font Paths
    private static readonly string[] PathsLatin =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\segoeui.ttf", @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\tahoma.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/Helvetica.ttc",
#elif UNITY_ANDROID
        "/system/fonts/Roboto-Regular.ttf",
#elif UNITY_IOS
        "/System/Library/Fonts/Helvetica.ttc",
#endif
    };
    private static readonly string[] PathsCJK_SC =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\msyh.ttc", @"C:\Windows\Fonts\msyhbd.ttc",
        @"C:\Windows\Fonts\simsun.ttc", @"C:\Windows\Fonts\simhei.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/PingFang.ttc", "/System/Library/Fonts/STHeiti Medium.ttc",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansCJK-Regular.ttc",
#elif UNITY_IOS
        "/System/Library/Fonts/PingFang.ttc",
#endif
    };
    private static readonly string[] PathsCJK_TC =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\msjh.ttc", @"C:\Windows\Fonts\mingliu.ttc",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/PingFang.ttc",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansCJK-Regular.ttc",
#elif UNITY_IOS
        "/System/Library/Fonts/PingFang.ttc",
#endif
    };
    private static readonly string[] PathsJapanese =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\YuGothM.ttc", @"C:\Windows\Fonts\meiryo.ttc",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/ヒラギノ角ゴシック W3.ttc", "/System/Library/Fonts/Supplemental/Hiragino Sans GB.ttc", "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansCJK-Regular.ttc",
#elif UNITY_IOS
        "/System/Library/Fonts/Osaka.ttf",
#endif
    };
    private static readonly string[] PathsKorean =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\malgun.ttf", @"C:\Windows\Fonts\gulim.ttc",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/AppleSDGothicNeo.ttc",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansCJK-Regular.ttc",
#elif UNITY_IOS
        "/System/Library/Fonts/AppleSDGothicNeo.ttc",
#endif
    };
    private static readonly string[] PathsArabic =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\arial.ttf", @"C:\Windows\Fonts\tahoma.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/GeezaPro.ttc",
#elif UNITY_ANDROID
        "/system/fonts/NotoNaskhArabic-Regular.ttf",
#elif UNITY_IOS
        "/System/Library/Fonts/GeezaPro.ttc",
#endif
    };
    private static readonly string[] PathsThai =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\tahoma.ttf", @"C:\Windows\Fonts\cordia.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/Thonburi.ttf",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansThai-Regular.ttf",
#elif UNITY_IOS
        "/System/Library/Fonts/Thonburi.ttf",
#endif
    };
    #endregion

    // Additional global-script coverage: Cyrillic, Greek, Indic (Devanagari, etc.)
    // These paths are best-effort and may not exist on all platforms. We rely on File.Exists checks.
    private static readonly string[] PathsGreek =
    {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\times.ttf", @"C:\Windows\Fonts\aria.ttf", // common generic fonts with Greek glyphs
    #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        @"/System/Library/Fonts/Times New Roman.ttf", @"/Library/Fonts/Arial.ttf",
    #elif UNITY_ANDROID
        @"/system/fonts/NotoSans-Regular.ttf",
    #elif UNITY_IOS
        @"/System/Library/Fonts/Times New Roman.ttf",
    #endif
    };
    private static readonly string[] PathsCyrillic =
    {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\times.ttf", @"C:\Windows\Fonts\arial.ttf",
    #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        @"/System/Library/Fonts/Times New Roman.ttf", @"/Library/Fonts/Arial.ttf",
    #elif UNITY_ANDROID
        @"/system/fonts/NotoSans-Regular.ttf",
    #elif UNITY_IOS
        @"/System/Library/Fonts/Times New Roman.ttf",
    #endif
    };
    private static readonly string[] PathsIndic =
    {
    #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\MSMINCHO.TTF", @"C:\Windows\Fonts\Latha.ttf",
    #elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        @"/System/Library/Fonts/Times New Roman.ttf", @"/Library/Fonts/Arial Unicode.ttf",
    #elif UNITY_ANDROID
        @"/system/fonts/NotoSansDevanagari-Regular.ttf", @"/system/fonts/NotoSans-Regular.ttf",
    #elif UNITY_IOS
        @"/System/Library/Fonts/Devanagari.ttf",
    #endif
    };
}
