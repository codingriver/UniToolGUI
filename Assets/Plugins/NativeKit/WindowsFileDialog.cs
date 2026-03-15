using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// 文件/文件夹选择对话框（Win/Mac/Linux）。
/// Windows : Win32 GetOpenFileName / GetSaveFileName / SHBrowseForFolder。
/// macOS   : osascript via stdin（无临时文件，无磁盘IO竞态）。
/// Linux   : zenity。
/// </summary>
public static class WindowsFileDialog
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    #region Win32 常量与结构
    private const int MAX_PATH        = 260;
    private const int MAX_PATH_MULTI  = 65536;
    private const int OFN_FILEMUSTEXIST    = 0x00001000;
    private const int OFN_HIDEREADONLY     = 0x00000004;
    private const int OFN_NOCHANGEDIR      = 0x00000008;
    private const int OFN_OVERWRITEPROMPT  = 0x00000002;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_EXPLORER         = 0x00080000;
    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE   = 0x0040;
    private const uint BIF_EDITBOX          = 0x0010;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int    lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int    nMaxCustFilter;
        public int    nFilterIndex;
        public string lpstrFile;
        public int    nMaxFile;
        public string lpstrFileTitle;
        public int    nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int    Flags;
        public short  nFileOffset;
        public short  nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int    dwReserved;
        public int    flagsEx;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint   ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int    iImage;
    }
    #endregion
    #region Win32 导入
    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);
    [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHBrowseForFolder(ref BROWSEINFOW lpbi);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);
    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr hMem);
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();
    #endregion

    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var ofn = BuildOFN(filter, title, initialDir, defaultExt, MAX_PATH,
            OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR);
        return GetOpenFileName(ref ofn) ? ofn.lpstrFile : null;
    }

    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var ofn = BuildOFN(filter, title, initialDir, defaultExt, MAX_PATH_MULTI,
            OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR | OFN_ALLOWMULTISELECT | OFN_EXPLORER);
        if (!GetOpenFileName(ref ofn)) return null;
        var parts = ofn.lpstrFile.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        if (parts.Length == 1) return parts;
        var dir = parts[0];
        var files = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++) files[i - 1] = System.IO.Path.Combine(dir, parts[i]);
        return files;
    }

    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var ofn = BuildOFN(filter, title, initialDir, defaultExt, MAX_PATH, OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR);
        if (!string.IsNullOrEmpty(defaultFileName))
        {
            var buf = new char[MAX_PATH];
            for (int i = 0; i < defaultFileName.Length && i < MAX_PATH - 1; i++) buf[i] = defaultFileName[i];
            ofn.lpstrFile = new string(buf);
        }
        return GetSaveFileName(ref ofn) ? ofn.lpstrFile : null;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        var bi = new BROWSEINFOW
        {
            hwndOwner = GetActiveWindow(), lpszTitle = title,
            ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_EDITBOX,
            pszDisplayName = new string(new char[MAX_PATH])
        };
        IntPtr pidl = SHBrowseForFolder(ref bi);
        if (pidl == IntPtr.Zero) return null;
        IntPtr pPath = Marshal.AllocHGlobal(MAX_PATH * 2);
        try { return SHGetPathFromIDList(pidl, pPath) ? Marshal.PtrToStringAuto(pPath) : null; }
        finally { Marshal.FreeHGlobal(pPath); CoTaskMemFree(pidl); }
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2) { sb.Append(filterPairs[i]); sb.Append('\0'); sb.Append(filterPairs[i + 1]); sb.Append('\0'); }
        sb.Append('\0');
        return sb.ToString();
    }

    private static OPENFILENAME BuildOFN(string filter, string title, string initialDir,
        string defaultExt, int maxFile, int flags)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn); ofn.hwndOwner = GetActiveWindow();
        ofn.lpstrFilter = filter; ofn.lpstrFile = new string(new char[maxFile]); ofn.nMaxFile = maxFile;
        ofn.lpstrTitle = title; ofn.lpstrInitialDir = initialDir;
        ofn.lpstrDefExt = defaultExt; ofn.Flags = flags;
        return ofn;
    }

