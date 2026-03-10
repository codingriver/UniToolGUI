using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 单实例检测：确保应用只运行一个实例（Win/Mac/Linux）
/// </summary>
public static class WindowsSingleInstance
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateMutex(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReleaseMutex(IntPtr hMutex);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetLastError();

    private const uint ERROR_ALREADY_EXISTS = 183;
    private static IntPtr _mutexHandle = IntPtr.Zero;

    public static bool TryAcquire(string mutexName = null)
    {
        if (string.IsNullOrEmpty(mutexName))
            mutexName = "Global\\" + (Application.productName ?? "UnityApp");
        else if (!mutexName.StartsWith("Global\\") && !mutexName.StartsWith("Local\\"))
            mutexName = "Global\\" + mutexName;

        _mutexHandle = CreateMutex(IntPtr.Zero, true, mutexName);
        if (_mutexHandle == IntPtr.Zero) return true;
        return GetLastError() != ERROR_ALREADY_EXISTS;
    }

    public static void Release()
    {
        if (_mutexHandle != IntPtr.Zero)
        {
            ReleaseMutex(_mutexHandle);
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
        }
    }

    public static bool IsAnotherInstanceRunning(string mutexName = null)
    {
        if (string.IsNullOrEmpty(mutexName))
            mutexName = "Global\\" + (Application.productName ?? "UnityApp");
        else if (!mutexName.StartsWith("Global\\") && !mutexName.StartsWith("Local\\"))
            mutexName = "Global\\" + mutexName;

        IntPtr h = CreateMutex(IntPtr.Zero, false, mutexName);
        if (h == IntPtr.Zero) return false;
        bool exists = GetLastError() == ERROR_ALREADY_EXISTS;
        CloseHandle(h);
        return exists;
    }

#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    private static FileStream _lockFile;

    private static string GetLockPath(string mutexName)
    {
        var name = mutexName ?? Application.productName ?? "UnityApp";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return Path.Combine(Path.GetTempPath(), "." + name + ".lock");
    }

    public static bool TryAcquire(string mutexName = null)
    {
        var lockPath = GetLockPath(mutexName);
        try
        {
            _lockFile = new FileStream(lockPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static void Release()
    {
        if (_lockFile != null)
        {
            try { _lockFile.Close(); _lockFile.Dispose(); } catch { }
            _lockFile = null;
        }
    }

    public static bool IsAnotherInstanceRunning(string mutexName = null)
    {
        var lockPath = GetLockPath(mutexName);
        try
        {
            using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                return false;
        }
        catch { return true; }
    }

#else
    public static bool TryAcquire(string mutexName = null) => true;
    public static void Release() { }
    public static bool IsAnotherInstanceRunning(string mutexName = null) => false;
#endif
}
