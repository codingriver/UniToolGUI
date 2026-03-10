using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// 文件/文件夹选择对话框（Win/Mac/Linux）
/// </summary>
public static class WindowsFileDialog
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    #region 常量与结构定义

    private const int MAX_PATH = 260;
    private const int MAX_PATH_MULTI = 65536;

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_HIDEREADONLY = 0x00000004;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_OVERWRITEPROMPT = 0x00000002;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_EXPLORER = 0x00080000;

    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;
    private const uint BIF_EDITBOX = 0x0010;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct BROWSEINFOW
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public string pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    #endregion

    #region DLL 导入

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
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = GetActiveWindow();
        ofn.lpstrFilter = filter;
        ofn.lpstrFile = new string(new char[MAX_PATH]);
        ofn.nMaxFile = MAX_PATH;
        ofn.lpstrTitle = title;
        ofn.lpstrInitialDir = initialDir;
        ofn.lpstrDefExt = defaultExt;
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR;

        return GetOpenFileName(ref ofn) ? ofn.lpstrFile : null;
    }

    /// <summary>
    /// 打开多文件选择对话框，返回选中文件路径数组
    /// </summary>
    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = GetActiveWindow();
        ofn.lpstrFilter = filter;
        ofn.lpstrFile = new string(new char[MAX_PATH_MULTI]);
        ofn.nMaxFile = MAX_PATH_MULTI;
        ofn.lpstrTitle = title;
        ofn.lpstrInitialDir = initialDir;
        ofn.lpstrDefExt = defaultExt;
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR | OFN_ALLOWMULTISELECT | OFN_EXPLORER;

        if (!GetOpenFileName(ref ofn)) return null;

        var parts = ofn.lpstrFile.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        if (parts.Length == 1) return parts;
        var dir = parts[0];
        var files = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
            files[i - 1] = System.IO.Path.Combine(dir, parts[i]);
        return files;
    }

    /// <summary>
    /// 打开保存文件对话框
    /// </summary>
    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = GetActiveWindow();
        ofn.lpstrFilter = filter;
        var fileBuffer = new char[MAX_PATH];
        if (!string.IsNullOrEmpty(defaultFileName))
        {
            for (int i = 0; i < defaultFileName.Length && i < MAX_PATH - 1; i++)
                fileBuffer[i] = defaultFileName[i];
        }
        ofn.lpstrFile = new string(fileBuffer);
        ofn.nMaxFile = MAX_PATH;
        ofn.lpstrTitle = title;
        ofn.lpstrInitialDir = initialDir;
        ofn.lpstrDefExt = defaultExt;
        ofn.Flags = OFN_OVERWRITEPROMPT | OFN_NOCHANGEDIR;

        return GetSaveFileName(ref ofn) ? ofn.lpstrFile : null;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        var bi = new BROWSEINFOW();
        bi.hwndOwner = GetActiveWindow();
        bi.lpszTitle = title;
        bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE | BIF_EDITBOX;
        bi.pszDisplayName = new string(new char[MAX_PATH]);

        IntPtr pidl = SHBrowseForFolder(ref bi);
        if (pidl != IntPtr.Zero)
        {
            IntPtr pPath = Marshal.AllocHGlobal(MAX_PATH * 2);
            try
            {
                if (SHGetPathFromIDList(pidl, pPath))
                    return Marshal.PtrToStringAuto(pPath);
            }
            finally
            {
                Marshal.FreeHGlobal(pPath);
                CoTaskMemFree(pidl);
            }
        }
        return null;
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");

        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2)
        {
            sb.Append(filterPairs[i]);
            sb.Append('\0');
            sb.Append(filterPairs[i + 1]);
            sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }

