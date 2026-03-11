using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using AtlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode;

// FIX-1: Dynamic mode, skip unreliable pre-validation
// FIX-2: Ensure defaultFontAsset != null before injection
// FIX-3: OnFontsReady event for business layer
// FIX-4: FontAsset(SDF) only, no Legacy Font
// FIX-5: Full staged diagnostic logs
// FIX-6: Full null guards on fallbackFontAssetTable
public class SystemFontFallback : MonoBehaviour
{
    public static SystemFontFallback Instance { get; private set; }

    [Header("Primary font asset (empty = auto-detect)")]
    [SerializeField] private FontAsset primaryFontAsset;
    [Header("Max fonts per group")]
    [SerializeField] private int maxFontsPerGroup = 2;
    [Header("CJK atlas size (px)")]
    [SerializeField] private int cjkAtlasSize = 4096;
    [Header("Other language atlas size (px)")]
    [SerializeField] private int otherAtlasSize = 2048;
    [Header("Log all system fonts (debug only)")]
    [SerializeField] private bool logAllSystemFonts = false;

    private readonly Dictionary<SystemFontHelper.FontGroup, List<FontAsset>>
        _loaded = new Dictionary<SystemFontHelper.FontGroup, List<FontAsset>>();

    public bool IsReady { get; private set; }
    public event System.Action OnFontsReady;
    public event System.Action<SystemFontHelper.FontGroup> OnGroupFailed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Start()
    {
        Debug.Log("[SFF] ===== Start =====");
        if (logAllSystemFonts) SystemFontHelper.LogAllInstalledFonts();
        DiagnoseCandidatePaths();
        yield return null;
        DiagnoseTextSettings("[Before]");
        yield return StartCoroutine(LoadGroup(SystemFontHelper.FontGroup.CJK));
        yield return null;
        yield return StartCoroutine(LoadGroup(SystemFontHelper.FontGroup.Latin));
        yield return null;
        yield return StartCoroutine(LoadGroup(SystemFontHelper.FontGroup.Arabic));
        yield return null;
        yield return StartCoroutine(LoadGroup(SystemFontHelper.FontGroup.Thai));
        yield return null;
        yield return StartCoroutine(LoadGroup(SystemFontHelper.FontGroup.Fallback));
        yield return null;
        InjectFallbacks();
        DiagnoseTextSettings("[After]");
        IsReady = true;
        OnFontsReady?.Invoke();
        Debug.Log("[SFF] ===== Done IsReady=true =====");
    }

    private IEnumerator LoadGroup(SystemFontHelper.FontGroup group)
    {
        Debug.Log("[SFF] -- Group: " + group + " --");
        var paths = SystemFontHelper.GetAllExistingPaths(group);
        if (paths.Count == 0)
        {
            Debug.LogWarning("[SFF] [" + group + "] No font files found");
            OnGroupFailed?.Invoke(group);
            yield break;
        }
        for (int i = 0; i < paths.Count; i++)
            Debug.Log("[SFF] [" + group + "] Candidate[" + i + "]: " + paths[i]);
        var list = new List<FontAsset>();
        int tried = 0;
        foreach (var path in paths)
        {
            if (tried >= maxFontsPerGroup) { Debug.Log("[SFF] Limit reached"); break; }
            tried++;
            var fa = TryCreateFontAsset(path, group);
            if (fa != null) list.Add(fa);
            yield return null;
        }
        if (list.Count > 0) { _loaded[group] = list; Debug.Log("[SFF] [" + group + "] OK: " + list.Count); }
        else { Debug.LogWarning("[SFF] [" + group + "] All failed"); OnGroupFailed?.Invoke(group); }
    }

