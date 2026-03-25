using System;
using System.Diagnostics;
using System.IO;
using System.Text;

/// <summary>
/// 按操作临时提权写入 hosts，避免整个 GUI 进程长期以管理员/root 运行。
/// 当前优先支持 macOS/Linux；Windows 保留后续补充。
/// </summary>
public static class PrivilegedHostsWriter
{
    public static bool TryWrite(string targetPath, string content, Action<string> log = null)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return false;

        try
        {
            var tempFile = CreateTempFile(content);
            try
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return TryWriteMacOS(tempFile, targetPath, log);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                return TryWriteLinux(tempFile, targetPath, log);
#else
                log?.Invoke("[Hosts] 当前平台暂未实现按操作临时提权写入");
                return false;
#endif
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Hosts] 临时提权写入准备失败: {ex.Message}");
            return false;
        }
    }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    private static bool TryWriteMacOS(string tempFile, string targetPath, Action<string> log)
    {
        var backupPath = targetPath + ".unitool.bak";
        var shell = new StringBuilder();
        shell.Append("/bin/mkdir -p /tmp");
        shell.Append(" && ");
        shell.Append("/bin/cp ");
        shell.Append(QuoteForShell(targetPath));
        shell.Append(" ");
        shell.Append(QuoteForShell(backupPath));
        shell.Append(" && ");
        shell.Append("/usr/bin/install -o root -g wheel -m 644 ");
        shell.Append(QuoteForShell(tempFile));
        shell.Append(" ");
        shell.Append(QuoteForShell(targetPath));

        var appleScript = $"do shell script \"{shell.ToString().Replace("\"", "\\\"")}\" with administrator privileges";
        log?.Invoke("[Hosts] 准备通过 macOS 管理员授权写入 hosts");

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
                log?.Invoke("[Hosts] osascript 启动失败");
                return false;
            }

            process.StandardInput.WriteLine(appleScript);
            process.StandardInput.Close();

            var exited = process.WaitForExit(30000);
            var stdout = process.StandardOutput.ReadToEnd().Trim();
            var stderr = process.StandardError.ReadToEnd().Trim();

            if (!exited)
            {
                try { process.Kill(); } catch { }
                log?.Invoke("[Hosts] macOS 授权写入超时");
                return false;
            }

            if (process.ExitCode != 0)
            {
                log?.Invoke($"[Hosts] macOS 授权写入失败 code={process.ExitCode} err={stderr} out={stdout}");
                return false;
            }

            log?.Invoke($"[Hosts] 已通过管理员授权写入: {targetPath}");
            return true;
        }
    }
#endif

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    private static bool TryWriteLinux(string tempFile, string targetPath, Action<string> log)
    {
        var backupPath = targetPath + ".unitool.bak";
        var shell = new StringBuilder();
        shell.Append("/bin/cp ");
        shell.Append(QuoteForShell(targetPath));
        shell.Append(" ");
        shell.Append(QuoteForShell(backupPath));
        shell.Append(" && ");
        shell.Append("/usr/bin/install -o root -g root -m 644 ");
        shell.Append(QuoteForShell(tempFile));
        shell.Append(" ");
        shell.Append(QuoteForShell(targetPath));

        var psi = new ProcessStartInfo
        {
            FileName = "pkexec",
            Arguments = "/bin/sh -c " + QuoteForDoubleQuotedArgument(shell.ToString()),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        log?.Invoke("[Hosts] 准备通过 pkexec 写入 hosts");

        using (var process = Process.Start(psi))
        {
            if (process == null)
            {
                log?.Invoke("[Hosts] pkexec 启动失败");
                return false;
            }

            var exited = process.WaitForExit(30000);
            var stdout = process.StandardOutput.ReadToEnd().Trim();
            var stderr = process.StandardError.ReadToEnd().Trim();

            if (!exited)
            {
                try { process.Kill(); } catch { }
                log?.Invoke("[Hosts] Linux 授权写入超时");
                return false;
            }

            if (process.ExitCode != 0)
            {
                log?.Invoke($"[Hosts] Linux 授权写入失败 code={process.ExitCode} err={stderr} out={stdout}");
                return false;
            }

            log?.Invoke($"[Hosts] 已通过管理员授权写入: {targetPath}");
            return true;
        }
    }
#endif

    private static string CreateTempFile(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "UniToolGUI");
        Directory.CreateDirectory(dir);

        var tempFile = Path.Combine(dir, "hosts-update-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(tempFile, content, new UTF8Encoding(false));
        return tempFile;
    }

    private static string QuoteForShell(string value)
    {
        return "'" + (value ?? string.Empty).Replace("'", "'\\''") + "'";
    }

    private static string QuoteForDoubleQuotedArgument(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
