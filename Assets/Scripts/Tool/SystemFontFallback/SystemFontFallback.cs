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
        Japanese, Korean, CJKAll, Arabic, Thai, AllLanguages,
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
        if (ts.defaultFontAsset == null)
        { ts.defaultFontAsset = primary; Log("[SFF] Set " + ps.name + " <- " + primary.name); }
        else
        {
            var ex = ts.defaultFontAsset;
            if (ex.fallbackFontAssetTable == null) ex.fallbackFontAssetTable = new List<FontAsset>();
            if (_primary != null && _primary != ex && !ex.fallbackFontAssetTable.Contains(_primary))
            { ex.fallbackFontAssetTable.Insert(0, _primary); Log("[SFF] Prepend " + ps.name + " <- " + _primary.name); }
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
        if (_primary != null) { DestroyFA(_primary); _primary = null; }
        foreach (var fa in _fallbacks) DestroyFA(fa);
        _fallbacks.Clear(); IsReady = false;
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
            case LanguagePreset.LatinOnly:          g.Add(PathsLatin); break;
            case LanguagePreset.ChineseSimplified:  g.Add(PathsCJK_SC); g.Add(PathsLatin); break;
            case LanguagePreset.ChineseTraditional: g.Add(PathsCJK_TC); g.Add(PathsLatin); break;
            case LanguagePreset.ChineseBoth:        g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsLatin); break;
            case LanguagePreset.Japanese:           g.Add(PathsJapanese); g.Add(PathsLatin); break;
            case LanguagePreset.Korean:             g.Add(PathsKorean); g.Add(PathsLatin); break;
            case LanguagePreset.CJKAll:             g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsJapanese); g.Add(PathsKorean); g.Add(PathsLatin); break;
            case LanguagePreset.Arabic:             g.Add(PathsArabic); g.Add(PathsLatin); break;
            case LanguagePreset.Thai:               g.Add(PathsThai); g.Add(PathsLatin); break;
            case LanguagePreset.AllLanguages:       g.Add(PathsCJK_SC); g.Add(PathsCJK_TC); g.Add(PathsJapanese); g.Add(PathsKorean); g.Add(PathsArabic); g.Add(PathsThai); g.Add(PathsLatin); break;
        }
        return g;
    }

    private FontAsset CreateFontAsset(string path, bool isPrimary)
    {
        Font font;
        try { font = new Font(path); }
        catch (System.Exception e) { Debug.LogWarning("[SFF] Font failed: " + path + " | " + e.Message); return null; }
        if (font == null) return null;
        FontAsset fa;
        try
        {
            fa = FontAsset.CreateFontAsset(font,
                samplingPointSize: isPrimary ? samplingPointSize : Mathf.Max(8, samplingPointSize - 8),
                atlasPadding: isPrimary ? atlasPadding : Mathf.Max(2, atlasPadding - 2),
                renderMode: ToGlyphRenderMode(renderModePreset),
                atlasWidth: (int)atlasSizePreset, atlasHeight: (int)atlasSizePreset,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
        }
        catch (System.Exception e) { Debug.LogWarning("[SFF] CreateFA failed: " + path + " | " + e.Message); return null; }
        if (fa == null) return null;
        fa.name = "[SysFont]" + Path.GetFileNameWithoutExtension(path);
        if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<FontAsset>();
        return fa;
    }

    private static string FindFirstExisting(string[] paths)
    {
        if (paths == null) return null;
        foreach (var p in paths) if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
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
        "/System/Library/Fonts/Osaka.ttf",
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
}
