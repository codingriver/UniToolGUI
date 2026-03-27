using UnityEngine;
using UnityEngine.UIElements;
using UIKit;
using NativeKit;
using System;
using System.IO;
using System.Text;

namespace CloudflareST.GUI
{
    public class PageOtherController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private Toggle        _debugToggle;
        private Toggle        _logToFileToggle;
        private Label         _logToFileHint;
        private Toggle        _trayMinToggle;
        private Toggle        _autoAdminToggle;
        private Label         _autoAdminHint;
        private Button        _btnClearLog;
        private Button        _btnCopyLog;
        private Button        _btnResetDefaults;
        private Button        _btnSwitchRole;
        private TrayBridge    _trayBridge;
        private VisualElement _groupMacHelper;
        private VisualElement _groupMacShell;
        private Label         _helperStatusLabel;
        private Button        _btnHelperInstall;
        private Button        _btnHelperUninstall;
        private Button        _btnHelperRefreshStatus;
        private Button        _btnHelperDiagnose;
        private Button        _btnHelperCopyReport;
        private Button        _btnHelperExportReport;
        private Button        _btnHelperRefreshTrust;
        private Button        _btnHelperOpenLogs;
        private Button        _btnHelperOpenPackage;
        private TextField     _helperCommandField;
        private Button        _btnHelperExec;
        private Label         _helperCommandOutput;
        private readonly StringBuilder _helperOutputBuffer = new StringBuilder();
        private string _helperLastDiagnosticReport;

        // 开机自启 Toggle 引用，供事件回调刷新
        private Toggle _startupToggle;
        private Label  _startupHint;
        private bool   _isAdmin;

