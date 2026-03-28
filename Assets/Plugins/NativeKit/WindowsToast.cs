using System;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

/// <summary>
/// 系统 Toast 通知（Win/Mac/Linux）。
/// Windows: 通过内联 PowerShell 命令调用 WinRT API，无需写临时文件。
/// Mac: osascript display notification。
/// Linux: notify-send。
/// </summary>
public static class WindowsToast
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // AppUserModelID — 使用 Unity 应用名，避免通知被归入"未知应用"
    private static string AppId => string.IsNullOrEmpty(UnityEngine.Application.productName)
        ? "Unity.Application"
        : UnityEngine.Application.productName.Replace(' ', '.');

    /// <summary>
    /// 显示系统 Toast 通知。
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="message">正文</param>
    /// <param name="imagePath">可选图片路径（本地文件）</param>
    /// <returns>是否成功</returns>
    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title))   title   = " ";
        if (string.IsNullOrEmpty(message)) message = " ";

        var xmlTitle   = EscapeXml(title);
        var xmlMessage = EscapeXml(message);

        var xml = new StringBuilder();
        xml.Append("<toast><visual><binding template=\"ToastGeneric\">");
        xml.Append("<text>").Append(xmlTitle).Append("</text>");
        xml.Append("<text>").Append(xmlMessage).Append("</text>");
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            var img = imagePath.Replace("\\", "/").Replace(":", "%3A");
            xml.Append("<image src=\"file:///").Append(img).Append("\" placement=\"appLogoOverride\"/>");
        }
        xml.Append("</binding></visual></toast>");

        var xmlEscaped = xml.ToString().Replace("'", "''");
        var appIdEscaped = AppId.Replace("'", "''");

        var psCommand =
            "Add-Type -AssemblyName System.Runtime.WindowsRuntime; " +
            "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime] | Out-Null; " +
            "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType=WindowsRuntime] | Out-Null; " +
            $"$xml = New-Object Windows.Data.Xml.Dom.XmlDocument; $xml.LoadXml('{xmlEscaped}'); " +
            $"$toast = [Windows.UI.Notifications.ToastNotification]::new($xml); " +
            $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{appIdEscaped}').Show($toast)";

        int code = RunPowerShell(psCommand, 8000);

        if (code != 0)
            Debug.LogWarning($"[WindowsToast] 显示失败，powershell 返回码: {code}");

        return code == 0;
    }

    private static int RunPowerShell(string script, int timeoutMs)
    {
        try
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + EncodePowerShell(script),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                bool exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    Debug.LogWarning("[WindowsToast] PowerShell 执行超时");
                    return -1;
                }

                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Debug.LogWarning("[WindowsToast] stdout: " + stdout.Trim());
                    if (!string.IsNullOrWhiteSpace(stderr))
                        Debug.LogWarning("[WindowsToast] stderr: " + stderr.Trim());
                }

                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WindowsToast] 启动 powershell 失败: " + ex.Message);
            return -1;
        }
    }

    private static string EncodePowerShell(string script)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }

    private static string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
    }

#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    /// <summary>
    /// 显示系统通知（osascript）。Mac 不支持 imagePath。
    /// </summary>
    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title))   title   = " ";
        if (string.IsNullOrEmpty(message)) message = " ";

        var t = EscAS(title);
        var m = EscAS(message);
        var script = $"display notification \"{m}\" with title \"{t}\"";
        int code = RunOsascriptInline(script, 5000);
        return code == 0;
    }

    private static int RunOsascriptInline(string script, int timeoutMs)
    {
        try
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = "-",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = Encoding.UTF8,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                process.Start();
                process.StandardInput.Write(script);
                process.StandardInput.Close();

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                bool exited = process.WaitForExit(timeoutMs);
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    Debug.LogWarning("[WindowsToast] osascript 执行超时");
                    return -1;
                }

                if (process.ExitCode != 0)
                {
                    if (!string.IsNullOrWhiteSpace(stdout))
                        Debug.LogWarning("[WindowsToast] stdout: " + stdout.Trim());
                    if (!string.IsNullOrWhiteSpace(stderr))
                        Debug.LogWarning("[WindowsToast] stderr: " + stderr.Trim());
                }

                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WindowsToast] 启动 osascript 失败: " + ex.Message);
            return -1;
        }
    }

    private static string EscAS(string s)
        => (s ?? "")
            .Replace("\\", "\\\\")
            .Replace("\r\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("\n", "\\n")
            .Replace("\"", "\\\"");

#elif UNITY_STANDALONE_LINUX
    /// <summary>
    /// 显示系统通知（notify-send）。
    /// </summary>
    public static bool Show(string title, string message, string imagePath = null)
    {
        if (string.IsNullOrEmpty(title))   title   = " ";
        if (string.IsNullOrEmpty(message)) message = " ";

        var args = new StringBuilder();
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            args.Append($"-i {ProcessHelper.Quote(imagePath)} ");
        args.Append($"{ProcessHelper.Quote(title)} {ProcessHelper.Quote(message)}");

        int code = ProcessHelper.Run("notify-send", args.ToString(), 5000);
        return code == 0;
    }

#else
    /// <summary>其他平台：无操作。</summary>
    public static bool Show(string title, string message, string imagePath = null) => false;
#endif
}
