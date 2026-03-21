using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// 原生消息框封装（Win/Mac/Linux）
/// </summary>
public static class WindowsMessageBox
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static int Show(IntPtr hWnd, string text, string caption, int buttons = 0, int icon = 0)
    {
        uint type = (uint)(buttons | icon);
        return MessageBox(hWnd, text, caption, type);
    }

    public static void Info(string text, string caption = "信息")
    {
        Show(IntPtr.Zero, text, caption, 0, 64);
    }

    public static void Warning(string text, string caption = "警告")
    {
        Show(IntPtr.Zero, text, caption, 0, 48);
    }

    public static void Error(string text, string caption = "错误")
    {
        Show(IntPtr.Zero, text, caption, 0, 16);
    }

    public static bool Confirm(string text, string caption = "确认")
    {
        int result = Show(IntPtr.Zero, text, caption, 1, 32);
        return result == 1;
    }

    public static bool YesNo(string text, string caption = "请选择")
    {
        int result = Show(IntPtr.Zero, text, caption, 4, 32);
        return result == 6;
    }

    public static void Info(IntPtr hWnd, string text, string caption = "信息")
    {
        Show(hWnd, text, caption, 0, 64);
    }

    public static bool Confirm(IntPtr hWnd, string text, string caption = "确认")
    {
        return Show(hWnd, text, caption, 1, 32) == 1;
    }

#elif UNITY_STANDALONE_OSX
    private static string EscapeAppleScript(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\r\n", "\\n")
                .Replace("\r", "\\n")
                .Replace("\n", "\\n")
                .Replace("\"", "\\\"");
    }

    private static int RunOsascript(string script)
    {
        return RunOsascriptWithResult(script) != null ? 1 : 2;
    }

    private static string RunOsascriptGetButton(string script)
    {
        return RunOsascriptWithResult(script);
    }

    private static string RunOsascriptWithResult(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = "-",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = System.Text.Encoding.UTF8,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                p.StandardInput.Write(script);
                p.StandardInput.Close();

                var output = p.StandardOutput.ReadToEnd().Trim();
                var error = p.StandardError.ReadToEnd().Trim();
                p.WaitForExit(30000);

                if (p.ExitCode != 0)
                {
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogWarning($"[MessageBox] {error}");
                    return null;
                }

                return string.IsNullOrEmpty(output) ? string.Empty : output;
            }
        }
        catch (Exception ex) { Debug.LogWarning($"[MessageBox] {ex.Message}"); return null; }
    }

    public static int Show(IntPtr hWnd, string text, string caption, int buttons = 0, int icon = 0)
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        RunOsascript($"display dialog \"{t}\" with title \"{c}\" buttons {{\"OK\"}} default button 1");
        return 1;
    }

    public static void Info(string text, string caption = "信息")
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        RunOsascript($"display dialog \"{t}\" with title \"{c}\" buttons {{\"OK\"}} default button 1 with icon note");
    }

    public static void Warning(string text, string caption = "警告")
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        RunOsascript($"display dialog \"{t}\" with title \"{c}\" buttons {{\"OK\"}} default button 1 with icon caution");
    }

    public static void Error(string text, string caption = "错误")
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        RunOsascript($"display dialog \"{t}\" with title \"{c}\" buttons {{\"OK\"}} default button 1 with icon stop");
    }

    public static bool Confirm(string text, string caption = "确认")
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        var result = RunOsascriptGetButton($"set r to button returned of (display dialog \"{t}\" with title \"{c}\" buttons {{\"Cancel\", \"OK\"}} default button 2)\nreturn r");
        return result == "OK";
    }

    public static bool YesNo(string text, string caption = "请选择")
    {
        var t = EscapeAppleScript(text);
        var c = EscapeAppleScript(caption);
        var result = RunOsascriptGetButton($"set r to button returned of (display dialog \"{t}\" with title \"{c}\" buttons {{\"No\", \"Yes\"}} default button 2)\nreturn r");
        return result == "Yes";
    }

    public static void Info(IntPtr hWnd, string text, string caption = "信息") => Info(text, caption);
    public static bool Confirm(IntPtr hWnd, string text, string caption = "确认") => Confirm(text, caption);

#elif UNITY_STANDALONE_LINUX
    private static string Esc(string s) => (s ?? "").Replace("\"", "\\\"");

    public static int Show(IntPtr hWnd, string text, string caption, int buttons = 0, int icon = 0)
    {
        ProcessHelper.Run("zenity", $"--info --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\"", 30000);
        return 1;
    }

    public static void Info(string text, string caption = "信息")
    {
        ProcessHelper.Run("zenity", $"--info --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\"", 30000);
    }

    public static void Warning(string text, string caption = "警告")
    {
        ProcessHelper.Run("zenity", $"--warning --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\"", 30000);
    }

    public static void Error(string text, string caption = "错误")
    {
        ProcessHelper.Run("zenity", $"--error --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\"", 30000);
    }

    public static bool Confirm(string text, string caption = "确认")
    {
        return ProcessHelper.Run("zenity", $"--question --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\"", 30000) == 0;
    }

    public static bool YesNo(string text, string caption = "请选择")
    {
        return ProcessHelper.Run("zenity", $"--question --text=\"{Esc(text)}\" --title=\"{Esc(caption)}\" --ok-label=Yes --cancel-label=No", 30000) == 0;
    }

    public static void Info(IntPtr hWnd, string text, string caption = "信息") => Info(text, caption);
    public static bool Confirm(IntPtr hWnd, string text, string caption = "确认") => Confirm(text, caption);

#else
    public static int Show(IntPtr hWnd, string text, string caption, int buttons = 0, int icon = 0) { Debug.Log($"[{caption}] {text}"); return 1; }
    public static void Info(string text, string caption = "信息") { Debug.Log($"[{caption}] {text}"); }
    public static void Warning(string text, string caption = "警告") { Debug.LogWarning($"[{caption}] {text}"); }
    public static void Error(string text, string caption = "错误") { Debug.LogError($"[{caption}] {text}"); }
    public static bool Confirm(string text, string caption = "确认") { Debug.Log($"[{caption}] {text}"); return true; }
    public static bool YesNo(string text, string caption = "请选择") { Debug.Log($"[{caption}] {text}"); return true; }
    public static void Info(IntPtr hWnd, string text, string caption = "信息") => Info(text, caption);
    public static bool Confirm(IntPtr hWnd, string text, string caption = "确认") => Confirm(text, caption);
#endif
}
