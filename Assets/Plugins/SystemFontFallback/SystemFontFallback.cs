using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using AtlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode;

public class SystemFontFallback : MonoBehaviour
{
    // Language group. All options include Latin/English automatically.
    public enum LanguagePreset
    {
        LatinOnly,           // Latin/English only. Win: Segoe UI > Arial. Fastest.
        ChineseSimplified,   // Simplified Chinese + English (Recommended). Win: MS YaHei > SimSun.
        ChineseTraditional,  // Traditional Chinese + English. Win: MS JhengHei.
        ChineseBoth,         // Simplified + Traditional Chinese + English.
        Japanese,            // Japanese + English. Win: Yu Gothic > Meiryo.
        Korean,              // Korean + English. Win: Malgun Gothic > Gulim.
        CJKAll,              // All CJK + English. Broadest, slightly slower.
        Arabic,              // Arabic + English. Win: Arial > Tahoma.
        Thai,                // Thai + English. Win: Tahoma > Cordia New.
        AllLanguages,        // All languages. Slowest, for i18n apps.
    }

    // Atlas initial size. Dynamic mode auto-expands. Recommend Small_512.
    public enum AtlasSizePreset
    {
        Tiny_256    = 256,   // 256px. Very small, CJK expands frequently.
        Small_512   = 512,   // 512px. (Recommended) Best balance.
        Medium_1024 = 1024,  // 1024px. Fewer expansions for larger char sets.
        Large_2048  = 2048,  // 2048px. ~16MB VRAM.
        Huge_4096   = 4096,  // 4096px. ~64MB VRAM. Rarely needed.
    }

    // Glyph render mode. Affects sharpness vs generation speed.
    public enum RenderModePreset
    {
        SDFAA,  // (Recommended) Adaptive AA-SDF. Fastest. Same as SDF32 at 12-24px.
        SDF,    // Standard SDF. Medium quality and speed.
        SDF8,   // 8x supersampled. Sharper edges, good for icon fonts.
        SDF16,  // 16x supersampled. High quality headlines.
        SDF32,  // 32x supersampled. Highest quality, ~3x slower than SDFAA.
        Bitmap, // Rasterized. Fastest but aliased when scaled.
    }

    [Header("== Language ==")]
    [Tooltip("Language group. All options include English automatically.")]
    public LanguagePreset languagePreset = LanguagePreset.ChineseSimplified;
    [Header("== Primary Font Asset (optional) ==")]
    [Tooltip("Manual font asset. Leave empty to auto-create from system fonts.")]
    [SerializeField] private FontAsset primaryFontAsset;
    [Header("== Atlas Size ==")]
    [Tooltip("Initial atlas size. Recommend Small_512.")]
    public AtlasSizePreset atlasSizePreset = AtlasSizePreset.Small_512;
    [Header("== Render Mode ==")]
    [Tooltip("Quality vs speed. Recommend SDFAA for dynamic fonts.")]
    public RenderModePreset renderModePreset = RenderModePreset.SDFAA;
    [Header("== Sampling Point Size ==")]
    [Tooltip("Primary font sampling size (8-72). Smaller = faster load, less sharp at large sizes. Default 36.")]
    [Range(8, 72)]
    public int samplingPointSize = 36;
    [Header("== Atlas Padding ==")]
    [Tooltip("Glyph padding in atlas (2-16). Smaller = faster, more compact. Default 6.")]
    [Range(2, 16)]
    public int atlasPadding = 6;
    [Header("== Debug ==")]
    [Tooltip("Print font loading logs to Console.")]
    public bool enableLog = true;

    public static SystemFontFallback Instance { get; private set; }
    public bool IsReady { get; private set; }
    public event System.Action OnFontsReady;
    private FontAsset _primary;
    private readonly List<FontAsset> _fallbacks = new List<FontAsset>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); LoadAndInject();
    }

    private void LoadAndInject()
    {
        Log("[SFF] Start Language=" + languagePreset + " Atlas=" + atlasSizePreset + " Render=" + renderModePreset);
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
        Inject(); StartCoroutine(LateInject()); IsReady = true; OnFontsReady?.Invoke(); Log("[SFF] Done");
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
                atlasWidth: (int)atlasSizePreset,
                atlasHeight: (int)atlasSizePreset,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);
        }
        catch (System.Exception e) { Debug.LogWarning("[SFF] CreateFA failed: " + path + " | " + e.Message); return null; }
        if (fa == null) return null;
        fa.name = "[SysFont]" + Path.GetFileNameWithoutExtension(path);
        if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<FontAsset>();
        return fa;
    }

    private void Inject()
    {
        var primary = primaryFontAsset ?? _primary;
        if (primary == null) return;
        if (primary.fallbackFontAssetTable == null) primary.fallbackFontAssetTable = new List<FontAsset>();
        foreach (var fb in _fallbacks)
            if (fb != null && fb != primary && !primary.fallbackFontAssetTable.Contains(fb))
                primary.fallbackFontAssetTable.Add(fb);
        var seen = new HashSet<PanelSettings>();
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var ps = doc?.panelSettings;
            if (ps == null || seen.Contains(ps)) continue;
            seen.Add(ps);
            var ts = ps.textSettings;
            if (ts == null) { Debug.LogWarning("[SFF] textSettings null: " + ps.name); continue; }
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
    }

    private System.Collections.IEnumerator LateInject() { yield return null; Inject(); }

    private static string FindFirstExisting(string[] paths)
    {
        if (paths == null) return null;
        foreach (var p in paths) if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) return p;
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
        var seen = new HashSet<PanelSettings>();
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var ps = doc?.panelSettings;
            if (ps == null || seen.Contains(ps)) continue;
            seen.Add(ps);
            var def = ps.textSettings?.defaultFontAsset;
            sb.AppendLine("[" + ps.name + "] default=" + (def != null ? def.name : "NULL") + " fallbacks=" + (def?.fallbackFontAssetTable?.Count ?? 0));
        }
        Debug.Log(sb.ToString());
    }

    #region Font Paths
    private static readonly string[] PathsLatin =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\segoeui.ttf",
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\tahoma.ttf",
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
        @"C:\Windows\Fonts\msyh.ttc",
        @"C:\Windows\Fonts\msyhbd.ttc",
        @"C:\Windows\Fonts\simsun.ttc",
        @"C:\Windows\Fonts\simhei.ttf",
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        "/System/Library/Fonts/PingFang.ttc",
        "/System/Library/Fonts/STHeiti Medium.ttc",
#elif UNITY_ANDROID
        "/system/fonts/NotoSansCJK-Regular.ttc",
#elif UNITY_IOS
        "/System/Library/Fonts/PingFang.ttc",
#endif
    };
    private static readonly string[] PathsCJK_TC =
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        @"C:\Windows\Fonts\msjh.ttc",
        @"C:\Windows\Fonts\mingliu.ttc",
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
        @"C:\Windows\Fonts\YuGothM.ttc",
        @"C:\Windows\Fonts\meiryo.ttc",
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
        @"C:\Windows\Fonts\malgun.ttf",
        @"C:\Windows\Fonts\gulim.ttc",
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
        @"C:\Windows\Fonts\arial.ttf",
        @"C:\Windows\Fonts\tahoma.ttf",
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
        @"C:\Windows\Fonts\tahoma.ttf",
        @"C:\Windows\Fonts\cordia.ttf",
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
