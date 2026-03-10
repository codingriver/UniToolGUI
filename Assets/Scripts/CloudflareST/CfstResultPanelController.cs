// UTF-8
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 测速结果面板控制器，对应 CfstResultPanel.uxml
    /// </summary>
    public class CfstResultPanelController
    {
        private readonly VisualElement _root;
        private CfstTestRecord _current;

        private Label    _countBadge;
        private Label    _testTime;
        private Label    _duration;
        private Label    _totalIps;
        private Label    _validCount;
        private Label    _bestIp;
        private Label    _bestLatency;
        private ListView _resultList;
        private Button   _btnCopy;
        private Button   _btnExport;
        private Button   _btnClear;

        private readonly List<string> _ipRows = new();

        public CfstResultPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
        }

        public void Refresh() { /* data already pushed via SetResult */ }

        public void SetResult(CfstTestRecord record)
        {
            _current = record;
            if (record == null) return;

            SetLabel(_testTime,   record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            SetLabel(_duration,   $"{record.Duration.TotalSeconds:F1}s");
            SetLabel(_bestIp,     string.IsNullOrEmpty(record.BestIp) ? "—" : record.BestIp);
            SetLabel(_bestLatency,record.BestLatencyMs > 0 ? $"{record.BestLatencyMs:F0} ms" : "—");
            SetLabel(_validCount, record.Success ? "有效" : "失败");

            if (_countBadge != null)
            {
                _countBadge.text = record.Success ? "完成" : "失败";
                foreach (var c in new[] { "idle", "running", "done", "error" })
                    _countBadge.RemoveFromClassList($"cfst-badge--{c}");
                _countBadge.AddToClassList(record.Success ? "cfst-badge--done" : "cfst-badge--error");
            }

            // Populate rows from summary
            _ipRows.Clear();
            if (!string.IsNullOrEmpty(record.BestIp))
                _ipRows.Add(record.BestIp);
            _resultList?.RefreshItems();
        }

        private void BindElements()
        {
            _countBadge  = _root.Q<Label>("result-count-badge");
            _testTime    = _root.Q<Label>("result-test-time");
            _duration    = _root.Q<Label>("result-duration");
            _totalIps    = _root.Q<Label>("result-total-ips");
            _validCount  = _root.Q<Label>("result-valid-count");
            _bestIp      = _root.Q<Label>("result-best-ip");
            _bestLatency = _root.Q<Label>("result-best-latency");
            _resultList  = _root.Q<ListView>("result-list");
            _btnCopy     = _root.Q<Button>("result-btn-copy");
            _btnExport   = _root.Q<Button>("result-btn-export");
            _btnClear    = _root.Q<Button>("result-btn-clear");

            if (_resultList != null)
            {
                _resultList.itemsSource = _ipRows;
                _resultList.makeItem    = () => new Label { style = { paddingLeft = 12, paddingTop = 4, paddingBottom = 4 } };
                _resultList.bindItem    = (e, i) =>
                {
                    if (e is Label lbl) lbl.text = _ipRows[i];
                };
            }

            _btnCopy?.RegisterCallback<ClickEvent>(_ => CopyIps());
            _btnClear?.RegisterCallback<ClickEvent>(_ => ClearResults());
        }

        private void CopyIps()
        {
            if (_ipRows.Count == 0) return;
            GUIUtility.systemCopyBuffer = string.Join("\n", _ipRows);
            Debug.Log("[CfstResult] IP 已复制到剪贴板");
        }

        private void ClearResults()
        {
            _current = null;
            _ipRows.Clear();
            _resultList?.RefreshItems();
            SetLabel(_testTime, "—");
            SetLabel(_duration, "—");
            SetLabel(_bestIp, "—");
            SetLabel(_bestLatency, "—");
        }

        private static void SetLabel(Label l, string v) { if (l != null) l.text = v; }
    }
}
