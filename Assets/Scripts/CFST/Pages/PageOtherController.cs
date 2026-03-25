using UnityEngine;
using UnityEngine.UIElements;
using UIKit;
using NativeKit;

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
                    _autoAdminHint.text = "macOS 不支持整应用长期管理员运行；Hosts 写入改为按操作提权";
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
                labelRole.text = "macOS 当前固定以普通用户运行；需要权限时仅在写入 Hosts 时申请授权";
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
            WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;

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
            ToastManager.Warning("macOS 不支持整应用长期管理员运行；Hosts 写入会在需要时单独申请授权");
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
                ToastManager.Warning("macOS 不支持整应用长期管理员运行；请继续使用普通模式，Hosts 写入会按操作申请授权");
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
                    ToastManager.Warning("macOS 不支持整应用管理员重启；请在写入 Hosts 时按提示授权");
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