        private bool _eventsBound;

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageOtherController.Init root is null");
                return;
            }

            _root = root;
            _opts = opts;
            _trayBridge = GetComponent<TrayBridge>() ?? FindObjectOfType<TrayBridge>();

            _debugToggle   = root.Q<Toggle>("toggle-debug");
            _logToFileToggle = root.Q<Toggle>("toggle-log-to-file");
            _logToFileHint   = root.Q<Label>("hint-log-to-file");
            _btnClearLog     = root.Q<Button>("btn-clear-log");
            _btnCopyLog      = root.Q<Button>("btn-copy-log");
            _btnResetDefaults = root.Q<Button>("btn-reset-defaults");
            _startupToggle   = root.Q<Toggle>("toggle-startup");
            _startupHint     = root.Q<Label>("hint-startup");
            _trayMinToggle   = root.Q<Toggle>("toggle-tray-minimize");
            _autoAdminToggle = root.Q<Toggle>("toggle-auto-admin");
            _autoAdminHint   = root.Q<Label>("hint-auto-admin");
            _btnSwitchRole   = root.Q<Button>("btn-switch-role");
            _groupMacHelper  = root.Q<VisualElement>("group-mac-helper");
            _groupMacShell   = root.Q<VisualElement>("group-mac-shell");
            _helperStatusLabel = root.Q<Label>("label-helper-status");
            _btnHelperInstall = root.Q<Button>("btn-helper-install");
            _btnHelperUninstall = root.Q<Button>("btn-helper-uninstall");
            _btnHelperRefreshStatus = root.Q<Button>("btn-helper-refresh-status");
            _btnHelperDiagnose = root.Q<Button>("btn-helper-diagnose");
            _btnHelperCopyReport = root.Q<Button>("btn-helper-copy-report");
            _btnHelperExportReport = root.Q<Button>("btn-helper-export-report");
            _btnHelperRefreshTrust = root.Q<Button>("btn-helper-refresh-trust");
            _btnHelperOpenLogs = root.Q<Button>("btn-helper-open-logs");
            _btnHelperOpenPackage = root.Q<Button>("btn-helper-open-package");
            _helperCommandField = root.Q<TextField>("field-helper-command");
            _btnHelperExec = root.Q<Button>("btn-helper-exec");
            _helperCommandOutput = root.Q<Label>("label-helper-command-output");

            // 移除旧静态 log-label（如果存在）
            root.Q<Label>("log-label")?.RemoveFromHierarchy();

            // 重置后会再次 Init；先解绑旧事件，避免重复注册导致多次触发
            UnbindEvents();

            _debugToggle?.RegisterValueChangedCallback(OnDebugToggleChanged);

            // ── 日志写入文件开关 ─────────────────────────────────
            if (_logToFileToggle != null)
            {
                _logToFileToggle.SetValueWithoutNotify(_opts.LogToFile);
                UpdateLogToFileHint(_logToFileHint, _opts.LogToFile);
                _logToFileToggle.RegisterValueChangedCallback(OnLogToFileToggleChanged);
            }

            _btnClearLog?.RegisterCallback<ClickEvent>(OnClearLogClicked);
            _btnCopyLog ?.RegisterCallback<ClickEvent>(OnCopyLogClicked);

            // ── 重置默认参数 ──────────────────────────────────────
            _btnResetDefaults?.RegisterCallback<ClickEvent>(OnResetDefaultsClicked);
            ConfigureMacHelperUi();

            // ── 开机自启 ─────────────────────────────────────────
            if (_startupToggle != null)
            {
                bool startupEnabled = false;
                try { startupEnabled = WindowsStartup.IsStartupEnabled(); }
                catch (System.Exception ex) { Debug.LogWarning("[Startup] IsStartupEnabled 失败: " + ex.Message); }
                _startupToggle.SetValueWithoutNotify(startupEnabled);
                UpdateStartupHint(_startupHint, _startupToggle.value);

                _startupToggle.RegisterValueChangedCallback(OnStartupToggleChanged);

                // 订阅全局事件：托盘菜单改变开机自启时同步刷新此 Toggle
                WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
                WindowsStartup.OnStartupChanged += OnStartupChangedExternal;
            }

            // ── 最小化到托盘开关 ──────────────────────────────────
            if (_trayMinToggle != null)
            {
                _trayMinToggle.SetValueWithoutNotify(_trayBridge == null || _trayBridge.MinimizeToTray);
                _trayMinToggle.RegisterValueChangedCallback(OnTrayMinToggleChanged);
            }

            // ── 自动管理员（方案A：注册表 AppCompatFlags） ────────────
            if (_autoAdminToggle != null)
            {
#if UNITY_STANDALONE_OSX
                _autoAdminToggle.SetValueWithoutNotify(false);
                _autoAdminToggle.SetEnabled(false);
                if (_autoAdminHint != null)
                    _autoAdminHint.text = "macOS 改为通过 Root Helper 执行高权限操作；整应用管理员模式已停用";
#else
                _autoAdminToggle.SetValueWithoutNotify(WindowsAdminAutoElevate.IsAutoAdminEnabled());
                UpdateAutoAdminHint(_autoAdminHint, _autoAdminToggle.value);
                _autoAdminToggle.RegisterValueChangedCallback(OnAutoAdminToggleChanged);
#endif
            }

            _eventsBound = true;

            // ── 管理员/普通用户切换按钮 ───────────────────────────────
            _isAdmin = false;
            try { _isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }
            string identity = WindowsAdmin.GetCurrentIdentityDisplay();
            var labelRole = root.Q<Label>("label-current-role");
            if (labelRole != null)
#if UNITY_STANDALONE_OSX
                labelRole.text = "macOS 当前固定以普通用户运行；高权限操作交由 Root Helper 执行";
#else
                labelRole.text = _isAdmin
                    ? "当前以管理员身份运行 · " + identity
                    : "当前以普通用户身份运行 · " + identity;
