using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class IcoGeneratorWindow : EditorWindow
{
    private Texture2D _sourceTexture;
    private string    _outputDir      = "Assets/StreamingAssets";
    private string    _outputFileName = "app.ico";
    private string    _lastResult     = "";
    private bool      _lastSuccess;
    private Vector2   _scroll;
    private static readonly int[] AllSizes     = { 16, 24, 32, 48, 64, 128, 256 };
    private readonly        bool[] _sizeEnabled = { true, false, true, true, false, false, false };

    [MenuItem("Tools/ICO Generator")]
    public static void ShowWindow() {
        var win = GetWindow<IcoGeneratorWindow>("ICO Generator");
        win.minSize = new Vector2(420, 480); win.Show();
    }
    private void OnSelectionChange() {
        if (Selection.activeObject is Texture2D tex && _sourceTexture == null) {
            _sourceTexture = tex; _outputFileName = tex.name + ".ico"; _lastResult = ""; Repaint();
        }
    }
    private void OnGUI() {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawHeader(); GUILayout.Space(8); DrawSourceSection(); GUILayout.Space(8);
        DrawSizeSection(); GUILayout.Space(8); DrawOutputSection(); GUILayout.Space(12);
        DrawGenerateButton(); GUILayout.Space(6); DrawResult();
        EditorGUILayout.EndScrollView(); HandleDragAndDrop();
    }
    private void DrawHeader() {
        var s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        GUILayout.Label("ICO Generator", s, GUILayout.Height(28));
        GUILayout.Label("PNG/JPG -> Multi-size BMP DIB ICO (Win32 LoadImage compatible)", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }
    private void DrawSourceSection() {
        GUILayout.Label("Source Texture", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope()) {
            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField(_sourceTexture, typeof(Texture2D), false, GUILayout.Height(64), GUILayout.Width(64));
            using (new EditorGUILayout.VerticalScope()) {
                if (_sourceTexture != null) {
                    GUILayout.Label("Name : " + _sourceTexture.name, EditorStyles.miniLabel);
                    GUILayout.Label(string.Format("Size : {0} x {1}", _sourceTexture.width, _sourceTexture.height), EditorStyles.miniLabel);
                    GUILayout.Label("Path : " + AssetDatabase.GetAssetPath(_sourceTexture), EditorStyles.miniLabel);
                } else EditorGUILayout.HelpBox("Drag a PNG/JPG from Project panel, or use the picker.", MessageType.Info);
            }
        }
    }
    private void DrawSizeSection() {
        GUILayout.Label("Output Sizes", EditorStyles.boldLabel);
        GUILayout.Label("Select sizes to include in the ICO:", EditorStyles.miniLabel);
        GUILayout.Space(2);
        using (new EditorGUILayout.HorizontalScope())
            for (int i = 0; i < AllSizes.Length; i++)
                _sizeEnabled[i] = GUILayout.Toggle(_sizeEnabled[i], AllSizes[i] + "px", GUI.skin.button, GUILayout.Width(52), GUILayout.Height(24));
        GUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope()) {
            GUILayout.Label("Preset:", GUILayout.Width(50));
            if (GUILayout.Button("Tray(16/32/48)",       EditorStyles.miniButton)) SetSizes(16,32,48);
            if (GUILayout.Button("Full(16/32/48/64/256)",EditorStyles.miniButton)) SetSizes(16,32,48,64,256);
            if (GUILayout.Button("All",  EditorStyles.miniButton)) SetAllSizes(true);
            if (GUILayout.Button("None", EditorStyles.miniButton)) SetAllSizes(false);
        }
    }
    private void DrawOutputSection() {
        GUILayout.Label("Output Settings", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope()) {
            _outputDir = EditorGUILayout.TextField("Output Dir", _outputDir);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
                string sel = EditorUtility.OpenFolderPanel("Select Output Directory", Application.dataPath, "");
                if (!string.IsNullOrEmpty(sel)) {
                    if (sel.StartsWith(Application.dataPath)) sel = "Assets" + sel.Substring(Application.dataPath.Length).Replace("\\", "/");
                    _outputDir = sel;
                }
            }
        }
        _outputFileName = EditorGUILayout.TextField("File Name", _outputFileName);
        GUILayout.Label("Full path: " + Path.Combine(_outputDir, _outputFileName).Replace("\\", "/"), EditorStyles.miniLabel);
    }
    private void DrawGenerateButton() {
        bool canGen = _sourceTexture != null && HasAnySizeEnabled();
        using (new EditorGUI.DisabledScope(!canGen)) {
            var bs = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = UnityEngine.FontStyle.Bold };
            UnityEngine.Color prev = GUI.backgroundColor;
            GUI.backgroundColor = canGen ? new UnityEngine.Color(0.45f, 0.82f, 0.45f) : UnityEngine.Color.gray;
            if (GUILayout.Button("Generate ICO", bs, GUILayout.Height(36))) Generate();
            GUI.backgroundColor = prev;
        }
        if (_sourceTexture == null) EditorGUILayout.HelpBox("Please select a source texture.", MessageType.Warning);
        else if (!HasAnySizeEnabled()) EditorGUILayout.HelpBox("Please select at least one size.", MessageType.Warning);
    }
    private void DrawResult() {
        if (string.IsNullOrEmpty(_lastResult)) return;
        EditorGUILayout.HelpBox(_lastResult, _lastSuccess ? MessageType.Info : MessageType.Error);
        if (_lastSuccess && GUILayout.Button("Reveal in Project", EditorStyles.miniButton)) {
            AssetDatabase.Refresh();
            string rel = Path.Combine(_outputDir, _outputFileName).Replace("\\", "/");
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rel);
            if (asset != null) EditorGUIUtility.PingObject(asset);
            else EditorUtility.RevealInFinder(Path.GetFullPath(rel));
        }
    }
    private void HandleDragAndDrop() {
        var evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (evt.type == EventType.DragPerform) {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is Texture2D tex) { _sourceTexture = tex; _outputFileName = tex.name + ".ico"; _lastResult = ""; Repaint(); break; }
        }
        evt.Use();
    }
    private void Generate() {
        _lastResult = ""; _lastSuccess = false;
        string assetPath = AssetDatabase.GetAssetPath(_sourceTexture);
        if (string.IsNullOrEmpty(assetPath)) { _lastResult = "Cannot resolve asset path."; return; }
        string srcAbs = Path.GetFullPath(assetPath);
        if (!File.Exists(srcAbs)) { _lastResult = "Source file not found: " + srcAbs; return; }
        string dirAbs = _outputDir.StartsWith("Assets")
            ? Path.Combine(Application.dataPath, _outputDir.Substring("Assets".Length).TrimStart("/".ToCharArray()))
            : _outputDir;
        try { Directory.CreateDirectory(dirAbs); }
        catch (Exception ex) { _lastResult = "Cannot create dir: " + ex.Message; return; }
        string outAbs = Path.Combine(dirAbs, _outputFileName);
        var sizes = new List<int>();
        for (int i = 0; i < AllSizes.Length; i++) if (_sizeEnabled[i]) sizes.Add(AllSizes[i]);
        if (sizes.Count == 0) { _lastResult = "No size selected."; return; }
        try {
            long bytes = BuildIco(srcAbs, outAbs, sizes.ToArray());
            AssetDatabase.Refresh(); _lastSuccess = true;
            _lastResult = string.Format("Done!\nPath : {0}\nSizes: {1}\nBytes: {2:N0}",
                outAbs, string.Join(", ", sizes.ConvertAll(s => s + "px").ToArray()), bytes);
        } catch (Exception ex) { _lastResult = "Build failed: " + ex.Message; }
    }

    private static long BuildIco(string srcPath, string dstPath, int[] sizes) {
        using (var srcBmp = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(srcPath)) {
            byte[][] imgData = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) {
                int sz = sizes[i];
                using (var bmp = new System.Drawing.Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) {
                    using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(srcBmp, 0, 0, sz, sz);
                    }
                    byte[] pixels = new byte[sz * sz * 4];
                    for (int row = 0; row < sz; row++) {
                        int srcRow = sz - 1 - row;
                        for (int col = 0; col < sz; col++) {
                            System.Drawing.Color c = bmp.GetPixel(col, srcRow);
                            int idx = (row * sz + col) * 4;
                            pixels[idx] = c.B; pixels[idx+1] = c.G; pixels[idx+2] = c.R; pixels[idx+3] = c.A;
                        }
                    }
                    using (var ms = new MemoryStream())
                    using (var bw = new BinaryWriter(ms)) {
                        bw.Write((int)40); bw.Write((int)sz); bw.Write((int)(sz*2));
                        bw.Write((short)1); bw.Write((short)32);
                        bw.Write((int)0); bw.Write((int)0); bw.Write((int)0);
                        bw.Write((int)0); bw.Write((int)0); bw.Write((int)0);
                        bw.Write(pixels);
                        int andStride = ((sz+31)/32)*4;
                        var andRow = new byte[andStride];
                        for (int r = 0; r < sz; r++) bw.Write(andRow);
                        bw.Flush(); imgData[i] = ms.ToArray();
                    }
                }
            }
            int count = sizes.Length, hdrSize = 6 + count * 16;
            using (var ico = new MemoryStream())
            using (var iw = new BinaryWriter(ico)) {
                iw.Write((short)0); iw.Write((short)1); iw.Write((short)count);
                int offset = hdrSize;
                for (int i = 0; i < count; i++) {
                    int sz = sizes[i];
                    iw.Write((byte)(sz==256?0:sz)); iw.Write((byte)(sz==256?0:sz));
                    iw.Write((byte)0); iw.Write((byte)0);
                    iw.Write((short)1); iw.Write((short)32);
                    iw.Write((int)imgData[i].Length); iw.Write((int)offset);
                    offset += imgData[i].Length;
                }
                for (int i = 0; i < count; i++) iw.Write(imgData[i]);
                iw.Flush();
                File.WriteAllBytes(dstPath, ico.ToArray());
                return ico.Length;
            }
        }
    }

    private bool HasAnySizeEnabled() { foreach (var b in _sizeEnabled) if (b) return true; return false; }
    private void SetSizes(params int[] enabled) {
        var set = new HashSet<int>(enabled);
        for (int i = 0; i < AllSizes.Length; i++) _sizeEnabled[i] = set.Contains(AllSizes[i]);
    }
    private void SetAllSizes(bool val) { for (int i = 0; i < _sizeEnabled.Length; i++) _sizeEnabled[i] = val; }
}
