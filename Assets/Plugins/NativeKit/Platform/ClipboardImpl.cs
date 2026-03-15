using UnityEngine;

/// <summary>空实现剪贴板（不支持的平台）。</summary>
public class NullClipboard : IClipboard
{
    public static readonly NullClipboard Instance = new NullClipboard();
    public string GetText() => null;
    public bool   SetText(string text) => false;
    public bool   HasText() => false;
}

/// <summary>剪贴板实现（委托给 GUIUtility / 系统剪贴板）。</summary>
public class NativeClipboard : IClipboard
{
    public static readonly NativeClipboard Instance = new NativeClipboard();

    public string GetText()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return WindowsClipboard.GetText();
#else
        return GUIUtility.systemCopyBuffer;
#endif
    }

    public bool SetText(string text)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return WindowsClipboard.SetText(text);
#else
        GUIUtility.systemCopyBuffer = text ?? "";
        return true;
#endif
    }

    public bool HasText() => !string.IsNullOrEmpty(GetText());
}