#endif
            if (_btnSwitchRole != null)
            {
#if UNITY_STANDALONE_OSX
                _btnSwitchRole.text = "macOS 不支持整应用管理员模式";
                _btnSwitchRole.SetEnabled(false);
#else
                _btnSwitchRole.text = _isAdmin ? "以普通用户重启" : "以管理员重启";
                _btnSwitchRole.RegisterCallback<ClickEvent>(OnSwitchRoleClicked);
#endif
            }

            // 启动提示 — 转发到日志页
            AppendLog("[INFO] CFST 已就绪");
        }

        /// <summary>转发日志到 PageLogController（由 MainWindowController 注入）</summary>
        public PageLogController LogController { get; set; }

        public void AppendLog(string line)
        {
            LogController?.AppendLog(line);
        }

        private static void UpdateLogToFileHint(Label hint, bool enabled)
        {
            if (hint == null) return;
            hint.text = enabled
                ? "✓ 日志将追加写入程序目录下的 cfst_log.txt"
                : "";
        }

        private void UpdateAutoAdminHint(Label hint, bool enabled)
        {
            if (hint == null) return;
            hint.text = enabled
                ? "✓ 已启用 — 下次启动将自动弹出 UAC 提示"
                : "";
        }

        private void UpdateStartupHint(Label hint, bool enabled)
        {
            if (hint == null) return;
            hint.text = enabled ? "✓ 已设置开机自启" : "";
        }

        private void OnDestroy()
        {
            UnbindEvents();
            WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound) return;

            _debugToggle?.UnregisterValueChangedCallback(OnDebugToggleChanged);
            _logToFileToggle?.UnregisterValueChangedCallback(OnLogToFileToggleChanged);
            _btnClearLog?.UnregisterCallback<ClickEvent>(OnClearLogClicked);
            _btnCopyLog?.UnregisterCallback<ClickEvent>(OnCopyLogClicked);
            _btnResetDefaults?.UnregisterCallback<ClickEvent>(OnResetDefaultsClicked);
            _startupToggle?.UnregisterValueChangedCallback(OnStartupToggleChanged);
            _trayMinToggle?.UnregisterValueChangedCallback(OnTrayMinToggleChanged);
            _autoAdminToggle?.UnregisterValueChangedCallback(OnAutoAdminToggleChanged);
            _btnSwitchRole?.UnregisterCallback<ClickEvent>(OnSwitchRoleClicked);
            _btnHelperInstall?.UnregisterCallback<ClickEvent>(OnHelperInstallClicked);
            _btnHelperUninstall?.UnregisterCallback<ClickEvent>(OnHelperUninstallClicked);
            _btnHelperRefreshStatus?.UnregisterCallback<ClickEvent>(OnHelperRefreshStatusClicked);
            _btnHelperDiagnose?.UnregisterCallback<ClickEvent>(OnHelperDiagnoseClicked);
            _btnHelperCopyReport?.UnregisterCallback<ClickEvent>(OnHelperCopyReportClicked);
            _btnHelperExportReport?.UnregisterCallback<ClickEvent>(OnHelperExportReportClicked);
            _btnHelperRefreshTrust?.UnregisterCallback<ClickEvent>(OnHelperRefreshTrustClicked);
            _btnHelperOpenLogs?.UnregisterCallback<ClickEvent>(OnHelperOpenLogsClicked);
            _btnHelperOpenPackage?.UnregisterCallback<ClickEvent>(OnHelperOpenPackageClicked);
            _btnHelperExec?.UnregisterCallback<ClickEvent>(OnHelperExecClicked);
            WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
            MacHelperService.OnEvent -= OnMacHelperEvent;

            _eventsBound = false;
        }

        private void OnDebugToggleChanged(ChangeEvent<bool> e)
        {
            _opts.Debug = e.newValue;
            ToastManager.Info(e.newValue ? "已启用调试输出" : "已关闭调试输出");
        }

        private void OnLogToFileToggleChanged(ChangeEvent<bool> e)
        {
            _opts.LogToFile = e.newValue;
            UpdateLogToFileHint(_logToFileHint, e.newValue);
            LogController?.SetLogToFile(e.newValue);
            ToastManager.Info(e.newValue ? "日志将写入 cfst_log.txt" : "已停止写入日志文件");
        }

        private void OnClearLogClicked(ClickEvent evt)
        {
            // 日志已移至日志页
        }

        private void OnCopyLogClicked(ClickEvent evt)
        {
            // 日志已移至日志页
        }

        private void OnResetDefaultsClicked(ClickEvent evt)
        {
            OnResetDefaults();
        }

        private void ConfigureMacHelperUi()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (_groupMacHelper != null) _groupMacHelper.style.display = DisplayStyle.Flex;
            if (_groupMacShell != null) _groupMacShell.style.display = DisplayStyle.Flex;
            MacHelperService.Initialize();
            MacHelperService.OnEvent -= OnMacHelperEvent;
            MacHelperService.OnEvent += OnMacHelperEvent;
            _btnHelperInstall?.RegisterCallback<ClickEvent>(OnHelperInstallClicked);
            _btnHelperUninstall?.RegisterCallback<ClickEvent>(OnHelperUninstallClicked);
            _btnHelperRefreshStatus?.RegisterCallback<ClickEvent>(OnHelperRefreshStatusClicked);
            _btnHelperDiagnose?.RegisterCallback<ClickEvent>(OnHelperDiagnoseClicked);
            _btnHelperCopyReport?.RegisterCallback<ClickEvent>(OnHelperCopyReportClicked);
            _btnHelperExportReport?.RegisterCallback<ClickEvent>(OnHelperExportReportClicked);
            _btnHelperRefreshTrust?.RegisterCallback<ClickEvent>(OnHelperRefreshTrustClicked);
            _btnHelperOpenLogs?.RegisterCallback<ClickEvent>(OnHelperOpenLogsClicked);
            _btnHelperOpenPackage?.RegisterCallback<ClickEvent>(OnHelperOpenPackageClicked);
            _btnHelperExec?.RegisterCallback<ClickEvent>(OnHelperExecClicked);
            RefreshMacHelperStatus();
