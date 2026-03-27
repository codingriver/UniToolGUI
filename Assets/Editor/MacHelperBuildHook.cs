using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Diagnostics;

namespace CloudflareST.GUI.Editor
{
    public sealed class MacHelperBuildHook : IPreprocessBuildWithReport
    {
        private const string BridgeBundlePath = "Assets/Plugins/NativeKit/MacOS/UniToolXpcBridge.bundle";
        private const string HelperPackagePath = "Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package";

        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneOSX)
                return;

            if (!Directory.Exists(BridgeBundlePath))
                throw new BuildFailedException("缺少 UniToolXpcBridge.bundle，请先执行 MacRootHelper/build.sh");
            if (!Directory.Exists(HelperPackagePath))
                throw new BuildFailedException("缺少 HelperArtifacts/package，请先执行 MacRootHelper/build.sh");

            string[] requiredFiles =
            {
                "com.unitool.roothelper",
                "com.unitool.roothelper.plist",
                "install_helper.sh",
                "uninstall_helper.sh",
                "refresh_trust.sh"
            };

            foreach (var fileName in requiredFiles)
            {
                string filePath = Path.Combine(HelperPackagePath, fileName);
                if (!File.Exists(filePath))
                    throw new BuildFailedException("缺少 helper 打包文件: " + fileName + "，请先执行 MacRootHelper/build.sh");
            }
        }

        [PostProcessBuild(200)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneOSX)
                return;

            string contentsPath = Path.Combine(pathToBuiltProject, "Contents", "Resources", "PrivilegedHelper");
            Directory.CreateDirectory(contentsPath);
            CopyDirectory(HelperPackagePath, contentsPath);
            RestoreExecutablePermissions(contentsPath);
            UnityEngine.Debug.Log("[MacHelperBuild] 已复制 Root Helper 资源到: " + contentsPath);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }

        private static void RestoreExecutablePermissions(string destinationDir)
        {
            string[] executableNames =
            {
                "com.unitool.roothelper",
                "install_helper.sh",
                "uninstall_helper.sh",
                "refresh_trust.sh"
            };

            foreach (var fileName in executableNames)
            {
                string path = Path.Combine(destinationDir, fileName);
                if (!File.Exists(path))
                    continue;

                using (var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = "755 \"" + path.Replace("\"", "\\\"") + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    process?.WaitForExit(5000);
                }
            }
        }
    }
}
