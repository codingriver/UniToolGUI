using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 单实例检测：确保应用只运行一个实例（Win/Mac/Linux）。
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
    private static uint _lastError = 0;

    /// <summary>
    /// 尝试获取单实例锁。返回 false 表示已有实例在运行。
    /// </summary>
    public static bool TryAcquire(string mutexName = null)
    {
        mutexName = NormalizeMutexName(mutexName);
        _mutexHandle = AcquireMutex(mutexName, true);
        if (_mutexHandle == IntPtr.Zero)
        {
            Debug.LogWarning("[SingleInstance] CreateMutex failed, error=" + GetLastError());
            return false;
        }
        return _lastError != ERROR_ALREADY_EXISTS;
    }

    /// <summary>释放单实例锁。</summary>
    public static void Release()
    {
        if (_mutexHandle != IntPtr.Zero)
        {
            ReleaseMutex(_mutexHandle);
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
        }
    }

    /// <summary>仅检测是否有另一个实例，不持有锁。</summary>
    public static bool IsAnotherInstanceRunning(string mutexName = null)
    {
        mutexName = NormalizeMutexName(mutexName);
        IntPtr h = AcquireMutex(mutexName, false);
        if (h == IntPtr.Zero) return false;
        bool exists = _lastError == ERROR_ALREADY_EXISTS;
        CloseHandle(h);
        return exists;
    }

    private static string NormalizeMutexName(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = Application.productName ?? "UnityApp";
        // 优先使用 Local\ 前缀（无需特殊权限，同一登录会话内唯一即可）
        // Global\ 需要 SeCreateGlobalPrivilege，普通用户在部分 Windows 版本下会失败
        if (!name.StartsWith("Global\\", StringComparison.Ordinal) &&
            !name.StartsWith("Local\\",  StringComparison.Ordinal))
            name = "Local\\" + name;
        return name;
    }

    /// <summary>
    /// 尝试获取单实例锁，先用 Local\ 前缀，失败时 fallback 到无前缀。
    /// CreateMutex 返回 IntPtr.Zero 表示系统错误，此时按保守策略视为已有实例（而非放行）。
    /// </summary>
    private static IntPtr AcquireMutex(string mutexName, bool initialOwner)
    {
        IntPtr h = CreateMutex(IntPtr.Zero, initialOwner, mutexName);
        _lastError = GetLastError(); // capture immediately before any other call clobbers it
        if (h != IntPtr.Zero) return h;
        // fallback: strip prefix and try bare name
        string bare = mutexName;
        if (bare.StartsWith("Local\\",  StringComparison.Ordinal)) bare = bare.Substring(6);
        if (bare.StartsWith("Global\\", StringComparison.Ordinal)) bare = bare.Substring(7);
        h = CreateMutex(IntPtr.Zero, initialOwner, bare);
        _lastError = GetLastError();
        return h;
    }

#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
    private static FileStream _lockFile;
    private static string     _lockPath;

    /// <summary>
    /// 尝试获取单实例锁。返回 false 表示已有实例在运行。
    /// </summary>
    public static bool TryAcquire(string mutexName = null)
    {
        _lockPath = GetLockPath(mutexName);
        try
        {
            var dir = Path.GetDirectoryName(_lockPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _lockFile = new FileStream(
                _lockPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsSingleInstance] TryAcquire 失败: {ex.Message}");
            return true;
        }
    }

    /// <summary>释放单实例锁。</summary>
    public static void Release()
    {
        if (_lockFile != null)
        {
            try { _lockFile.Close(); _lockFile.Dispose(); } catch { }
            _lockFile = null;
        }
        if (!string.IsNullOrEmpty(_lockPath))
        {
            try { File.Delete(_lockPath); } catch { }
            _lockPath = null;
        }
    }

    /// <summary>仅检测是否有另一个实例，不持有锁。</summary>
    public static bool IsAnotherInstanceRunning(string mutexName = null)
    {
        var path = GetLockPath(mutexName);
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                return false;
        }
        catch (FileNotFoundException) { return false; }
        catch (IOException)           { return true;  }
        catch                         { return false; }
    }

    /// <summary>
    /// 锁文件存储在用户私有目录（macOS: ~/Library/Caches，Linux: ~/.cache），
    /// 避免使用 /tmp（可能被系统清理或跨用户共享）。
    /// </summary>
    private static string GetLockPath(string mutexName)
    {
        var name = (mutexName ?? Application.productName ?? "UnityApp");
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Replace(' ', '_');

#if UNITY_STANDALONE_OSX
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Caches", name);
#else
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", name);
#endif
        return Path.Combine(cacheDir, ".instance.lock");
    }

#else
    public static bool TryAcquire(string mutexName = null) => true;
    public static void Release() { }
    public static bool IsAnotherInstanceRunning(string mutexName = null) => false;
#endif
}
