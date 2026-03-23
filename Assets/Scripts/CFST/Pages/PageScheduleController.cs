// ============================================================
// PageScheduleController.cs  —  定时调度页面控制器
// 交互：Cron输入框 + 快捷模板 + 语义预览卡片
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageScheduleController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;
        private Toggle        _enabledToggle;
        private TextField     _cronField;
        private Label         _cronErrorLabel;
        private Label         _cronSemanticLabel;
        private Label         _statusLabel;
        private Label         _next1Label;
        private Label         _next2Label;
        private Label         _runCountLabel;
        private Button        _btnRunNow;
        private Button        _btnStop;
        private Label         _lblSegMin, _lblSegHour, _lblSegDay, _lblSegMon, _lblSegWeek;
        private bool          _syncingField;

        private static readonly (string name, string expr)[] Templates =
        {
            ("tpl-every30m", "*/30 * * * *"),
            ("tpl-every1h",  "0 * * * *"),
            ("tpl-every2h",  "0 */2 * * *"),
            ("tpl-every6h",  "0 */6 * * *"),
            ("tpl-every12h", "0 */12 * * *"),
            ("tpl-daily2am", "0 2 * * *"),
            ("tpl-daily8am", "0 8 * * *"),
            ("tpl-daily8pm", "0 20 * * *"),
            ("tpl-weekly",   "0 0 * * 1"),
            ("tpl-workday",  "0 8 * * 1-5"),
        };

        public void Init(VisualElement root, CfstOptions opts)
        {
            if (root == null)
            {
                Debug.LogError("[UI] PageScheduleController.Init root is null");
                return;
            }

            _root = root; _opts = opts;
            _enabledToggle     = root.Q<Toggle>("toggle-schedule-enabled");
            _cronField         = root.Q<TextField>("field-cron");
            _cronErrorLabel    = root.Q<Label>("label-cron-error");
            _cronSemanticLabel = root.Q<Label>("label-cron-semantic");
            _statusLabel       = root.Q<Label>("label-sched-status");
            _next1Label        = root.Q<Label>("label-next1");
            _next2Label        = root.Q<Label>("label-next2");
            _runCountLabel     = root.Q<Label>("label-run-count");
            _btnRunNow         = root.Q<Button>("btn-sched-run-now");
            _btnStop           = root.Q<Button>("btn-sched-stop");
            _lblSegMin  = root.Q<Label>("lbl-seg-min");
            _lblSegHour = root.Q<Label>("lbl-seg-hour");
            _lblSegDay  = root.Q<Label>("lbl-seg-day");
            _lblSegMon  = root.Q<Label>("lbl-seg-mon");
            _lblSegWeek = root.Q<Label>("lbl-seg-week");

            // 恢复
            _enabledToggle?.SetValueWithoutNotify(_opts.ScheduleEnabled);
            string saved = _opts.CronExpression ?? "";
            _cronField?.SetValueWithoutNotify(saved);
            UpdateSegLabels(saved);
            HighlightMatchingTemplate(saved);

            // 输入框变更
            _cronField?.RegisterValueChangedCallback(e => {
                if (_syncingField) return;
                string expr = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim();
                _opts.CronExpression = expr;
                UpdateSegLabels(expr ?? "");
                HighlightMatchingTemplate(expr ?? "");
                ValidateAndPreview();
                if (_opts.ScheduleEnabled && ScheduleManager.Instance != null) ApplySchedule();
            });

            // 模板按钮
            foreach (var tpl in Templates) RegisterTemplate(tpl.name, tpl.expr);

            // 启用 Toggle
            _enabledToggle?.RegisterValueChangedCallback(e => {
                _opts.ScheduleEnabled = e.newValue;
                if (e.newValue) ApplySchedule(); else ScheduleManager.Instance?.Disable();
            });

            _btnRunNow?.RegisterCallback<ClickEvent>(_ => OnRunNow());
            _btnStop  ?.RegisterCallback<ClickEvent>(_ => OnStop());

            if (ScheduleManager.Instance != null)
                ScheduleManager.Instance.OnStateChanged += RefreshStatus;

            ValidateAndPreview(); RefreshStatus(); RefreshButtons();
        }

        private void OnDisable()
        {
            if (ScheduleManager.Instance != null)
                ScheduleManager.Instance.OnStateChanged -= RefreshStatus;
        }

        // ── 段标签更新 ───────────────────────────────────────
        private void UpdateSegLabels(string expr)
        {
            var segs = string.IsNullOrWhiteSpace(expr)
                ? new[]{"*","*","*","*","*"}
                : expr.Trim().Split(new[]{' '}, 5);
            while (segs.Length < 5) { var t=new string[segs.Length+1]; segs.CopyTo(t,0); t[segs.Length]="*"; segs=t; }
            if (_lblSegMin  != null) _lblSegMin.text  = segs[0];
            if (_lblSegHour != null) _lblSegHour.text = segs[1];
            if (_lblSegDay  != null) _lblSegDay.text  = segs[2];
            if (_lblSegMon  != null) _lblSegMon.text  = segs[3];
            if (_lblSegWeek != null) _lblSegWeek.text = segs[4];
        }

        // ── 模板按钮 ─────────────────────────────────────────
        private void RegisterTemplate(string btnName, string expr)
        {
            var btn = _root.Q<Button>(btnName);
            if (btn == null) return;
            btn.RegisterCallback<ClickEvent>(_ => {
                _opts.CronExpression = expr;
                _syncingField = true;
                _cronField?.SetValueWithoutNotify(expr);
                _syncingField = false;
                UpdateSegLabels(expr);
                HighlightMatchingTemplate(expr);
                ValidateAndPreview();
                if (_opts.ScheduleEnabled && ScheduleManager.Instance != null) ApplySchedule();
            });
        }

        private void HighlightMatchingTemplate(string expr)
        {
            string trimmed = expr?.Trim() ?? "";
            foreach (var tpl in Templates)
            {
                var btn = _root.Q<Button>(tpl.name);
                if (btn == null) continue;
                if (tpl.expr == trimmed) btn.AddToClassList("btn-tpl--active");
                else                     btn.RemoveFromClassList("btn-tpl--active");
            }
        }

        // ── 语义描述 ─────────────────────────────────────────
        private static string BuildSemantic(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return "未设置";
            var s = expr.Trim().Split(new[]{' '}, 5);
            if (s.Length < 5) return expr;
            string min=s[0],hour=s[1],day=s[2],mon=s[3],week=s[4];
            if (min.StartsWith("*/")&&hour=="*"&&day=="*"&&mon=="*"&&week=="*")
                return "每 "+min.Substring(2)+" 分钟执行一次";
            if (min=="0"&&hour=="*"&&day=="*"&&mon=="*"&&week=="*") return "每小时整点执行";
            if (min=="0"&&hour.StartsWith("*/")&&day=="*"&&mon=="*"&&week=="*")
                return "每 "+hour.Substring(2)+" 小时整点执行";
            if (min=="0"&&!hour.Contains("/")&&!hour.Contains(",")&&day=="*"&&mon=="*"&&week=="*")
                return "每天 "+hour.PadLeft(2,'0')+":00 执行";
            if (min=="0"&&hour.Contains(",")&&day=="*"&&mon=="*"&&week=="*")
                return "每天 "+hour.Replace(",",":00 和 ")+":00 执行";
            string[] wn={"周日","周一","周二","周三","周四","周五","周六","周日"};
            if (week=="1-5"&&!hour.Contains("/"))
                return "工作日 "+hour.PadLeft(2,'0')+":"+min.PadLeft(2,'0')+" 执行";
            if ((week=="6,0"||week=="0,6")&&!hour.Contains("/"))
                return "周末 "+hour.PadLeft(2,'0')+":"+min.PadLeft(2,'0')+" 执行";
            if (min=="0"&&!hour.Contains("/")&&day=="*"&&mon=="*"&&week!="*")
            {
                string wname = int.TryParse(week,out int wi)&&wi<wn.Length ? wn[wi] : "每周"+week;
                return wname+" "+hour.PadLeft(2,'0')+":00 执行";
            }
            if (min=="0"&&!hour.Contains("/")&&day!="*"&&mon=="*"&&week=="*")
                return "每月 "+day+" 日 "+hour.PadLeft(2,'0')+":00 执行";
            return "自定义: "+expr;
        }

        // ── 校验 + 预览 ──────────────────────────────────────
        private void ValidateAndPreview()
        {
            string expr = _opts.CronExpression;
            if (string.IsNullOrWhiteSpace(expr))
            {
                SetSemantic("未设置", false);
                if (_cronErrorLabel != null) _cronErrorLabel.text = "";
                if (_next1Label != null) _next1Label.text = "-";
                if (_next2Label != null) _next2Label.text = "-";
                return;
            }
            if (ScheduleManager.Instance == null) return;
            bool ok = ScheduleManager.Instance.TryPreview(
                expr, out DateTime? n1, out DateTime? n2, out string error);
            if (!ok)
            {
                SetSemantic("格式错误", false);
                if (_cronErrorLabel != null) _cronErrorLabel.text = "格式错误: "+error;
                if (_next1Label != null) _next1Label.text = "-";
                if (_next2Label != null) _next2Label.text = "-";
            }
            else
            {
                if (_cronErrorLabel != null) _cronErrorLabel.text = "";
                SetSemantic(BuildSemantic(expr), true);
                if (_next1Label != null)
                    _next1Label.text = n1.HasValue ? n1.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                if (_next2Label != null)
                    _next2Label.text = n2.HasValue ? n2.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            }
        }

        private void SetSemantic(string text, bool ok)
        {
            if (_cronSemanticLabel == null) return;
            _cronSemanticLabel.text = text;
            _cronSemanticLabel.style.color = ok
                ? new StyleColor(new UnityEngine.Color(0.90f, 0.90f, 0.93f))
                : new StyleColor(new UnityEngine.Color(0.97f, 0.53f, 0.44f));
        }

        // ── 应用调度 ─────────────────────────────────────────
        private void ApplySchedule()
        {
            if (ScheduleManager.Instance == null) return;
            bool ok = ScheduleManager.Instance.Enable(_opts.CronExpression);
            if (!ok)
            {
                _opts.ScheduleEnabled = false;
                _enabledToggle?.SetValueWithoutNotify(false);
                if (_cronErrorLabel != null)
                    _cronErrorLabel.text = ScheduleManager.Instance.LastError ?? "Cron 解析失败";
            }
            else { if (_cronErrorLabel != null) _cronErrorLabel.text = ""; }
            RefreshButtons();
        }

        private void OnRunNow() { ScheduleManager.Instance?.TriggerNow(); }

        private void OnStop()
        {
            _opts.ScheduleEnabled = false;
            _enabledToggle?.SetValueWithoutNotify(false);
            ScheduleManager.Instance?.Disable();
            RefreshButtons();
        }

        // ── 状态刷新 ─────────────────────────────────────────
        private void RefreshStatus()
        {
            var mgr = ScheduleManager.Instance;
            if (mgr == null) { if (_statusLabel != null) _statusLabel.text = "未初始化"; return; }
            if (!mgr.IsEnabled)
            {
                if (_statusLabel   != null) _statusLabel.text   = "未启用";
                if (_next1Label    != null) _next1Label.text    = "-";
                if (_next2Label    != null) _next2Label.text    = "-";
                if (_runCountLabel != null) _runCountLabel.text = mgr.RunCount + " 次";
                return;
            }
            if (_statusLabel != null) _statusLabel.text = mgr.IsWaiting ? "等待下次触发" : "执行中";
            if (_next1Label != null)
                _next1Label.text = mgr.NextRunAt.HasValue
                    ? mgr.NextRunAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            if (_next2Label != null && mgr.NextRunAt.HasValue)
            {
                ScheduleManager.Instance.TryPreview(
                    _opts.CronExpression, out _, out DateTime? n2, out _);
                _next2Label.text = n2.HasValue ? n2.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            }
            if (_runCountLabel != null) _runCountLabel.text = mgr.RunCount + " 次";
            RefreshButtons();
        }

        private void RefreshButtons()
        {
            var mgr = ScheduleManager.Instance;
            _btnStop?.SetEnabled(mgr != null && mgr.IsEnabled);
        }
    }
}