#elif UNITY_STANDALONE_OSX
    // ======================================================
    // macOS: osascript via stdin - 无临时文件
    // ======================================================

    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFile to (choose file");
        AppendPrompt(sb, title); AppendExtensions(sb, filter); AppendDefaultLocation(sb, initialDir);
        sb.Append(")
return POSIX path of theFile");
        return RunOsascriptInline(sb.ToString());
    }

    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFiles to (choose file with multiple selections allowed");
        AppendPrompt(sb, title); AppendExtensions(sb, filter); AppendDefaultLocation(sb, initialDir);
        sb.Append(")
set out to ""
repeat with f in theFiles
  set out to out & (POSIX path of f) & "
"
end repeat
return out");
        var result = RunOsascriptInline(sb.ToString());
        if (string.IsNullOrEmpty(result)) return null;
        return result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFile to (choose file name");
        AppendPrompt(sb, title);
        if (!string.IsNullOrEmpty(defaultFileName))
            sb.Append(" default name "" + EscAS(defaultFileName) + """ );
        AppendDefaultLocation(sb, initialDir);
        sb.Append(")
return POSIX path of theFile");
        var result = RunOsascriptInline(sb.ToString());
        if (result != null && !string.IsNullOrEmpty(defaultExt) && !result.Contains("."))
            result += "." + defaultExt;
        return result;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFolder to (choose folder");
        AppendPrompt(sb, title); AppendDefaultLocation(sb, initialDir);
        sb.Append(")
return POSIX path of theFolder");
        return RunOsascriptInline(sb.ToString());
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2) { sb.Append(filterPairs[i]); sb.Append('\0'); sb.Append(filterPairs[i + 1]); sb.Append('\0'); }
        sb.Append('\0');
        return sb.ToString();
    }

    private static void AppendPrompt(StringBuilder sb, string title)
    {
        if (!string.IsNullOrEmpty(title))
            sb.Append(" with prompt "" + EscAS(title) + """ );
    }

    private static void AppendDefaultLocation(StringBuilder sb, string dir)
    {
        if (!string.IsNullOrEmpty(dir))
            sb.Append(" default location (POSIX file \"" + EscAS(dir.Replace("\\\\", "/")) + "\")");
    }

    private static void AppendExtensions(StringBuilder sb, string filter)
    {
        var exts = ParseExtensionsFromFilter(filter);
        if (exts == null || exts.Length == 0) return;
        sb.Append(" of type {");
        for (int i = 0; i < exts.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(""" + EscAS(exts[i]) + """ );
        }
        sb.Append("}");
    }

    /// <summary>
    /// 通过 stdin 将 AppleScript 传给 osascript（osascript -）。
    /// 无需写临时 .scpt 文件，无磁盘IO也无竞态风险。
    /// </summary>
    private static string RunOsascriptInline(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardInputEncoding  = System.Text.Encoding.UTF8,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                p.StandardInput.Write(script);
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(60000);
                return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WindowsFileDialog] osascript failed: " + ex.Message);
            return null;
        }
    }

    private static string EscAS(string s) =>
        (s ?? "").Replace("\\", "\\\\").Replace(""", "\"");

    private static string[] ParseExtensionsFromFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;
        var parts = filter.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        var exts = new System.Collections.Generic.List<string>();
        for (int i = 1; i < parts.Length; i += 2)
            foreach (var p2 in parts[i].Split(';'))
            {
                var ext = p2.Trim().TrimStart('*').TrimStart('.');
                if (!string.IsNullOrEmpty(ext) && ext != "*") exts.Add(ext);
            }
        return exts.Count > 0 ? exts.ToArray() : null;
    }

#elif UNITY_STANDALONE_LINUX
    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        return ProcessHelper.RunAndRead("zenity",
            BuildZenityArgs("--file-selection", title, initialDir, filter), 60000)?.TrimEnd('\n');
    }

    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var result = ProcessHelper.RunAndRead("zenity",
            BuildZenityArgs("--file-selection --multiple --separator=|", title, initialDir, filter), 60000);
        return string.IsNullOrEmpty(result) ? null : result.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var args = new StringBuilder("--file-selection --save --confirm-overwrite");
        if (!string.IsNullOrEmpty(title)) args.Append(" --title=" + ProcessHelper.Quote(title));
        var path = initialDir ?? "";
        if (!string.IsNullOrEmpty(defaultFileName))
            path = string.IsNullOrEmpty(path) ? defaultFileName : System.IO.Path.Combine(path, defaultFileName);
        if (!string.IsNullOrEmpty(path)) args.Append(" --filename=" + ProcessHelper.Quote(path));
        AppendZenityFilters(args, filter);
        var result = ProcessHelper.RunAndRead("zenity", args.ToString(), 60000)?.TrimEnd('\n');
        if (result != null && !string.IsNullOrEmpty(defaultExt) && !result.Contains(".")) result += "." + defaultExt;
        return result;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        return ProcessHelper.RunAndRead("zenity",
            BuildZenityArgs("--file-selection --directory", title, initialDir, null), 60000)?.TrimEnd('\n');
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2) { sb.Append(filterPairs[i]); sb.Append('\0'); sb.Append(filterPairs[i+1]); sb.Append('\0'); }
        sb.Append('\0');
        return sb.ToString();
    }

    private static string BuildZenityArgs(string mode, string title, string initialDir, string filter)
    {
        var sb = new StringBuilder(mode);
        if (!string.IsNullOrEmpty(title)) sb.Append(" --title=" + ProcessHelper.Quote(title));
        if (!string.IsNullOrEmpty(initialDir)) sb.Append(" --filename=" + ProcessHelper.Quote(initialDir.TrimEnd('/', '\\') + "/"));
        AppendZenityFilters(sb, filter);
        return sb.ToString();
    }

    private static void AppendZenityFilters(StringBuilder sb, string filter)
    {
        var exts = ParseExtensionsFromFilter(filter);
        if (exts == null) return;
        foreach (var ext in exts) sb.Append(" --file-filter=\"*." + ext + "\"");
    }

    private static string[] ParseExtensionsFromFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;
        var parts = filter.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        var exts = new System.Collections.Generic.List<string>();
        for (int i = 1; i < parts.Length; i += 2)
            foreach (var p2 in parts[i].Split(';'))
            {
                var ext = p2.Trim().TrimStart('*').TrimStart('.');
                if (!string.IsNullOrEmpty(ext) && ext != "*") exts.Add(ext);
            }
        return exts.Count > 0 ? exts.ToArray() : null;
    }

#else
    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null) => null;
    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null) => null;
    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null) => null;
    public static string OpenFolderPanel(string title, string initialDir = null) => null;
    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2) { sb.Append(filterPairs[i]); sb.Append('\0'); sb.Append(filterPairs[i+1]); sb.Append('\0'); }
        sb.Append('\0');
        return sb.ToString();
    }
#endif
}
