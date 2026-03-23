using UnityEngine;

/// <summary>
/// 跨平台原生功能统一入口（门面类）。
///
/// 用法示例：
///   NativePlatform.FileDialog.OpenFilePanel("选择文件", filter);
///   NativePlatform.Toast.Show("标题", "内容");
///   NativePlatform.Tray.Initialize();
///
/// 在 Unity Editor 或不支持的平台上，所有操作均为安全的空实现。
/// </summary>
public static class NativePlatform
{
    // ── 懒加载单例字段 ──────────────────────────────────────────────────
    private static IFileDialog      _fileDialog;
    private static IClipboard       _clipboard;
    private static IToastService    _toast;
    private static IMessageBox      _messageBox;
    private static IShellService    _shell;
    private static ISingleInstance  _singleInstance;
    private static ITrayService     _tray;

    // ── 公共访问器 ───────────────────────────────────────────────────────

    /// <summary>文件/文件夹选择对话框</summary>
    public static IFileDialog FileDialog
        => _fileDialog ?? (_fileDialog = CreateFileDialog());

    /// <summary>剪贴板读写</summary>
    public static IClipboard Clipboard
        => _clipboard ?? (_clipboard = CreateClipboard());

    /// <summary>系统 Toast / 桌面通知</summary>
    public static IToastService Toast
        => _toast ?? (_toast = CreateToast());

    /// <summary>原生消息框</summary>
    public static IMessageBox MessageBox
        => _messageBox ?? (_messageBox = CreateMessageBox());

    /// <summary>Shell 操作（打开 URL / 文件 / 文件夹）</summary>
    public static IShellService Shell
        => _shell ?? (_shell = CreateShell());

    /// <summary>单实例检测</summary>
    public static ISingleInstance SingleInstance
        => _singleInstance ?? (_singleInstance = CreateSingleInstance());

    /// <summary>系统托盘（使用前须调用 Tray.Initialize()）</summary>
    public static ITrayService Tray
        => _tray ?? (_tray = CreateTray());

    // ── 当前平台 ─────────────────────────────────────────────────────────

    /// <summary>当前运行平台</summary>
    public static RuntimePlatform Platform => Application.platform;

    public static bool IsWindows =>
        Application.platform == RuntimePlatform.WindowsPlayer ||
        Application.platform == RuntimePlatform.WindowsEditor;

    public static bool IsMacOS =>
        Application.platform == RuntimePlatform.OSXPlayer ||
        Application.platform == RuntimePlatform.OSXEditor;

    public static bool IsLinux =>
        Application.platform == RuntimePlatform.LinuxPlayer ||
        Application.platform == RuntimePlatform.LinuxEditor;

    // ── 工厂方法 ─────────────────────────────────────────────────────────

    private static IFileDialog CreateFileDialog()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        return NativeFileDialog.Instance;
#else
        Debug.LogWarning("[NativePlatform] FileDialog: 当前平台不支持原生对话框，使用空实现");
        return NullFileDialog.Instance;
#endif
    }

    private static IClipboard CreateClipboard()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        return NativeClipboard.Instance;
#else
        return NullClipboard.Instance;
#endif
    }

    private static IToastService CreateToast()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        return NativeToastService.Instance;
#else
        return NullToastService.Instance;
#endif
    }

    private static IMessageBox CreateMessageBox()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        return NativeMessageBox.Instance;
#else
        return NullMessageBox.Instance;
#endif
    }

    private static IShellService CreateShell()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        return NativeShellService.Instance;
#else
        return NullShellService.Instance;
#endif
    }

    private static ISingleInstance CreateSingleInstance()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        return NativeSingleInstance.Instance;
#else
        return NullSingleInstance.Instance;
#endif
    }

    private static ITrayService CreateTray()
    {
        // TrayIconService 自身已实现 ITrayService（通过 #if 区分平台）
        // 直接返回其单例
        return TrayIconService.Instance;
    }

    // ── 便捷静态方法（最高频操作的快捷入口） ────────────────────────────

    /// <summary>显示 Toast 通知</summary>
    public static bool ShowToast(string title, string message, string imagePath = null)
        => Toast.Show(title, message, imagePath);

    /// <summary>打开文件选择框</summary>
    public static string OpenFile(string title, string filter, string ext = null, string dir = null)
        => FileDialog.OpenFilePanel(title, filter, ext, dir);

    /// <summary>打开文件夹选择框</summary>
    public static string OpenFolder(string title, string dir = null)
        => FileDialog.OpenFolderPanel(title, dir);

    /// <summary>打开 URL</summary>
    public static bool OpenUrl(string url)
        => Shell.OpenUrl(url);

    /// <summary>获取剪贴板文本</summary>
    public static string GetClipboard()
        => Clipboard.GetText();

    /// <summary>设置剪贴板文本</summary>
    public static bool SetClipboard(string text)
        => Clipboard.SetText(text);
}
