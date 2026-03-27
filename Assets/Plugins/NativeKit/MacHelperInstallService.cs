using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NativeKit
{
    public static class MacHelperInstallService
    {
        private const string InstalledHelperPath = "/Library/PrivilegedHelperTools/com.unitool.roothelper";
        private const string LaunchDaemonPath = "/Library/LaunchDaemons/com.unitool.roothelper.plist";
        private const string TrustFilePath = "/Users/Shared/UniTool/helper/trust.json";
        private const string LogDirectory = "/Users/Shared/UniTool/logs";

        // macOS 授权弹窗提示语，可由 Unity 侧覆盖
        public static string AuthorizationPrompt { get; set; } = "更新Hosts权限申请";
        // Root Helper 信任 token，可由 Unity 侧覆盖
        public static string TrustToken { get; set; } = "unitool-default-token";

        public static MacHelperStatus QueryStatus()
        {
            var status = new MacHelperStatus
            {
                helperBinaryPath = InstalledHelperPath,
                launchDaemonPath = LaunchDaemonPath,
                trustFilePath = TrustFilePath,
                logDirectory = LogDirectory,
                packageDirectory = GetRuntimePackageDirectory(),
                isInstalled = File.Exists(InstalledHelperPath) && File.Exists(LaunchDaemonPath),
                isConnected = false,
                message = "helper 未安装"
            };

#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            try
            {
                status.isConnected = status.isInstalled && MacHelperService.Connect();
                status.message = status.isConnected
                    ? "helper 已连接"
                    : (status.isInstalled ? "helper 已安装但通信失败" : "helper 未安装");
            }
            catch (Exception ex)
            {
                status.message = ex.Message;
            }
#endif
            return status;
        }

        public static bool Install(out string message)
        {
            return RunPrivilegedScript("install_helper.sh", out message);
        }

        public static bool Uninstall(out string message)
        {
            return RunPrivilegedScript("uninstall_helper.sh", out message, includeTrustArgs: false);
        }

        public static bool RefreshTrust(out string message)
        {
            return RunPrivilegedScript("refresh_trust.sh", out message);
        }

        public static void OpenPackageFolder()
        {
            string path = GetRuntimePackageDirectory();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                NativeShellService.Instance.OpenFolder(path);
        }

        public static void OpenHelperLogs()
        {
            if (Directory.Exists(LogDirectory))
                NativeShellService.Instance.OpenFolder(LogDirectory);
        }

        public static string GetRuntimePackageDirectory()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            try
            {
                string dataPath = Application.dataPath ?? string.Empty;
                string parent1 = Path.GetDirectoryName(dataPath);
                string parent2 = parent1 == null ? null : Path.GetDirectoryName(parent1);

                string[] candidates =
                {
                    Path.Combine(dataPath, "Resources", "PrivilegedHelper"),
                    parent1 == null ? null : Path.Combine(parent1, "Resources", "PrivilegedHelper"),
                    parent2 == null ? null : Path.Combine(parent2, "Resources", "PrivilegedHelper"),
                    Path.Combine(dataPath, "Plugins/NativeKit/MacOS/HelperArtifacts/package")
                };

                foreach (var candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                        return candidate;
                }

                return candidates[0];
            }
            catch
            {
                return null;
            }
#else
            return Path.Combine(Application.dataPath, "Plugins/NativeKit/MacOS/HelperArtifacts/package");
#endif
        }

        private static bool RunPrivilegedScript(string scriptName, out string message, bool includeTrustArgs = true)
        {
            message = "仅 macOS 打包版支持";
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            string packageDir = GetRuntimePackageDirectory();
            string scriptPath = Path.Combine(packageDir, scriptName);
            if (!File.Exists(scriptPath))
            {
                message = "缺少 helper 脚本: " + scriptPath;
                return false;
            }

            string arguments = string.Empty;
            if (includeTrustArgs)
            {
                arguments = " --token " + QuoteForShell(TrustToken);
            }

            // Ensure a safe working directory for osascript (Unity editor can have a deleted CWD)
            string shell = "cd /; " + QuoteForShell(scriptPath) + arguments;
            string prompt = string.IsNullOrWhiteSpace(AuthorizationPrompt) ? "需要管理员权限" : AuthorizationPrompt;
            string appleScript = "do shell script \"" + EscapeForAppleScript(shell) + "\" with administrator privileges"
                               + " with prompt \"" + EscapeForAppleScript(prompt) + "\"";

            try
            {
                try
                {
                    FileLogger.Log("[MacHelperInstall] script=" + scriptName
                                   + " path=" + scriptPath
                                   + " packageDir=" + packageDir
                                   + " token=" + (TrustToken ?? ""));
                    UnityEngine.Debug.Log("[MacHelperInstall] script=" + scriptName
                              + " path=" + scriptPath
                              + " packageDir=" + packageDir
                              + " token=" + (TrustToken ?? ""));
                }
                catch { }

                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/osascript",
                    Arguments = "-",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        message = "无法启动 osascript";
                        return false;
                    }

                    process.StandardInput.WriteLine(appleScript);
                    process.StandardInput.Close();
                    process.WaitForExit(30000);

                    string stdout = process.StandardOutput.ReadToEnd().Trim();
                    string stderr = process.StandardError.ReadToEnd().Trim();
                    if (process.ExitCode != 0)
                    {
                        message = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                        try
                        {
                            FileLogger.LogWarning("[MacHelperInstall] script failed: " + message);
                            UnityEngine.Debug.LogWarning("[MacHelperInstall] script failed: " + message);
                        }
                        catch { }
                        return false;
                    }

                    message = string.IsNullOrEmpty(stdout) ? "操作完成" : stdout;
                    try
                    {
                        FileLogger.Log("[MacHelperInstall] script ok: " + message);
                        UnityEngine.Debug.Log("[MacHelperInstall] script ok: " + message);
                    }
                    catch { }
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
#else
            return false;
#endif
        }

        private static bool TryGetAppExecutableAndHash(out string appExe, out string appSha256, out string message)
        {
            appExe = null;
            appSha256 = null;
            message = null;

            if (!MacAppLocator.TryGetExecutablePath(out appExe) || string.IsNullOrEmpty(appExe))
            {
                message = "无法定位当前应用可执行文件";
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(appExe))
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    appSha256 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
                return true;
            }
            catch (Exception ex)
            {
                message = "计算应用 SHA256 失败: " + ex.Message;
                return false;
            }
        }

        private static string QuoteForShell(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";
        }

        private static string EscapeForAppleScript(string value)
        {
            var text = value ?? string.Empty;
            var sb = new StringBuilder(text.Length + 16);
            foreach (char c in text)
            {
                if (c == '\\') sb.Append(@"\\");
                else if (c == '"') sb.Append("\\\"");
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
