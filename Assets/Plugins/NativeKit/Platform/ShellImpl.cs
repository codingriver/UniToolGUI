using UnityEngine;

/// <summary>空实现 Shell（不支持的平台）。</summary>
public class NullShellService : IShellService
{
    public static readonly NullShellService Instance = new NullShellService();
    public bool OpenUrl(string url) => false;
    public bool OpenFile(string filePath) => false;
    public bool OpenFolder(string folderPath, string filePath = null) => false;
}

/// <summary>原生 Shell 实现（委托给 WindowsShell / xdg-open / open）。</summary>
public class NativeShellService : IShellService
{
    public static readonly NativeShellService Instance = new NativeShellService();

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ProcessHelper.Run("cmd", $"/c start \"\" {ProcessHelper.Quote(url)}") == 0;
#elif UNITY_STANDALONE_OSX
        return ProcessHelper.Run("open", ProcessHelper.Quote(url)) == 0;
#elif UNITY_STANDALONE_LINUX
        return ProcessHelper.Run("xdg-open", ProcessHelper.Quote(url)) == 0;
#else
        Application.OpenURL(url); return true;
#endif
    }

    public bool OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return ProcessHelper.Run("explorer", ProcessHelper.Quote(filePath)) == 0;
#elif UNITY_STANDALONE_OSX
        return ProcessHelper.Run("open", ProcessHelper.Quote(filePath)) == 0;
#elif UNITY_STANDALONE_LINUX
        return ProcessHelper.Run("xdg-open", ProcessHelper.Quote(filePath)) == 0;
#else
        return false;
#endif
    }

    public bool OpenFolder(string folderPath, string filePath = null)
    {
        if (string.IsNullOrEmpty(folderPath)) return false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!string.IsNullOrEmpty(filePath))
            return ProcessHelper.Run("explorer", $"/select,{ProcessHelper.Quote(filePath)}") == 0;
        return ProcessHelper.Run("explorer", ProcessHelper.Quote(folderPath)) == 0;
#elif UNITY_STANDALONE_OSX
        if (!string.IsNullOrEmpty(filePath))
            return ProcessHelper.Run("open", $"-R {ProcessHelper.Quote(filePath)}") == 0;
        return ProcessHelper.Run("open", ProcessHelper.Quote(folderPath)) == 0;
#elif UNITY_STANDALONE_LINUX
        return ProcessHelper.Run("xdg-open", ProcessHelper.Quote(folderPath)) == 0;
#else
        return false;
#endif
    }
}