    private FontAsset TryCreateFontAsset(string path, SystemFontHelper.FontGroup group)
    {
        Debug.Log("[SFF]   Create: " + path);
        Font font;
        try { font = new Font(path); }
        catch (System.Exception e) { Debug.LogWarning("[SFF]   Font() ex: " + e.Message); return null; }
        if (font == null) { Debug.LogWarning("[SFF]   Font() null"); return null; }
        Debug.Log("[SFF]   Font OK: " + font.name + " dynamic=" + font.dynamic);
        bool isCJK = group == SystemFontHelper.FontGroup.CJK;
        int sz = isCJK ? cjkAtlasSize : otherAtlasSize;
        FontAsset fa;
        try
        {
            fa = FontAsset.CreateFontAsset(font,
                samplingPointSize: isCJK ? 42 : 36, atlasPadding: isCJK ? 9 : 6,
                renderMode: GlyphRenderMode.SDF32, atlasWidth: sz, atlasHeight: sz,
                atlasPopulationMode: AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        }
        catch (System.Exception e) { Debug.LogWarning("[SFF]   CreateFA ex: " + e.Message); return null; }
        if (fa == null) { Debug.LogWarning("[SFF]   CreateFA null"); return null; }
        fa.name = "[SysFont]" + group + "_" + Path.GetFileNameWithoutExtension(path);
        // FIX: CreateFontAsset may return null fallbackFontAssetTable, initialize it
        if (fa.fallbackFontAssetTable == null)
            fa.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset>();
        Debug.Log("[SFF]   FA OK: " + fa.name + " mode=" + fa.atlasPopulationMode +
                  " chars=" + fa.characterTable.Count +
                  " atlases=" + (fa.atlasTextures == null ? 0 : fa.atlasTextures.Length));
        return fa;
    }

    private void InjectFallbacks()
    {
        Debug.Log("[SFF] -- Inject --");
        var target = primaryFontAsset ?? GetDefaultFontFromPanelSettings();
        if (target == null)
        {
            var first = GetFirstLoaded();
            if (first == null)
            {
                Debug.LogError("[SFF] No primary font and all system fonts failed.\n" +
                    "Check: 1) paths exist 2) TextSettings bound to PanelSettings 3) enable logAllSystemFonts");
                return;
            }
            SetDefaultFontToAllPanels(first);
            target = first;
            Debug.Log("[SFF] No primary -> set " + first.name + " as default");
        }
        else
        {
            Debug.Log("[SFF] Target: " + target.name);
            EnsureDefaultFontSet(target);
        }
        if (target.fallbackFontAssetTable == null)
        {
            Debug.LogWarning("[SFF] target.fallbackFontAssetTable is null, initializing: " + target.name);
            target.fallbackFontAssetTable = new System.Collections.Generic.List<FontAsset>();
        }
        var order = new[]
        {
            SystemFontHelper.FontGroup.CJK, SystemFontHelper.FontGroup.Latin,
            SystemFontHelper.FontGroup.Arabic, SystemFontHelper.FontGroup.Thai,
            SystemFontHelper.FontGroup.Fallback,
        };
        foreach (var group in order)
        {
            if (!_loaded.TryGetValue(group, out var fonts)) continue;
            foreach (var fa in fonts)
            {
                if (fa == null || fa == target) continue;
                if (!target.fallbackFontAssetTable.Contains(fa))
                {
                    target.fallbackFontAssetTable.Add(fa);
                    Debug.Log("[SFF] += [" + group + "] " + fa.name);
                }
            }
        }
        DiagnoseFallbackChain(target);
    }

    private FontAsset GetDefaultFontFromPanelSettings()
    {
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var fa = doc.panelSettings?.textSettings?.defaultFontAsset;
            if (fa != null) return fa;
        }
        return null;
    }