#elif UNITY_STANDALONE_OSX
    private static string[] ParseExtensionsFromFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;
        var parts = filter.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        var exts = new System.Collections.Generic.List<string>();
        for (int i = 1; i < parts.Length; i += 2)
        {
            foreach (var p in parts[i].Split(';'))
            {
                var ext = p.Trim().TrimStart('*').TrimStart('.');
                if (!string.IsNullOrEmpty(ext) && ext != "*") exts.Add(ext);
            }
        }
        return exts.Count > 0 ? exts.ToArray() : null;
    }

    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(title))
            sb.Append($"set thePrompt to \"{EscAS(title)}\"\n");
        else
            sb.Append("set thePrompt to \"选择文件\"\n");

        sb.Append("set theFile to (choose file with prompt thePrompt");
        var exts = ParseExtensionsFromFilter(filter);
        if (exts != null && exts.Length > 0)
        {
            sb.Append(" of type {");
            for (int i = 0; i < exts.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"\"{EscAS(exts[i])}\"");
            }
            sb.Append("}");
        }
        if (!string.IsNullOrEmpty(initialDir))
            sb.Append($" default location (POSIX file \"{EscAS(initialDir.Replace("\\", "/"))}\")");
        sb.Append(")\nreturn POSIX path of theFile");

        return RunOsascriptFile(sb.ToString());
    }

    /// <summary>
    /// 多文件选择
    /// </summary>
    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(title))
            sb.Append($"set thePrompt to \"{EscAS(title)}\"\n");
        else
            sb.Append("set thePrompt to \"选择文件\"\n");

        sb.Append("set theFiles to (choose file with prompt thePrompt with multiple selections allowed");
        var exts = ParseExtensionsFromFilter(filter);
        if (exts != null && exts.Length > 0)
        {
            sb.Append(" of type {");
            for (int i = 0; i < exts.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"\"{EscAS(exts[i])}\"");
            }
            sb.Append("}");
        }
        if (!string.IsNullOrEmpty(initialDir))
            sb.Append($" default location (POSIX file \"{EscAS(initialDir.Replace("\\", "/"))}\")");
        sb.Append(")\n");
        sb.Append("set output to \"\"\n");
        sb.Append("repeat with f in theFiles\n");
        sb.Append("  set output to output & (POSIX path of f) & \"\n\"\n");
        sb.Append("end repeat\n");
        sb.Append("return output");

        var result = RunOsascriptFile(sb.ToString());
        if (string.IsNullOrEmpty(result)) return null;
        return result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// 保存文件对话框
    /// </summary>
    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFile to (choose file name");
        if (!string.IsNullOrEmpty(title))
            sb.Append($" with prompt \"{EscAS(title)}\"");
        if (!string.IsNullOrEmpty(defaultFileName))
            sb.Append($" default name \"{EscAS(defaultFileName)}\"");
        if (!string.IsNullOrEmpty(initialDir))
            sb.Append($" default location (POSIX file \"{EscAS(initialDir.Replace("\\", "/"))}\")");
        sb.Append(")\nreturn POSIX path of theFile");

        var result = RunOsascriptFile(sb.ToString());
        if (result != null && !string.IsNullOrEmpty(defaultExt) && !result.Contains("."))
            result += "." + defaultExt;
        return result;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        var sb = new StringBuilder();
        sb.Append("set theFolder to (choose folder");
        if (!string.IsNullOrEmpty(title))
            sb.Append($" with prompt \"{EscAS(title)}\"");
        if (!string.IsNullOrEmpty(initialDir))
            sb.Append($" default location (POSIX file \"{EscAS(initialDir.Replace("\\", "/"))}\")");
        sb.Append(")\nreturn POSIX path of theFolder");

        return RunOsascriptFile(sb.ToString());
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2)
        {
            sb.Append(filterPairs[i]); sb.Append('\0');
            sb.Append(filterPairs[i + 1]); sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }

    private static string EscAS(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string RunOsascriptFile(string script)
    {
        try
        {
            var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fd_" + Guid.NewGuid().ToString("N") + ".scpt");
            System.IO.File.WriteAllText(tempFile, script);
            try
            {
                return ProcessHelper.RunAndRead("osascript", tempFile, 60000);
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[FileDialog] {ex.Message}"); return null; }
    }

#elif UNITY_STANDALONE_LINUX
    public static string OpenFilePanel(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var args = "--file-selection";
        if (!string.IsNullOrEmpty(title)) args += $" --title={ProcessHelper.Quote(title)}";
        if (!string.IsNullOrEmpty(initialDir)) args += $" --filename={ProcessHelper.Quote(initialDir + "/")}";
        var exts = ParseExtensionsFromFilter(filter);
        if (exts != null)
        {
            foreach (var ext in exts)
                args += $" --file-filter=\"*.{ext}\"";
        }
        return ProcessHelper.RunAndRead("zenity", args, 60000);
    }

    public static string[] OpenFilePanelMulti(string title, string filter, string defaultExt = null, string initialDir = null)
    {
        var args = "--file-selection --multiple --separator=\"|\"";
        if (!string.IsNullOrEmpty(title)) args += $" --title={ProcessHelper.Quote(title)}";
        if (!string.IsNullOrEmpty(initialDir)) args += $" --filename={ProcessHelper.Quote(initialDir + "/")}";
        var exts = ParseExtensionsFromFilter(filter);
        if (exts != null)
        {
            foreach (var ext in exts)
                args += $" --file-filter=\"*.{ext}\"";
        }
        var result = ProcessHelper.RunAndRead("zenity", args, 60000);
        return string.IsNullOrEmpty(result) ? null : result.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
    }

    public static string SaveFilePanel(string title, string filter, string defaultExt = null, string initialDir = null, string defaultFileName = null)
    {
        var args = "--file-selection --save --confirm-overwrite";
        if (!string.IsNullOrEmpty(title)) args += $" --title={ProcessHelper.Quote(title)}";
        if (!string.IsNullOrEmpty(initialDir))
        {
            var path = initialDir + "/";
            if (!string.IsNullOrEmpty(defaultFileName)) path = System.IO.Path.Combine(initialDir, defaultFileName);
            args += $" --filename={ProcessHelper.Quote(path)}";
        }
        else if (!string.IsNullOrEmpty(defaultFileName))
        {
            args += $" --filename={ProcessHelper.Quote(defaultFileName)}";
        }
        var result = ProcessHelper.RunAndRead("zenity", args, 60000);
        if (result != null && !string.IsNullOrEmpty(defaultExt) && !result.Contains("."))
            result += "." + defaultExt;
        return result;
    }

    public static string OpenFolderPanel(string title, string initialDir = null)
    {
        var args = "--file-selection --directory";
        if (!string.IsNullOrEmpty(title)) args += $" --title={ProcessHelper.Quote(title)}";
        if (!string.IsNullOrEmpty(initialDir)) args += $" --filename={ProcessHelper.Quote(initialDir + "/")}";
        return ProcessHelper.RunAndRead("zenity", args, 60000);
    }

    public static string CreateFilter(params string[] filterPairs)
    {
        if (filterPairs == null || filterPairs.Length % 2 != 0)
            throw new ArgumentException("必须提供成对的描述和模式");
        var sb = new StringBuilder();
        for (int i = 0; i < filterPairs.Length; i += 2)
        {
            sb.Append(filterPairs[i]); sb.Append('\0');
            sb.Append(filterPairs[i + 1]); sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }

    private static string[] ParseExtensionsFromFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;
        var parts = filter.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        var exts = new System.Collections.Generic.List<string>();
        for (int i = 1; i < parts.Length; i += 2)
        {
            foreach (var p in parts[i].Split(';'))
            {
                var ext = p.Trim().TrimStart('*').TrimStart('.');
                if (!string.IsNullOrEmpty(ext) && ext != "*") exts.Add(ext);
            }
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
        for (int i = 0; i < filterPairs.Length; i += 2)
        {
            sb.Append(filterPairs[i]); sb.Append('\0');
            sb.Append(filterPairs[i + 1]); sb.Append('\0');
        }
        sb.Append('\0');
        return sb.ToString();
    }
#endif
}
