using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 系统 Toast 通知（Win/Mac/Linux）
/// </summary>
public static class WindowsToast
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const string AppId = "Unity.Application";

    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title)) title = " ";
        if (string.IsNullOrEmpty(message)) message = " ";
        title = EscapeXml(title);
        message = EscapeXml(message);

        var xml = new StringBuilder();
        xml.Append("<toast><visual><binding template=\"ToastGeneric\">");
        xml.Append("<text>").Append(title).Append("</text>");
        xml.Append("<text>").Append(message).Append("</text>");
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            var img = imagePath.Replace("\\", "/").Replace(":", "%3A");
            xml.Append("<image src=\"file:///").Append(img).Append("\" placement=\"appLogoOverride\"/>");
        }
        xml.Append("</binding></visual></toast>");

        var tempFile = Path.Combine(Path.GetTempPath(), "toast_" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            var script = $@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml('{xml.ToString().Replace("'", "''")}')
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{AppId}').Show($toast)
";
            File.WriteAllText(tempFile, script, Encoding.UTF8);
            return ProcessHelper.Run("powershell", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempFile}\"", 8000) == 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsToast] 显示失败: {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

#elif UNITY_STANDALONE_OSX
    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title)) title = " ";
        if (string.IsNullOrEmpty(message)) message = " ";
        var t = title.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var m = message.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var script = $"display notification \"{m}\" with title \"{t}\"";
        return ProcessHelper.Run("osascript", $"-e '{script}'", 3000) == 0;
    }

#elif UNITY_STANDALONE_LINUX
    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title)) title = " ";
        if (string.IsNullOrEmpty(message)) message = " ";
        var args = $"{ProcessHelper.Quote(title)} {ProcessHelper.Quote(message)}";
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            args = $"-i {ProcessHelper.Quote(imagePath)} {args}";
        return ProcessHelper.Run("notify-send", args, 3000) == 0;
    }

#else
    public static bool Show(string title, string message, string imagePath = null) => false;
#endif
}