#else
            if (_groupMacHelper != null) _groupMacHelper.style.display = DisplayStyle.None;
            if (_groupMacShell  != null) _groupMacShell.style.display  = DisplayStyle.None;
#endif
        }

        private void RefreshMacHelperStatus()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            var status = MacHelperInstallService.QueryStatus();
            if (_helperStatusLabel != null)
                _helperStatusLabel.text = status.message
                    + "\nHelper: " + status.helperBinaryPath
                    + "\nTrust: " + status.trustFilePath
                    + "\nPackage: " + status.packageDirectory;
#endif
        }

        // 在后台线程重连 helper，完成后在主线程刷新状态标签
        // 用于安装/卸载/刷新信任后，避免 XPC Connect 阻塞主线程
        private void RefreshMacHelperStatusWithReconnect()
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    MacHelperService.Disconnect();
                    bool connected = MacHelperService.Connect();
                    UnityMainThreadDispatcher.Enqueue(() => RefreshMacHelperStatus());
                }
                catch
                {
                    UnityMainThreadDispatcher.Enqueue(() => RefreshMacHelperStatus());
                }
            });
#endif
        }

        private void AppendHelperOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (_helperOutputBuffer.Length > 0)
                _helperOutputBuffer.Append('\n');
            _helperOutputBuffer.Append(line.TrimEnd());

            if (_helperCommandOutput != null)
                _helperCommandOutput.text = _helperOutputBuffer.ToString();

            AppendLog("[MacHelper] " + line.Trim());
        }

        private System.Collections.IEnumerator DelayedRefreshStatus(float delaySec)
        {
            yield return new UnityEngine.WaitForSeconds(delaySec);
            RefreshMacHelperStatus();
        }

        private void OnMacHelperEvent(MacHelperEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (evt == null)
                return;

            if (evt.EventType == "connection_opened" || evt.EventType == "connection_error" || evt.EventType == "connection_closed")
                RefreshMacHelperStatus();

            if (!string.IsNullOrEmpty(evt.Message))
            {
                string scope = !string.IsNullOrEmpty(evt.Action) ? evt.Action : evt.EventType;
                string prefix = string.IsNullOrEmpty(evt.RequestId) ? scope : evt.RequestId + " · " + scope + " · " + evt.EventType;
                AppendHelperOutput(prefix + ": " + evt.Message);
            }
#endif
        }

        private void OnHelperInstallClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            bool ok = MacHelperInstallService.Install(out var message);
            AppendLog(ok ? "[INFO] Root Helper 安装成功: " + message : "[ERROR] Root Helper 安装失败: " + message);
            if (ok)
            {
                ToastManager.Success("Root Helper 安装成功");
                RefreshMacHelperStatusWithReconnect();
            }
            else
            {
                ToastManager.Error("Root Helper 安装失败: " + message);
                RefreshMacHelperStatus();
            }
#endif
        }

        private void OnHelperUninstallClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            bool confirmed = WindowsMessageBox.Confirm("确定要卸载 macOS Root Helper 吗？", "卸载确认");
            if (!confirmed) return;
            MacHelperService.Disconnect();
            bool ok = MacHelperInstallService.Uninstall(out var message);
            AppendLog(ok ? "[INFO] Root Helper 卸载成功: " + message : "[ERROR] Root Helper 卸载失败: " + message);
            if (ok)
            {
                ToastManager.Success("Root Helper 卸载成功");
                RefreshMacHelperStatus();
            }
            else
            {
                ToastManager.Error("Root Helper 卸载失败: " + message);
                RefreshMacHelperStatus();
            }
