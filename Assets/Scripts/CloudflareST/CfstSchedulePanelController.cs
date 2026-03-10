// UTF-8
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 调度配置面板控制器，对应 CfstSchedulePanel.uxml
    /// </summary>
    public class CfstSchedulePanelController
    {
        private readonly VisualElement _root;

        private TextField _interval;
        private TextField _at;
        private TextField _cron;
        private TextField _tz;
        private TextField _hosts;
        private Toggle    _hostsDryRun;
        private Label     _status;
        private Label     _nextRun;
        private Label     _lastRun;
        private Label     _feedback;
        private Button    _btnSave;

        public CfstSchedulePanelController(VisualElement root)
        {
            _root = root;
            BindElements();
            LoadFromPrefs();
        }

        public void Refresh() => LoadFromPrefs();

        private void BindElements()
        {
            _interval    = _root.Q<TextField>("sched-interval");
            _at          = _root.Q<TextField>("sched-at");
            _cron        = _root.Q<TextField>("sched-cron");
            _tz          = _root.Q<TextField>("sched-tz");
            _hosts       = _root.Q<TextField>("sched-hosts");
            _hostsDryRun = _root.Q<Toggle>("sched-hosts-dry-run");
            _status      = _root.Q<Label>("sched-status");
            _nextRun     = _root.Q<Label>("sched-next-run");
            _lastRun     = _root.Q<Label>("sched-last-run");
            _feedback    = _root.Q<Label>("sched-feedback");
            _btnSave     = _root.Q<Button>("sched-btn-save");

            _btnSave?.RegisterCallback<ClickEvent>(_ => SaveToPrefs());
        }

        private void SaveToPrefs()
        {
            PlayerPrefs.SetString("cfst.sched.interval",     _interval?.value    ?? "");
            PlayerPrefs.SetString("cfst.sched.at",           _at?.value          ?? "");
            PlayerPrefs.SetString("cfst.sched.cron",         _cron?.value        ?? "");
            PlayerPrefs.SetString("cfst.sched.tz",           _tz?.value          ?? "local");
            PlayerPrefs.SetString("cfst.sched.hosts",        _hosts?.value       ?? "");
            PlayerPrefs.SetInt("cfst.sched.hostsDryRun",    (_hostsDryRun?.value ?? false) ? 1 : 0);
            PlayerPrefs.Save();
            UpdateStatusDisplay();
            SetFeedback("调度配置已保存", "ok");
        }

        private void LoadFromPrefs()
        {
            SetText(_interval,    PlayerPrefs.GetString("cfst.sched.interval", ""));
            SetText(_at,          PlayerPrefs.GetString("cfst.sched.at",       ""));
            SetText(_cron,        PlayerPrefs.GetString("cfst.sched.cron",     ""));
            SetText(_tz,          PlayerPrefs.GetString("cfst.sched.tz",       "local"));
            SetText(_hosts,       PlayerPrefs.GetString("cfst.sched.hosts",    ""));
            if (_hostsDryRun != null)
                _hostsDryRun.SetValueWithoutNotify(PlayerPrefs.GetInt("cfst.sched.hostsDryRun", 0) == 1);
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            bool hasSchedule = !string.IsNullOrWhiteSpace(_interval?.value)
                            || !string.IsNullOrWhiteSpace(_at?.value)
                            || !string.IsNullOrWhiteSpace(_cron?.value);
            if (_status != null)
            {
                _status.text = hasSchedule ? "已配置" : "未配置";
                foreach (var c in new[] { "idle", "running", "done" })
                    _status.RemoveFromClassList($"cfst-badge--{c}");
                _status.AddToClassList(hasSchedule ? "cfst-badge--running" : "cfst-badge--idle");
            }
            SetLabel(_nextRun, "— (运行时调度)" );
            SetLabel(_lastRun, PlayerPrefs.GetString("cfst.sched.lastRun", "—"));
        }

        private static void SetText(TextField f, string v)  { if (f != null) f.SetValueWithoutNotify(v); }
        private static void SetLabel(Label l, string v)      { if (l != null) l.text = v; }

        private void SetFeedback(string msg, string kind)
        {
            if (_feedback == null) return;
            _feedback.text = msg;
            foreach (var c in new[] { "ok", "err", "info" })
                _feedback.RemoveFromClassList($"cfst-feedback--{c}");
            _feedback.AddToClassList($"cfst-feedback--{kind}");
        }
    }
}
