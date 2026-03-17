using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.GUI
{
    public class PageScheduleController : MonoBehaviour
    {
        private VisualElement    _root;
        private CfstOptions      _opts;

        private RadioButtonGroup _schedModeGroup;

        private VisualElement _groupInterval;
        private VisualElement _groupDaily;
        private VisualElement _groupCron;
        private VisualElement _groupTimezone;

        private IntegerField  _intervalField;
        private TextField     _dailyAtField;
        private TextField     _cronField;
        private DropdownField _timezoneDropdown;
        private Label         _labelNext1;
        private Label         _labelNext2;

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _schedModeGroup = root.Q<RadioButtonGroup>("sched-mode-group");

            _groupInterval  = root.Q<VisualElement>("group-interval");
            _groupDaily     = root.Q<VisualElement>("group-daily");
            _groupCron      = root.Q<VisualElement>("group-cron");
            _groupTimezone  = root.Q<VisualElement>("group-timezone");

            _intervalField    = root.Q<IntegerField>("field-intervalmin");
            _dailyAtField     = root.Q<TextField>("field-dailyat");
            _cronField        = root.Q<TextField>("field-cron");
            _timezoneDropdown = root.Q<DropdownField>("dropdown-timezone");
            _labelNext1       = root.Q<Label>("label-next1");
            _labelNext2       = root.Q<Label>("label-next2");

            // 填充时区列表
            var tzList = new List<string> { "Local -- 系统默认" };
            foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
                tzList.Add(tz.Id);
            if (_timezoneDropdown != null)
            {
                _timezoneDropdown.choices = tzList;
                _timezoneDropdown.value   = tzList[0];
                _timezoneDropdown.RegisterValueChangedCallback(e =>
                {
                    _opts.TimeZone = e.newValue.StartsWith("Local") ? null : e.newValue;
                    UpdatePreview();
                });
            }

            // RadioButtonGroup: 0=None,1=Interval,2=Daily,3=Cron
            _schedModeGroup?.RegisterValueChangedCallback(e =>
            {
                switch (e.newValue)
                {
                    case 1: SetMode(ScheduleMode.Interval); break;
                    case 2: SetMode(ScheduleMode.Daily);    break;
                    case 3: SetMode(ScheduleMode.Cron);     break;
                    default: SetMode(ScheduleMode.None);    break;
                }
            });

            _intervalField?.RegisterValueChangedCallback(e =>
            {
                _opts.IntervalMinutes = e.newValue < 1 ? 1 : e.newValue;
                UpdatePreview();
            });
            _dailyAtField?.RegisterValueChangedCallback(e =>
            {
                _opts.DailyAt = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim();
                UpdatePreview();
            });
            _cronField?.RegisterValueChangedCallback(e =>
            {
                _opts.CronExpression = string.IsNullOrWhiteSpace(e.newValue) ? null : e.newValue.Trim();
                UpdatePreview();
            });

            // 强制选中第一项，确保运行时 RadioButtonGroup 有默认选中状态
            if (_schedModeGroup != null) _schedModeGroup.value = 0;
            SetMode(ScheduleMode.None);
        }

        private void SetMode(ScheduleMode mode)
        {
            _opts.ScheduleMode = mode;
            SetGroupVisible(_groupInterval, mode == ScheduleMode.Interval);
            SetGroupVisible(_groupDaily,    mode == ScheduleMode.Daily);
            SetGroupVisible(_groupCron,     mode == ScheduleMode.Cron);
            SetGroupVisible(_groupTimezone, mode == ScheduleMode.Daily || mode == ScheduleMode.Cron);
            UpdatePreview();
        }

        private void SetGroupVisible(VisualElement g, bool visible)
        {
            if (g == null) return;
            g.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdatePreview()
        {
            if (_labelNext1 == null) return;
            try
            {
                var now = DateTime.Now;
                switch (_opts.ScheduleMode)
                {
                    case ScheduleMode.None:
                        _labelNext1.text = "-";
                        if (_labelNext2 != null) _labelNext2.text = "-";
                        break;
                    case ScheduleMode.Interval:
                        if (_opts.IntervalMinutes > 0)
                        {
                            var n1 = now.AddMinutes(_opts.IntervalMinutes);
                            var n2 = n1.AddMinutes(_opts.IntervalMinutes);
                            _labelNext1.text = n1.ToString("yyyy-MM-dd HH:mm");
                            if (_labelNext2 != null) _labelNext2.text = n2.ToString("yyyy-MM-dd HH:mm");
                        }
                        break;
                    default:
                        _labelNext1.text = "(需要 Cron 解析库)";
                        if (_labelNext2 != null) _labelNext2.text = "-";
                        break;
                }
            }
            catch
            {
                _labelNext1.text = "-";
            }
        }
    }
}
