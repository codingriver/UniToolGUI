// UTF-8
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CloudflareST.Unity.UI
{
    /// <summary>
    /// 历史记录面板控制器，对应 CfstHistoryPanel.uxml
    /// </summary>
    public class CfstHistoryPanelController
    {
        private readonly VisualElement _root;
        private readonly List<CfstTestRecord> _records = new();

        private Label    _countBadge;
        private ListView _historyList;
        private Button   _btnClear;
        private VisualElement _detailCard;
        private Label _detailTime;
        private Label _detailProtocol;
        private Label _detailDuration;
        private Label _detailBestIp;
        private Label _detailSummary;
        private Button _btnRerun;

        public System.Action<CfstTestRecord> OnRerun;

        public CfstHistoryPanelController(VisualElement root)
        {
            _root = root;
            BindElements();
        }

        public void Refresh() => RefreshList();

        public void AddRecord(CfstTestRecord record)
        {
            if (record == null) return;
            _records.Insert(0, record);
            PlayerPrefs.SetString("cfst.sched.lastRun", record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            PlayerPrefs.Save();
            RefreshList();
        }

        private void BindElements()
        {
            _countBadge  = _root.Q<Label>("history-count-badge");
            _historyList = _root.Q<ListView>("history-list");
            _btnClear    = _root.Q<Button>("history-btn-clear");
            _detailCard  = _root.Q<VisualElement>("history-detail-card");
            _detailTime  = _root.Q<Label>("detail-time");
            _detailProtocol = _root.Q<Label>("detail-protocol");
            _detailDuration = _root.Q<Label>("detail-duration");
            _detailBestIp   = _root.Q<Label>("detail-best-ip");
            _detailSummary  = _root.Q<Label>("detail-summary");
            _btnRerun    = _root.Q<Button>("detail-btn-rerun");

            if (_historyList != null)
            {
                _historyList.itemsSource = _records;
                _historyList.makeItem = () =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("cfst-history-row");
                    var t = new Label(); t.AddToClassList("cfst-history-time");
                    var m = new Label(); m.AddToClassList("cfst-history-mode");
                    var s = new Label(); s.AddToClassList("cfst-history-summary");
                    var st= new Label(); st.AddToClassList("cfst-history-status");
                    row.Add(t); row.Add(m); row.Add(s); row.Add(st);
                    return row;
                };
                _historyList.bindItem = (elem, i) =>
                {
                    if (i >= _records.Count) return;
                    var rec = _records[i];
                    var children = elem.Children();
                    int idx = 0;
                    foreach (var child in children)
                    {
                        if (child is Label lbl)
                        {
                            switch (idx)
                            {
                                case 0: lbl.text = rec.Timestamp.ToString("MM-dd HH:mm:ss"); break;
                                case 1: lbl.text = rec.Protocol; break;
                                case 2: lbl.text = rec.Summary;  break;
                                case 3:
                                    lbl.text = rec.Success ? "成功" : "失败";
                                    lbl.style.color = rec.Success
                                        ? new StyleColor(new UnityEngine.Color(0.25f, 0.72f, 0.31f))
                                        : new StyleColor(new UnityEngine.Color(0.97f, 0.32f, 0.29f));
                                    break;
                            }
                            idx++;
                        }
                    }
                };
                _historyList.selectionChanged += objs =>
                {
                    foreach (var o in objs)
                    {
                        if (o is CfstTestRecord rec) ShowDetail(rec);
                    }
                };
            }

            _btnClear?.RegisterCallback<ClickEvent>(_ =>
            {
                _records.Clear();
                RefreshList();
                HideDetail();
            });

            _btnRerun?.RegisterCallback<ClickEvent>(_ =>
            {
                if (_historyList?.selectedItem is CfstTestRecord rec)
                    OnRerun?.Invoke(rec);
            });

            HideDetail();
        }

        private void RefreshList()
        {
            _historyList?.RefreshItems();
            if (_countBadge != null) _countBadge.text = $"{_records.Count} 条";
        }

        private void ShowDetail(CfstTestRecord rec)
        {
            if (_detailCard != null) _detailCard.style.display = DisplayStyle.Flex;
            if (_detailTime     != null) _detailTime.text     = rec.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            if (_detailProtocol != null) _detailProtocol.text = rec.Protocol;
            if (_detailDuration != null) _detailDuration.text = $"{rec.Duration.TotalSeconds:F1}s";
            if (_detailBestIp   != null) _detailBestIp.text   = string.IsNullOrEmpty(rec.BestIp) ? "—" : rec.BestIp;
            if (_detailSummary  != null) _detailSummary.text  = rec.Summary;
        }

        private void HideDetail()
        {
            if (_detailCard != null) _detailCard.style.display = DisplayStyle.None;
        }
    }
}