#endif
        }

        private void OnHelperRefreshStatusClicked(ClickEvent evt)
        {
            RefreshMacHelperStatus();
            ToastManager.Info("已刷新 Root Helper 状态");
        }

        private void OnHelperDiagnoseClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            _helperOutputBuffer.Length = 0;
            if (_helperCommandOutput != null)
                _helperCommandOutput.text = string.Empty;

            AppendHelperOutput("开始执行只读诊断...");
            RefreshMacHelperStatus();

            var report = new StringBuilder();
            report.AppendLine("=== UniTool macOS Root Helper 诊断报告 ===");
            report.AppendLine("时间: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            var uiStatus = MacHelperInstallService.QueryStatus();
            report.AppendLine("UI状态: " + uiStatus.message);
            report.AppendLine("Helper路径: " + uiStatus.helperBinaryPath);
            report.AppendLine("Trust路径: " + uiStatus.trustFilePath);
            report.AppendLine("Package路径: " + uiStatus.packageDirectory);

            string packageDir = uiStatus.packageDirectory;
            if (!string.IsNullOrEmpty(packageDir))
            {
                string[] packageFiles =
                {
                    "com.unitool.roothelper",
                    "com.unitool.roothelper.plist",
                    "install_helper.sh",
                    "uninstall_helper.sh",
                    "refresh_trust.sh"
                };

                report.AppendLine("Package完整性:");
                foreach (var fileName in packageFiles)
                {
                    string path = Path.Combine(packageDir, fileName);
                    bool exists = File.Exists(path);
                    report.AppendLine("- " + fileName + ": " + (exists ? "OK" : "MISSING"));
                }
            }

            try
            {
                bool pingOk = MacHelperService.Ping(out var pingEvent, out var pingError);
                if (pingOk)
                {
                    string line = "ping 通过: " + (pingEvent?.Message ?? "pong");
                    AppendHelperOutput(line);
                    report.AppendLine(line);
                }
                else
                {
                    string line = "ping 失败: " + pingError;
                    AppendHelperOutput(line);
                    report.AppendLine(line);
                }

                bool statusOk = MacHelperService.QueryStatus(out var statusEvent, out var statusError);
                if (statusOk)
                {
                    string line = "status 通过: " + (statusEvent?.PayloadJson ?? "{}");
                    AppendHelperOutput(line);
                    report.AppendLine(line);
                }
                else
                {
                    string line = "status 失败: " + statusError;
                    AppendHelperOutput(line);
                    report.AppendLine(line);
                }

                string helperLogPath = Path.Combine(uiStatus.logDirectory ?? string.Empty, "helper.log");
                AppendLogTailToReport(report, helperLogPath, 40);

                _helperLastDiagnosticReport = report.ToString();
                if (pingOk && statusOk)
                    ToastManager.Success("Root Helper 诊断通过");
                else
                    ToastManager.Warning("Root Helper 诊断完成（存在异常项）");
            }
            catch (System.Exception ex)
            {
                string line = "诊断异常: " + ex.Message;
                AppendHelperOutput(line);
                report.AppendLine(line);
                _helperLastDiagnosticReport = report.ToString();
                ToastManager.Error("诊断失败: " + ex.Message);
            }
#endif
        }

        private void OnHelperCopyReportClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (string.IsNullOrWhiteSpace(_helperLastDiagnosticReport))
            {
                ToastManager.Warning("暂无诊断报告，请先执行诊断自检");
                return;
            }

            bool ok = NativePlatform.SetClipboard(_helperLastDiagnosticReport);
            if (ok)
                ToastManager.Success("诊断报告已复制到剪贴板");
            else
                ToastManager.Error("诊断报告复制失败");
#endif
        }

        private void OnHelperExportReportClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            if (string.IsNullOrWhiteSpace(_helperLastDiagnosticReport))
            {
                ToastManager.Warning("暂无诊断报告，请先执行诊断自检");
                return;
            }

            try
            {
                string fileName = "unitool_helper_diagnose_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                string filePath = Path.Combine(AppRuntimePaths.GetDesktopDataDir(), fileName);
                File.WriteAllText(filePath, _helperLastDiagnosticReport, Encoding.UTF8);
                AppendHelperOutput("诊断报告已导出: " + filePath);
                ToastManager.Success("诊断报告已导出");
                NativeShellService.Instance.OpenFolder(Path.GetDirectoryName(filePath));
            }
            catch (Exception ex)
            {
                AppendHelperOutput("导出失败: " + ex.Message);
                ToastManager.Error("导出失败: " + ex.Message);
            }
