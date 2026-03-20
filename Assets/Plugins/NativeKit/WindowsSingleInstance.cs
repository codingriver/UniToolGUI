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
        string rawName = mutexName;
        mutexName = NormalizeMutexName(mutexName);
        Debug.Log(string.Format("[SingleInstance] TryAcquire 开始 | rawName={0} | normalizedName={1}", rawName, mutexName));
        _mutexHandle = AcquireMutex(mutexName, true);
        uint err = _lastError;
        Debug.Log(string.Format("[SingleInstance] CreateMutex 结果 | handle=0x{0:X} | lastError={1} | ERROR_ALREADY_EXISTS={2}",
            _mutexHandle.ToInt64(), err, ERROR_ALREADY_EXISTS));
        if (_mutexHandle == IntPtr.Zero)
        {
            // handle 为零 = 系统级错误（权限/名称非法等），保守策略：视为已有实例
            Debug.LogError(string.Format("[SingleInstance] CreateMutex 返回 IntPtr.Zero，Win32 错误码={0}，保守策略视为已有实例运行", err));
            return false;
        }
        if (err == ERROR_ALREADY_EXISTS)
        {
            Debug.LogWarning("[SingleInstance] 检测到已有实例（ERROR_ALREADY_EXISTS=183），TryAcquire 返回 false");
            return false;
        }
        Debug.Log("[SingleInstance] 单实例锁获取成功，当前进程为第一个实例");
        return true;
    }

    /// <summary>释放单实例锁。</summary>
    public static void Release()
    {
        if (_mutexHandle != IntPtr.Zero)
        {
            Debug.Log(string.Format("[SingleInstance] Release | handle=0x{0:X}", _mutexHandle.ToInt64()));
            ReleaseMutex(_mutexHandle);
            CloseHandle(_mutexHandle);
            _mutexHandle = IntPtr.Zero;
            Debug.Log("[SingleInstance] 单实例锁已释放");
        }
        else
        {
            Debug.Log("[SingleInstance] Release 调用但 handle 已为零，忽略");
        }
    }

    /// <summary>仅检测是否有另一个实例，不持有锁。</summary>
    public static bool IsAnotherInstanceRunning(string mutexName = null)
    {
        string rawName = mutexName;
        mutexName = NormalizeMutexName(mutexName);
        Debug.Log(string.Format("[SingleInstance] IsAnotherInstanceRunning | rawName={0} | normalizedName={1}", rawName, mutexName));
        IntPtr h = AcquireMutex(mutexName, false);
        if (h == IntPtr.Zero)
        {
            Debug.Log(string.Format("[SingleInstance] IsAnotherInstanceRunning: CreateMutex 返回零，lastError={0}，视为无其他实例", _lastError));
            return false;
        }
        bool exists = _lastError == ERROR_ALREADY_EXISTS;
        Debug.Log(string.Format("[SingleInstance] IsAnotherInstanceRunning: handle=0x{0:X} lastError={1} exists={2}", h.ToInt64(), _lastError, exists));
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
        Debug.Log(string.Format("[SingleInstance] AcquireMutex | name={0} | initialOwner={1}", mutexName, initialOwner));
        IntPtr h = CreateMutex(IntPtr.Zero, initialOwner, mutexName);
        _lastError = GetLastError(); // 必须紧跟 CreateMutex，任何其他调用都会覆盖 LastError
        Debug.Log(string.Format("[SingleInstance] CreateMutex(主名称) | handle=0x{0:X} | lastError={1}", h.ToInt64(), _lastError));
        if (h != IntPtr.Zero) return h;

        // fallback: 去掉前缀再试一次
        string bare = mutexName;
        if (bare.StartsWith("Local\\",  StringComparison.Ordinal)) bare = bare.Substring(6);
        if (bare.StartsWith("Global\\", StringComparison.Ordinal)) bare = bare.Substring(7);
        if (bare == mutexName)
        {
            // 无前缀可去，不重试，直接返回失败
            Debug.LogWarning(string.Format("[SingleInstance] CreateMutex 失败且无前缀可 fallback | lastError={0}", _lastError));
            return IntPtr.Zero;
        }
        Debug.Log(string.Format("[SingleInstance] CreateMutex fallback | bareName={0}", bare));
        h = CreateMutex(IntPtr.Zero, initialOwner, bare);
        // ★ 关键修复：只在 fallback 成功时更新 _lastError，失败时保留主名称的错误码
        uint fallbackErr = GetLastError();
        Debug.Log(string.Format("[SingleInstance] CreateMutex(fallback) | handle=0x{0:X} | lastError={1}", h.ToInt64(), fallbackErr));
        if (h != IntPtr.Zero)
            _lastError = fallbackErr; // fallback 成功，用 fallback 的错误码判断
        // h==Zero 时保留主名称的 _lastError（通常也是失败码，上层按保守策略处理）
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
