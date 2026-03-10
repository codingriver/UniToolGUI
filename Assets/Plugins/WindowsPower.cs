using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// 电源/休眠控制（Win/Mac/Linux）
/// </summary>
public static class WindowsPower
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    public static void PreventSleep()
    {
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
    }

    public static void PreventSleepAndDisplay()
    {
        SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
    }

    public static void AllowSleep()
    {
        SetThreadExecutionState(ES_CONTINUOUS);
    }

    public static void ResetIdleTimer()
    {
        SetThreadExecutionState(ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
    }

#elif UNITY_STANDALONE_OSX
    private static Process _caffeinateProcess;
    private static readonly object _lock = new object();

    public static void PreventSleep()
    {
        lock (_lock)
        {
            try
            {
                KillProcessSafe(ref _caffeinateProcess);
                _caffeinateProcess = ProcessHelper.StartBackground("caffeinate", "-i");
            }
            catch (Exception ex) { Debug.LogWarning($"[WindowsPower] PreventSleep: {ex.Message}"); }
        }
    }

    public static void PreventSleepAndDisplay()
    {
        lock (_lock)
        {
            try
            {
                KillProcessSafe(ref _caffeinateProcess);
                _caffeinateProcess = ProcessHelper.StartBackground("caffeinate", "-i -s");
            }
            catch (Exception ex) { Debug.LogWarning($"[WindowsPower] PreventSleepAndDisplay: {ex.Message}"); }
        }
    }

    public static void AllowSleep()
    {
        lock (_lock) { KillProcessSafe(ref _caffeinateProcess); }
    }

    public static void ResetIdleTimer()
    {
        ProcessHelper.StartBackground("caffeinate", "-i -t 1");
    }

    private static void KillProcessSafe(ref Process p)
    {
        if (p != null)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            p = null;
        }
    }

#elif UNITY_STANDALONE_LINUX
    private static Process _inhibitProcess;
    private static readonly object _lock = new object();

    public static void PreventSleep()
    {
        lock (_lock)
        {
            try
            {
                KillProcessSafe(ref _inhibitProcess);
                _inhibitProcess = ProcessHelper.StartBackground("systemd-inhibit", "--what=idle:sleep --who=Unity --why=PreventSleep sleep infinity");
            }
            catch (Exception ex) { Debug.LogWarning($"[WindowsPower] PreventSleep: {ex.Message}"); }
        }
    }

    public static void PreventSleepAndDisplay()
    {
        PreventSleep();
    }

    public static void AllowSleep()
    {
        lock (_lock) { KillProcessSafe(ref _inhibitProcess); }
    }

    public static void ResetIdleTimer()
    {
        ProcessHelper.StartBackground("xdg-screensaver", "reset");
    }

    private static void KillProcessSafe(ref Process p)
    {
        if (p != null)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            p = null;
        }
    }

#else
    public static void PreventSleep() { }
    public static void PreventSleepAndDisplay() { }
    public static void AllowSleep() { }
    public static void ResetIdleTimer() { }
#endif
}
