using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 回收站操作（Win/Mac/Linux）
/// </summary>
public static class WindowsRecycleBin
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;

    /// <summary>
    /// 将文件/文件夹移至回收站（Win 支持多路径用 \0 分隔）
    /// </summary>
    public static int MoveToRecycleBin(string path, bool showConfirm = true)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        if (!path.EndsWith("\0")) path += "\0";
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path,
            fFlags = (ushort)(FOF_ALLOWUNDO | (showConfirm ? 0 : FOF_NOCONFIRMATION))
        };
        return SHFileOperation(ref op);
    }

    public static int MoveToRecycleBinSilent(string path)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        if (!path.EndsWith("\0")) path += "\0";
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path,
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI
        };
        return SHFileOperation(ref op);
    }

#elif UNITY_STANDALONE_OSX
    public static int MoveToRecycleBin(string path, bool showConfirm = true)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        var paths = path.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        int lastResult = 0;
        foreach (var p in paths)
        {
            var r = MoveOneToTrash(p);
            if (r != 0) lastResult = r;
        }
        return lastResult;
    }

    public static int MoveToRecycleBinSilent(string path) => MoveToRecycleBin(path, false);

    private static int MoveOneToTrash(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return -1;
        try
        {
            var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            var name = Path.GetFileName(path);
            var dest = Path.Combine(trash, name);
            var n = 1;
            while (File.Exists(dest) || Directory.Exists(dest))
                dest = Path.Combine(trash, name + " " + (n++));
            if (File.Exists(path)) File.Move(path, dest);
            else if (Directory.Exists(path)) Directory.Move(path, dest);
            return 0;
        }
        catch (Exception ex) { Debug.LogWarning($"[RecycleBin] {ex.Message}"); return -1; }
    }

#elif UNITY_STANDALONE_LINUX
    public static int MoveToRecycleBin(string path, bool showConfirm = true)
    {
        if (string.IsNullOrEmpty(path)) return -1;
        var paths = path.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        int lastResult = 0;
        foreach (var p in paths)
        {
            var r = MoveOneToTrash(p);
            if (r != 0) lastResult = r;
        }
        return lastResult;
    }

    public static int MoveToRecycleBinSilent(string path) => MoveToRecycleBin(path, false);

    private static int MoveOneToTrash(string path)
    {
        var code = ProcessHelper.Run("gio", "trash " + ProcessHelper.Quote(path), 3000);
        if (code == 0) return 0;
        try
        {
            var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash", "files");
            if (!Directory.Exists(trash)) Directory.CreateDirectory(trash);
            var name = Path.GetFileName(path);
            var dest = Path.Combine(trash, name);
            var n = 1;
            while (File.Exists(dest) || Directory.Exists(dest))
                dest = Path.Combine(trash, name + " " + (n++));
            if (File.Exists(path)) File.Move(path, dest);
            else if (Directory.Exists(path)) Directory.Move(path, dest);
            return 0;
        }
        catch (Exception ex) { Debug.LogWarning($"[RecycleBin] {ex.Message}"); return -1; }
    }

#else
    public static int MoveToRecycleBin(string path, bool showConfirm = true) => -1;
    public static int MoveToRecycleBinSilent(string path) => -1;
#endif
}
