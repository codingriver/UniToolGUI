using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Win32 P/Invoke wrapper for Windows Registry access.
/// Avoids dependency on Microsoft.Win32.Registry assembly
/// which is unavailable under Unity's .NET Standard 2.1 profile.
/// </summary>
public static class RegistryHelper
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // Registry root handles
    private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

    // Access rights
    private const uint KEY_READ  = 0x20019;
    private const uint KEY_WRITE = 0x20006;

    // Registry value types
    private const uint REG_SZ    = 1;
    private const uint REG_DWORD = 4;

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegOpenKeyExW(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegQueryValueExW(IntPtr hKey, string lpValueName, IntPtr lpReserved, out uint lpType, byte[] lpData, ref uint lpcbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegSetValueExW(IntPtr hKey, string lpValueName, uint Reserved, uint dwType, byte[] lpData, uint cbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegDeleteValueW(IntPtr hKey, string lpValueName);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr hKey);

    /// <summary>
    /// Opens a subkey under HKEY_CURRENT_USER and returns a RegistryKeyWrapper.
    /// Returns null if the key does not exist or cannot be opened.
    /// </summary>
    public static RegistryKeyWrapper OpenCurrentUserKey(string subKey, bool writable)
    {
        uint access = writable ? KEY_WRITE | KEY_READ : KEY_READ;
        int result = RegOpenKeyExW(HKEY_CURRENT_USER, subKey, 0, access, out IntPtr hKey);
        if (result != 0) return null;
        return new RegistryKeyWrapper(hKey);
    }

    /// <summary>
    /// Thin disposable wrapper around a raw HKEY handle.
    /// Exposes GetValue, SetValue, DeleteValue matching the Microsoft.Win32.RegistryKey API surface used in this project.
    /// </summary>
    public sealed class RegistryKeyWrapper : IDisposable
    {
        private IntPtr _hKey;
        private bool _disposed;

        internal RegistryKeyWrapper(IntPtr hKey)
        {
            _hKey = hKey;
        }

        /// <summary>
        /// Reads a REG_SZ or REG_DWORD value. Returns null if the value does not exist.
        /// REG_SZ  -> returns string
        /// REG_DWORD -> returns int
        /// </summary>
        public object GetValue(string name)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RegistryKeyWrapper));

            uint type = 0;
            uint size = 0;
            // First call: determine size
            RegQueryValueExW(_hKey, name, IntPtr.Zero, out type, null, ref size);
            if (size == 0) return null;

            byte[] data = new byte[size];
            int result = RegQueryValueExW(_hKey, name, IntPtr.Zero, out type, data, ref size);
            if (result != 0) return null;

            if (type == REG_DWORD)
            {
                return BitConverter.ToInt32(data, 0);
            }
            else if (type == REG_SZ)
            {
                // REG_SZ is UTF-16LE, may include null terminator
                string s = Encoding.Unicode.GetString(data);
                return s.TrimEnd('\0');
            }
            return null;
        }

        /// <summary>
        /// Writes a string value as REG_SZ.
        /// </summary>
        public void SetValue(string name, string value)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RegistryKeyWrapper));
            // Include null terminator
            byte[] data = Encoding.Unicode.GetBytes(value + "\0");
            int result = RegSetValueExW(_hKey, name, 0, REG_SZ, data, (uint)data.Length);
            if (result != 0)
                throw new Exception($"[RegistryHelper] RegSetValueExW failed with code {result}");
        }

        /// <summary>
        /// Deletes a named value. If throwOnMissing is false, missing values are silently ignored.
        /// </summary>
        public void DeleteValue(string name, bool throwOnMissing = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RegistryKeyWrapper));
            int result = RegDeleteValueW(_hKey, name);
            if (result != 0 && throwOnMissing)
                throw new Exception($"[RegistryHelper] RegDeleteValueW failed with code {result}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                RegCloseKey(_hKey);
                _hKey = IntPtr.Zero;
                _disposed = true;
            }
        }
    }

#else
    // Stub for non-Windows platforms — methods will never be called due to #if guards
    // in WindowsStartup and WindowsTheme, but must compile on all platforms.
    public static object OpenCurrentUserKey(string subKey, bool writable) => null;
#endif
}