#endif
        }

        private static void AppendLogTailToReport(StringBuilder report, string logPath, int maxLines)
        {
            if (report == null)
                return;

            report.AppendLine("Helper日志摘要:");
            if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            {
                report.AppendLine("- helper.log 不存在");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(logPath);
                int take = Math.Max(1, maxLines);
                int start = Math.Max(0, lines.Length - take);
                report.AppendLine("- 路径: " + logPath);
                report.AppendLine("- 总行数: " + lines.Length + "，展示后 " + (lines.Length - start) + " 行");
                report.AppendLine("--- helper.log tail ---");
                for (int i = start; i < lines.Length; i++)
                    report.AppendLine(lines[i]);
                report.AppendLine("--- end helper.log tail ---");
            }
            catch (Exception ex)
            {
                report.AppendLine("- 读取 helper.log 失败: " + ex.Message);
            }
        }

        private void OnHelperRefreshTrustClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            // 先断开旧连接，避免刷新信任后旧连接状态缓存导致误报
            MacHelperService.Disconnect();
            bool ok = MacHelperInstallService.RefreshTrust(out var message);
            AppendLog(ok ? "[INFO] Root Helper 信任刷新成功: " + message : "[ERROR] Root Helper 信任刷新失败: " + message);
            if (ok)
            {
                ToastManager.Success("信任刷新成功");
                RefreshMacHelperStatusWithReconnect();
            }
            else
            {
                ToastManager.Error("信任刷新失败: " + message);
                RefreshMacHelperStatus();
            }
#endif
        }

        private void OnHelperOpenLogsClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            MacHelperInstallService.OpenHelperLogs();
#endif
        }

        private void OnHelperOpenPackageClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            MacHelperInstallService.OpenPackageFolder();
#endif
        }

        private void OnHelperExecClicked(ClickEvent evt)
        {
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_EDITOR_OSX && MAC_HELPER_IN_EDITOR)
            string command = _helperCommandField?.value;
            if (string.IsNullOrWhiteSpace(command))
            {
                ToastManager.Warning("请输入要执行的 root 命令");
                return;
            }

            bool confirmed = WindowsMessageBox.Confirm(
                "以下命令将以 root 权限执行。\n请勿执行未知命令，确认后继续：\n\n" + command,
                "执行 Root 命令");
            if (!confirmed)
                return;

            _helperOutputBuffer.Length = 0;
            if (_helperCommandOutput != null)
                _helperCommandOutput.text = string.Empty;

            try
            {
                string requestId = MacHelperService.SubmitShellCommand(command);
                AppendHelperOutput("已提交 Root 命令，请求 ID: " + requestId);
                ToastManager.Info("Root 命令已提交");
            }
            catch (System.Exception ex)
            {
                AppendHelperOutput("提交失败: " + ex.Message);
                ToastManager.Error("Root 命令提交失败: " + ex.Message);
            }
#endif
        }

        private void OnStartupToggleChanged(ChangeEvent<bool> e)
        {
            bool ok = false;
            try
            {
                string exePath = WindowsStartup.GetCurrentExePath();
                ok = e.newValue
                    ? WindowsStartup.EnableStartup(exePath)
                    : WindowsStartup.DisableStartup();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Startup] 切换失败: " + ex.Message);
                ok = false;
            }

            if (!ok)
            {
                _startupToggle?.SetValueWithoutNotify(!e.newValue);
                AppendLog("[WARN] 开机自启设置失败，请检查权限");
                ToastManager.Error("开机自启设置失败，请检查权限");
            }
            else
            {
                AppendLog(e.newValue ? "[INFO] 已启用开机自启" : "[INFO] 已禁用开机自启");
                ToastManager.Success(e.newValue ? "已启用开机自启" : "已禁用开机自启");
            }
            UpdateStartupHint(_startupHint, _startupToggle != null && _startupToggle.value);
        }

        private void OnTrayMinToggleChanged(ChangeEvent<bool> e)
        {
            if (_trayBridge != null)
                _trayBridge.MinimizeToTray = e.newValue;
            ToastManager.Info(e.newValue ? "已启用最小化到托盘" : "已关闭最小化到托盘");
        }

        private void OnAutoAdminToggleChanged(ChangeEvent<bool> e)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            bool ok = e.newValue
                ? WindowsAdminAutoElevate.EnableAutoAdmin()
                : WindowsAdminAutoElevate.DisableAutoAdmin();
            if (ok)
            {
                ToastManager.Success(e.newValue
                    ? "已设置：下次启动将自动请求管理员权限"
                    : "已取消：下次启动将以普通权限运行");
            }
            else
            {
                _autoAdminToggle?.SetValueWithoutNotify(!e.newValue);
                ToastManager.Error("注册表写入失败，设置未生效");
            }
            UpdateAutoAdminHint(_autoAdminHint, _autoAdminToggle != null && _autoAdminToggle.value);
