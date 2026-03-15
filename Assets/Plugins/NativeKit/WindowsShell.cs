using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Shell 操作：打开 URL、文件、文件夹等（Win/Mac/Linux）
/// </summary>
public static class WindowsShell
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile,
        string lpParameters, string lpDirectory, int nShowCmd);

    private const int SW_SHOWNORMAL = 1;

    public static bool OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        IntPtr result = ShellExecute(IntPtr.Zero, "open", url, null, null, SW_SHOWNORMAL);
        return (long)result > 32;
    }

    public static bool OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        IntPtr result = ShellExecute(IntPtr.Zero, "open", filePath, null, null, SW_SHOWNORMAL);
        return (long)result > 32;
    }

    public static bool OpenFolder(string folderPath, string filePath = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return false;
        if (!string.IsNullOrEmpty(filePath))
        {
            IntPtr result = ShellExecute(IntPtr.Zero, "open", "explorer.exe",
                "/select,\"" + filePath.Replace("\"", "\\\"") + "\"", null, SW_SHOWNORMAL);
            return (long)result > 32;
        }
        IntPtr r = ShellExecute(IntPtr.Zero, "open", folderPath, null, null, SW_SHOWNORMAL);
        return (long)r > 32;
    }

    public static bool OpenFolderInExplorer(string folderPath)
    {
        return OpenFolder(folderPath, null);
    }

    public static bool Execute(string operation, string filePath, string parameters = null, string workingDir = null)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        IntPtr result = ShellExecute(IntPtr.Zero, operation ?? "open", filePath, parameters, workingDir, SW_SHOWNORMAL);
        return (long)result > 32;
    }

#elif UNITY_STANDALONE_OSX
    public static bool OpenUrl(string url) => RunOpen(url);
    public static bool OpenFile(string filePath) => RunOpen(filePath);
    public static bool OpenFolder(string folderPath, string filePath = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return false;
        if (!string.IsNullOrEmpty(filePath))
            return ProcessHelper.StartBackground("open", "-R " + ProcessHelper.Quote(filePath)) != null;
        return RunOpen(folderPath);
    }
    public static bool OpenFolderInExplorer(string folderPath) => OpenFolder(folderPath, null);
    public static bool Execute(string operation, string filePath, string parameters = null, string workingDir = null) => RunOpen(filePath);

    private static bool RunOpen(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return ProcessHelper.StartBackground("open", ProcessHelper.Quote(path)) != null;
    }

#elif UNITY_STANDALONE_LINUX
    public static bool OpenUrl(string url) => RunXdgOpen(url);
    public static bool OpenFile(string filePath) => RunXdgOpen(filePath);
    public static bool OpenFolder(string folderPath, string filePath = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return false;
        if (!string.IsNullOrEmpty(filePath))
        {
            var dir = Path.GetDirectoryName(filePath);
            return RunXdgOpen(!string.IsNullOrEmpty(dir) ? dir : folderPath);
        }
        return RunXdgOpen(folderPath);
    }
    public static bool OpenFolderInExplorer(string folderPath) => OpenFolder(folderPath, null);
    public static bool Execute(string operation, string filePath, string parameters = null, string workingDir = null) => RunXdgOpen(filePath);

    private static bool RunXdgOpen(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return ProcessHelper.StartBackground("xdg-open", ProcessHelper.Quote(path)) != null;
    }

#else
    public static bool OpenUrl(string url) => false;
    public static bool OpenFile(string filePath) => false;
    public static bool OpenFolder(string folderPath, string filePath = null) => false;
    public static bool OpenFolderInExplorer(string folderPath) => false;
    public static bool Execute(string operation, string filePath, string parameters = null, string workingDir = null) => false;
#endif
}
