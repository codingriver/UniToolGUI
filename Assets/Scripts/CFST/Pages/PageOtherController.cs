using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    public class PageOtherController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        private Toggle        _debugToggle;

        // 开机自启 Toggle 引用，供事件回调刷新
        private Toggle _startupToggle;
        private Label  _startupHint;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _debugToggle  = root.Q<Toggle>("toggle-debug");

            // 移除旧静态 log-label（如果存在）
            root.Q<Label>("log-label")?.RemoveFromHierarchy();

            _debugToggle?.RegisterValueChangedCallback(e => _opts.Debug = e.newValue);

            root.Q<Button>("btn-clear-log")?.RegisterCallback<ClickEvent>(_ => { /* 日志已移至日志页 */ });
            root.Q<Button>("btn-copy-log") ?.RegisterCallback<ClickEvent>(_ => { /* 日志已移至日志页 */ });

            // ── 重置默认参数 ──────────────────────────────────────
            root.Q<Button>("btn-reset-defaults")?.RegisterCallback<ClickEvent>(_ => OnResetDefaults());

            // ── 开机自启 ─────────────────────────────────────────
            _startupToggle = root.Q<Toggle>("toggle-startup");
            _startupHint   = root.Q<Label>("hint-startup");
            var startupToggle = _startupToggle;
            var hintStartup   = _startupHint;
            if (startupToggle != null)
            {
                bool startupEnabled = false;
                try { startupEnabled = WindowsStartup.IsStartupEnabled(); }
                catch (System.Exception ex) { Debug.LogWarning("[Startup] IsStartupEnabled 失败: " + ex.Message); }
                startupToggle.value = startupEnabled;
                UpdateStartupHint(hintStartup, startupToggle.value);

                startupToggle.RegisterValueChangedCallback(e =>
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
                        startupToggle.SetValueWithoutNotify(!e.newValue);
                        AppendLog("[WARN] 开机自启设置失败，请检查权限");
                        ToastManager.Error("开机自启设置失败，请检查权限");
                    }
                    else
                    {
                        AppendLog(e.newValue ? "[INFO] 已启用开机自启" : "[INFO] 已禁用开机自启");
                        ToastManager.Success(e.newValue ? "已启用开机自启" : "已禁用开机自启");
                    }
                    UpdateStartupHint(hintStartup, startupToggle.value);
                });

                // 订阅全局事件：托盘菜单改变开机自启时同步刷新此 Toggle
                WindowsStartup.OnStartupChanged += OnStartupChangedExternal;
            }

            // ── 最小化到托盘开关 ──────────────────────────────────
            var trayMinToggle = root.Q<Toggle>("toggle-tray-minimize");
            if (trayMinToggle != null)
            {
                trayMinToggle.value = CfstTrayManager.MinimizeToTray;
                trayMinToggle.RegisterValueChangedCallback(e =>
                    CfstTrayManager.MinimizeToTray = e.newValue);
            }

            // ── 自动管理员（方案A：注册表 AppCompatFlags） ────────────
            var autoAdminToggle = root.Q<Toggle>("toggle-auto-admin");
            var hintAutoAdmin   = root.Q<Label>("hint-auto-admin");
            if (autoAdminToggle != null)
            {
                autoAdminToggle.SetValueWithoutNotify(WindowsAdminAutoElevate.IsAutoAdminEnabled());
                UpdateAutoAdminHint(hintAutoAdmin, autoAdminToggle.value);
                autoAdminToggle.RegisterValueChangedCallback(e =>
                {
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
                        autoAdminToggle.SetValueWithoutNotify(!e.newValue);
                        ToastManager.Error("注册表写入失败，设置未生效");
                    }
                    UpdateAutoAdminHint(hintAutoAdmin, autoAdminToggle.value);
                });
            }

            // ── 管理员/普通用户切换按钮 ───────────────────────────────
            bool isAdmin = false;
            try { isAdmin = WindowsAdmin.IsRunningAsAdmin(); } catch { }
            var labelRole  = root.Q<Label>("label-current-role");
            var btnSwitch  = root.Q<Button>("btn-switch-role");
            if (labelRole != null)
                labelRole.text = isAdmin
                    ? "当前以管理员身份运行"
                    : "当前以普通用户身份运行";
            if (btnSwitch != null)
            {
                btnSwitch.text = isAdmin ? "以普通用户重启" : "以管理员重启";
                btnSwitch.RegisterCallback<ClickEvent>(_ =>
                {
                    if (isAdmin)
                    {
                        // 以普通用户重启：先禁用 RUNASADMIN，再普通重启
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
                    }
                    else
                    {
                        // 以管理员重启
                        bool ok = false;
                        try { ok = WindowsAdmin.RestartAsAdmin(); } catch { }
                        if (!ok) ToastManager.Error("提升权限失败，请手动以管理员身份运行");
                    }
                });
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
            WindowsStartup.OnStartupChanged -= OnStartupChangedExternal;
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