#elif UNITY_STANDALONE_OSX
            _autoAdminToggle?.SetValueWithoutNotify(!e.newValue); // 恢复 toggle 状态（无持久化）
            FileLogger.LogWarning("[Admin] macOS 不支持整应用长期管理员运行；已阻止自动管理员开关");
            ToastManager.Warning("macOS 不支持整应用长期管理员运行；请改用 Root Helper");
#else
            ToastManager.Error("当前平台不支持自动管理员设置");
            _autoAdminToggle?.SetValueWithoutNotify(!e.newValue);
#endif
        }

        private void OnSwitchRoleClicked(ClickEvent evt)
        {
            try { _isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { _isAdmin = false; }

            if (_isAdmin)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                WindowsAdminAutoElevate.DisableAutoAdmin();
                try
                {
                    string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exe))
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    System.Diagnostics.Process.GetCurrentProcess().Kill();
                }
                catch (System.Exception ex)
                {
                    ToastManager.Error("重启失败：" + ex.Message);
                }
#elif UNITY_STANDALONE_OSX
                FileLogger.LogWarning("[Admin] macOS 不支持从设置页切换为整应用管理员模式");
                ToastManager.Warning("macOS 不支持整应用长期管理员运行；请继续使用普通模式并安装 Root Helper");
#endif
            }
            else
            {
                bool ok = false;
                try { ok = WindowsAdmin.RestartAsAdmin(); } catch { }
                if (!ok)
                {
#if UNITY_STANDALONE_OSX
                    FileLogger.LogWarning("[Admin] macOS 不支持整应用管理员重启，请改用按操作提权");
                    ToastManager.Warning("macOS 不支持整应用管理员重启；请安装 Root Helper 处理高权限操作");
#else
                    ToastManager.Error("提升权限失败，请手动以管理员身份运行");
#endif
                }
            }
        }

        /// <summary>重置所有持久化设置为默认值，并广播事件让各页面刷新 UI</summary>
        private void OnResetDefaults()
        {
            bool confirmed = WindowsMessageBox.Confirm(
                "确定要重置所有参数为默认值吗？\n此操作将清除所有已保存的设置。",
                "重置确认");
            if (!confirmed) return;

            SettingsStorage.ResetAll(_opts);
            AppendLog("[INFO] 已重置所有参数为默认值");
            ToastManager.Success("已重置所有参数为默认值");

            // 刷新本页 UI
            if (_debugToggle != null) _debugToggle.SetValueWithoutNotify(_opts.Debug);
            if (_startupToggle != null)
            {
                bool startupEnabled = false;
                try { startupEnabled = WindowsStartup.IsStartupEnabled(); } catch { }
                _startupToggle.SetValueWithoutNotify(startupEnabled);
                UpdateStartupHint(_startupHint, startupEnabled);
            }

            // 广播重置事件，MainWindowController 重新初始化各页面
            OnSettingsReset?.Invoke();
        }

        /// <summary>重置完成后广播，由 MainWindowController 订阅后重新 InitPages</summary>
        public event System.Action OnSettingsReset;

        /// <summary>
        /// 托盘菜单或其他地方修改了开机自启状态后，同步刷新界面 Toggle。
        /// WindowsStartup.OnStartupChanged 在主线程触发，可直接操作 UI。
        /// </summary>
        private void OnStartupChangedExternal(bool enabled)
        {
            if (_startupToggle == null) return;
            _startupToggle.SetValueWithoutNotify(enabled);
            UpdateStartupHint(_startupHint, enabled);
            AppendLog(enabled ? "[INFO] 开机自启已启用（外部变更）" : "[INFO] 开机自启已禁用（外部变更）");
        }
    }
}