    private void EnsureDefaultFontSet(FontAsset fa)
    {
        var seen = new HashSet<PanelSettings>();
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var ps = doc.panelSettings;
            if (ps == null || seen.Contains(ps)) continue;
            seen.Add(ps);
            var ts = ps.textSettings;
            if (ts == null) continue;
            if (ts.defaultFontAsset == null)
            {
                ts.defaultFontAsset = fa;
                Debug.Log("[SFF] TextSettings(" + ps.name + ").defaultFont <- " + fa.name);
            }
        }
    }

    private void SetDefaultFontToAllPanels(FontAsset fa)
    {
        var seen = new HashSet<PanelSettings>();
        foreach (var doc in FindObjectsOfType<UIDocument>())
        {
            var ps = doc.panelSettings;
            if (ps == null || seen.Contains(ps)) continue;
            seen.Add(ps);
            var ts = ps.textSettings;
            if (ts == null) { Debug.LogWarning("[SFF] textSettings null: " + ps.name); continue; }
            if (ts.defaultFontAsset == null)
            {
                ts.defaultFontAsset = fa;
                Debug.Log("[SFF] defaultFont(" + ps.name + ") <- " + fa.name);
            }
            else if (ts.defaultFontAsset.fallbackFontAssetTable == null)
            {
                Debug.LogWarning("[SFF] fallbackFontAssetTable null on: " + ps.name);
            }
            else if (!ts.defaultFontAsset.fallbackFontAssetTable.Contains(fa))
            {
                ts.defaultFontAsset.fallbackFontAssetTable.Add(fa);
                Debug.Log("[SFF] fallback(" + ps.name + ") += " + fa.name);
            }
        }
    }

    private FontAsset GetFirstLoaded()
    {
        foreach (var g in new[] { SystemFontHelper.FontGroup.CJK,
            SystemFontHelper.FontGroup.Latin, SystemFontHelper.FontGroup.Fallback })
            if (_loaded.TryGetValue(g, out var l) && l.Count > 0) return l[0];
        return null;
    }

    private void DiagnoseCandidatePaths()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SFF] -- Candidate Paths --");
        foreach (SystemFontHelper.FontGroup g in System.Enum.GetValues(typeof(SystemFontHelper.FontGroup)))
        {
            var found = SystemFontHelper.GetAllExistingPaths(g);
            sb.AppendLine("  " + g + ": " + found.Count + " file(s)");
            foreach (var p in found) sb.AppendLine("    OK " + p);
        }
        Debug.Log(sb.ToString());
    }

    private void DiagnoseTextSettings(string tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SFF] -- TextSettings " + tag + " --");
        var docs = FindObjectsOfType<UIDocument>();
        sb.AppendLine("  UIDocument count: " + docs.Length);
        var seen = new HashSet<PanelSettings>();
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            sb.AppendLine("  [" + doc.name + "]");
            var ps = doc.panelSettings;
            sb.AppendLine("    panelSettings: " + (ps == null ? "NULL" : ps.name));
            if (ps == null || seen.Contains(ps)) continue;
            seen.Add(ps);
            var ts = ps.textSettings;
            sb.AppendLine("    textSettings : " + (ts == null ? "NULL" : ts.name));
            if (ts == null) continue;
            var def = ts.defaultFontAsset;
            sb.AppendLine("    defaultFont  : " + (def == null ? "NULL" : def.name));
            if (def == null) continue;
            if (def.fallbackFontAssetTable == null) { sb.AppendLine("    fallbackTable: NULL"); continue; }
            sb.AppendLine("    fallbackCount: " + def.fallbackFontAssetTable.Count);
            foreach (var fb in def.fallbackFontAssetTable)
                sb.AppendLine("      -> " + (fb == null ? "NULL" : fb.name));
        }
        Debug.Log(sb.ToString());
    }

    private void DiagnoseFallbackChain(FontAsset root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SFF] -- Fallback Chain --");
        sb.AppendLine("  Primary: " + root.name);
        if (root.fallbackFontAssetTable == null)
        {
            sb.AppendLine("  fallbackFontAssetTable: NULL");
            Debug.Log(sb.ToString());
            return;
        }
        sb.AppendLine("  Count: " + root.fallbackFontAssetTable.Count);
        for (int i = 0; i < root.fallbackFontAssetTable.Count; i++)
        {
            var fb = root.fallbackFontAssetTable[i];
            sb.AppendLine("  [" + i + "] " + (fb == null ? "NULL" : fb.name));
        }
        Debug.Log(sb.ToString());
    }

    public bool CanRenderCharacter(char c)
    {
        foreach (var kv in _loaded)
            foreach (var fa in kv.Value)
                if (fa != null && fa.HasCharacter(c)) return true;
        return false;
    }

    public IReadOnlyList<FontAsset> GetLoadedFonts(SystemFontHelper.FontGroup group)
    {
        return _loaded.TryGetValue(group, out var l) ? l : System.Array.Empty<FontAsset>();
    }

    [ContextMenu("Run Diagnostics")]
    public void RunDiagnostics()
    {
        DiagnoseCandidatePaths();
        DiagnoseTextSettings("[Manual]");
        var target = primaryFontAsset ?? GetDefaultFontFromPanelSettings();
        if (target != null) DiagnoseFallbackChain(target);
        else Debug.LogWarning("[SFF] No primary font found");
    }
}
