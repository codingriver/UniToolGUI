using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 系统信息（Win 使用 Win32 API，Mac/Linux 使用系统命令 + Unity API）
/// </summary>
public static class WindowsSystemInfo
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved1;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public static (int width, int height) GetPrimaryScreenSize()
    {
        return (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
    }

    public static (int width, int height) GetVirtualScreenSize()
    {
        return (GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));
    }

    public static (bool onAC, int percent, int remainingSeconds) GetBatteryInfo()
    {
        if (!GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
            return (true, 100, -1);
        bool onAC = status.ACLineStatus == 1;
        int percent = status.BatteryLifePercent;
        if (percent > 100) percent = 100;
        return (onAC, percent, status.BatteryLifeTime);
    }

    public static uint GetIdleTimeSeconds()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (!GetLastInputInfo(ref lii)) return 0;
        return (uint)((Environment.TickCount - lii.dwTime) / 1000);
    }

    public static string GetComputerName() => Environment.MachineName;

    public static string GetUserName() => Environment.UserName;

#elif UNITY_STANDALONE_OSX
    public static (int width, int height) GetPrimaryScreenSize()
    {
        return (Screen.currentResolution.width, Screen.currentResolution.height);
    }

    public static (int width, int height) GetVirtualScreenSize()
    {
        int totalW = 0, maxH = 0;
        foreach (var d in Display.displays)
        {
            totalW += d.systemWidth;
            if (d.systemHeight > maxH) maxH = d.systemHeight;
        }
        if (totalW == 0) return GetPrimaryScreenSize();
        return (totalW, maxH);
    }

    public static (bool onAC, int percent, int remainingSeconds) GetBatteryInfo()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("pmset", "-g batt", 3000);
            if (string.IsNullOrEmpty(output)) return (true, 100, -1);
            bool onAC = output.Contains("AC Power");
            int percent = -1;
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
            if (match.Success) percent = int.Parse(match.Groups[1].Value);
            int remaining = -1;
            var timeMatch = System.Text.RegularExpressions.Regex.Match(output, @"(\d+):(\d+) remaining");
            if (timeMatch.Success)
                remaining = int.Parse(timeMatch.Groups[1].Value) * 3600 + int.Parse(timeMatch.Groups[2].Value) * 60;
            return (onAC, percent >= 0 ? percent : 100, remaining);
        }
        catch { return (true, 100, -1); }
    }

    public static uint GetIdleTimeSeconds()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("ioreg", "-c IOHIDSystem -d 4 -S", 3000);
            if (string.IsNullOrEmpty(output)) return 0;
            var match = System.Text.RegularExpressions.Regex.Match(output, @"HIDIdleTime.*?=\s*(\d+)");
            if (match.Success)
            {
                long nanos = long.Parse(match.Groups[1].Value);
                return (uint)(nanos / 1000000000);
            }
        }
        catch { }
        return 0;
    }

    public static string GetComputerName() => Environment.MachineName;

    public static string GetUserName() => Environment.UserName;

#elif UNITY_STANDALONE_LINUX
    public static (int width, int height) GetPrimaryScreenSize()
    {
        return (Screen.currentResolution.width, Screen.currentResolution.height);
    }

    public static (int width, int height) GetVirtualScreenSize()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("xrandr", "--current", 3000);
            if (!string.IsNullOrEmpty(output))
            {
                var match = System.Text.RegularExpressions.Regex.Match(output, @"current (\d+) x (\d+)");
                if (match.Success)
                    return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }
        }
        catch { }
        return GetPrimaryScreenSize();
    }

    public static (bool onAC, int percent, int remainingSeconds) GetBatteryInfo()
    {
        try
        {
            string basePath = "/sys/class/power_supply/";
            if (System.IO.Directory.Exists(basePath))
            {
                foreach (var dir in System.IO.Directory.GetDirectories(basePath))
                {
                    var typePath = System.IO.Path.Combine(dir, "type");
                    if (!System.IO.File.Exists(typePath)) continue;
                    var type = System.IO.File.ReadAllText(typePath).Trim();
                    if (type != "Battery") continue;

                    var statusPath = System.IO.Path.Combine(dir, "status");
                    var capacityPath = System.IO.Path.Combine(dir, "capacity");
                    bool onAC = true;
                    if (System.IO.File.Exists(statusPath))
                    {
                        var status = System.IO.File.ReadAllText(statusPath).Trim();
                        onAC = status != "Discharging";
                    }
                    int percent = 100;
                    if (System.IO.File.Exists(capacityPath))
                        int.TryParse(System.IO.File.ReadAllText(capacityPath).Trim(), out percent);
                    return (onAC, percent, -1);
                }
            }
        }
        catch { }
        return (true, 100, -1);
    }

    public static uint GetIdleTimeSeconds()
    {
        try
        {
            var output = ProcessHelper.RunAndRead("xprintidle", "", 2000);
            if (!string.IsNullOrEmpty(output) && uint.TryParse(output.Trim(), out uint ms))
                return ms / 1000;
        }
        catch { }
        return 0;
    }

    public static string GetComputerName() => Environment.MachineName;

    public static string GetUserName() => Environment.UserName;

#else
    public static (int width, int height) GetPrimaryScreenSize() => (Screen.width, Screen.height);
    public static (int width, int height) GetVirtualScreenSize() => (Screen.width, Screen.height);
    public static (bool onAC, int percent, int remainingSeconds) GetBatteryInfo() => (true, 100, -1);
    public static uint GetIdleTimeSeconds() => 0;
    public static string GetComputerName() => Environment.MachineName;
    public static string GetUserName() => Environment.UserName;
#endif
}
