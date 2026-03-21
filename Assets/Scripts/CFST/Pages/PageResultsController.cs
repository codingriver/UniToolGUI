using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UIKit;

namespace CloudflareST.GUI
{
    public class PageResultsController : MonoBehaviour
    {
        private VisualElement _root;
        private CfstOptions   _opts;

        // 概况
        private Label _statTotal;
        private Label _statPassed;
        private Label _statLatency;
        private Label _statSpeed;
        private Label _statTime;
        private Label _statElapsed;

        // 筛选
        private DropdownField _filterDC;
        private IntegerField  _filterLatMax;
        private FloatField    _filterSpeedMin;
        private DropdownField _filterSort;

        // 列表
        private VisualElement _resultsList;
        private Label         _resultsEmpty;

        // Hosts 预览
        private VisualElement _hostsPreviewArea;
        private Label         _hostsPreviewTitle;
        private Label         _hostsStatusIcon;
        private Label         _hostsStatusText;
        private Label         _hostsPreviewContent;

        // 当前视图数据
        private List<IPInfo> _viewList = new List<IPInfo>();

        public void Init(VisualElement root, CfstOptions opts)
        {
            _root = root;
            _opts = opts;

            _statTotal   = root.Q<Label>("stat-total");
            _statPassed  = root.Q<Label>("stat-passed");
            _statLatency = root.Q<Label>("stat-latency");
            _statSpeed   = root.Q<Label>("stat-speed");
            _statTime    = root.Q<Label>("stat-time");
            _statElapsed = root.Q<Label>("stat-elapsed");

            _filterDC       = root.Q<DropdownField>("filter-datacenter");
            _filterLatMax   = root.Q<IntegerField>("filter-latencymax");
            _filterSpeedMin = root.Q<FloatField>("filter-speedmin");
            _filterSort     = root.Q<DropdownField>("filter-sort");

            _resultsList  = root.Q<VisualElement>("results-list");
            _resultsEmpty = root.Q<Label>("results-empty");

            _hostsPreviewArea    = root.Q<VisualElement>("hosts-preview-area");
            _hostsPreviewTitle   = root.Q<Label>("hosts-preview-title");
            _hostsStatusIcon     = root.Q<Label>("hosts-status-icon");
            _hostsStatusText     = root.Q<Label>("hosts-status-text");
            _hostsPreviewContent = root.Q<Label>("hosts-preview-content");

            // 排序选项
            if (_filterSort != null)
            {
                _filterSort.choices = new List<string>
                {
                    "延迟升序", "延迟降序", "速度升序", "速度降序", "丢包率升序"
                };
                _filterSort.value = "延迟升序";
            }

            root.Q<Button>("btn-apply-filter")?.RegisterCallback<ClickEvent>(_ => ApplyFilter());
            root.Q<Button>("btn-copy-first")?.RegisterCallback<ClickEvent>(_ => CopyFirst());
            root.Q<Button>("btn-copy-all")?.RegisterCallback<ClickEvent>(_ => CopyAll());
            root.Q<Button>("btn-export-csv")?.RegisterCallback<ClickEvent>(_ => ExportCsv());
            root.Q<Button>("btn-rerun")?.RegisterCallback<ClickEvent>(_ =>
            {
                var mw = FindObjectOfType<MainWindowController>();
                mw?.NavigateTo(0);
                // 延迟一帧再触发 Start（避免在 Click 回调里直接切页+启动）
                root.schedule.Execute(() =>
                {
                    // 通过广播事件方式解耦
                    UnityEngine.Events.UnityEvent ev = new UnityEngine.Events.UnityEvent();
                }).StartingIn(100);
            });

            TestResult.Instance.OnResultUpdated += RefreshResults;
        }

        private void OnDestroy()
        {
            TestResult.Instance.OnResultUpdated -= RefreshResults;
        }

        public void RefreshResults()
        {
            var s = AppState.Instance;
            var r = TestResult.Instance;

            // 概况
            if (_statTotal  != null) _statTotal.text  = s.TotalCount.ToString();
            if (_statPassed != null) _statPassed.text = s.PassedCount.ToString();
            if (r.IpList.Count > 0)
            {
                var best = r.IpList.OrderBy(x => x.AvgDelay).First();
                if (_statLatency != null) _statLatency.text = $"{best.AvgDelay:F0} ms";
                var fastest = r.IpList.OrderByDescending(x => x.DownloadSpeed).First();
                if (_statSpeed != null)
                    _statSpeed.text = fastest.DownloadSpeed > 0
                        ? $"{fastest.DownloadSpeed:F2} MB/s" : "-";
            }
            if (_statTime    != null) _statTime.text    = s.FinishTime > System.DateTime.MinValue ? s.FinishTime.ToString("HH:mm:ss") : "-";
            if (_statElapsed != null) _statElapsed.text = s.Elapsed > System.TimeSpan.Zero ? s.Elapsed.ToString(@"mm\:ss") : "-";

            // 更新地区筛选列表
            if (_filterDC != null)
            {
                var dcs = new List<string> { "全部" };
                dcs.AddRange(r.IpList.Select(x => x.DataCenter).Distinct().OrderBy(x => x));
                _filterDC.choices = dcs;
                if (!dcs.Contains(_filterDC.value)) _filterDC.value = "全部";
            }

            ApplyFilter();
            RefreshHostsPreview();
        }

        private void ApplyFilter()
        {
            var src = TestResult.Instance.IpList.ToList();

            // 地区筛选
            string dc = _filterDC?.value;
            if (!string.IsNullOrEmpty(dc) && dc != "全部")
                src = src.Where(x => x.DataCenter == dc).ToList();

            // 延迟上限
            int latMax = _filterLatMax?.value ?? 0;
            if (latMax > 0) src = src.Where(x => x.AvgDelay <= latMax).ToList();

            // 速度下限
            float spdMin = _filterSpeedMin?.value ?? 0f;
            if (spdMin > 0) src = src.Where(x => x.DownloadSpeed >= spdMin).ToList();

            // 排序
            switch (_filterSort?.value)
            {
                case "延迟降序":   src = src.OrderByDescending(x => x.AvgDelay).ToList();      break;
                case "速度升序":   src = src.OrderBy(x => x.DownloadSpeed).ToList();           break;
                case "速度降序":   src = src.OrderByDescending(x => x.DownloadSpeed).ToList(); break;
                case "丢包率升序": src = src.OrderBy(x => x.PacketLoss).ToList();             break;
                default:           src = src.OrderBy(x => x.AvgDelay).ToList();               break;
            }

            _viewList = src;
            RebuildTable();
        }

        private void RebuildTable()
        {
            if (_resultsList == null) return;
            _resultsList.Clear();

            bool empty = _viewList.Count == 0;
            if (_resultsEmpty != null)
                _resultsEmpty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _viewList.Count; i++)
            {
                var ip  = _viewList[i];
                var row = BuildRow(i + 1, ip);
                _resultsList.Add(row);
            }

            // 复制第1名按钮状态
            var btnFirst = _root?.Q<Button>("btn-copy-first");
            btnFirst?.SetEnabled(_viewList.Count > 0);
        }

        private VisualElement BuildRow(int rank, IPInfo ip)
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");

            row.Add(MakeCell(rank.ToString(),                         "col-rank"));
            row.Add(MakeCell(ip.IP,                                   "col-ip"));
            row.Add(MakeCell($"{ip.PacketLoss * 100:F0}%",            "col-loss"));
            row.Add(MakeCell($"{ip.AvgDelay:F0} ms",                  "col-delay"));
            row.Add(MakeCell(ip.DownloadSpeed > 0
                ? $"{ip.DownloadSpeed:F2} MB/s" : "-",               "col-speed"));
            row.Add(MakeCell(ip.DataCenter,                           "col-dc"));
            row.Add(MakeCell(ip.Region,                               "col-region"));

            // 右键：显示上下文菜单
            row.RegisterCallback<ContextClickEvent>(e =>
            {
                e.StopPropagation();
                // 将屏幕坐标转为面板本地坐标
                Vector2 localPos = _root != null
                    ? _root.WorldToLocal(new Vector2(e.mousePosition.x, e.mousePosition.y))
                    : new Vector2(e.mousePosition.x, e.mousePosition.y);
                ShowContextMenu(row, ip, localPos);
            });
            // 单击高亮
            row.RegisterCallback<ClickEvent>(_ =>
            {
                foreach (var r in _resultsList.Children())
                    r.RemoveFromClassList("table-row--selected");
                row.AddToClassList("table-row--selected");
            });

            return row;
        }

        // ── 右键菜单 ──────────────────────────────────────
        private VisualElement _contextMenu;
        private VisualElement _contextTarget;

        private void ShowContextMenu(VisualElement anchor, IPInfo ip, Vector2 pos)
        {
            HideContextMenu();

            _contextMenu = new VisualElement();
            _contextMenu.style.position   = Position.Absolute;
            _contextMenu.style.left       = pos.x;
            _contextMenu.style.top        = pos.y;
            _contextMenu.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.13f, 0.17f, 0.24f));
            _contextMenu.style.borderTopWidth    = 1;
            _contextMenu.style.borderBottomWidth = 1;
            _contextMenu.style.borderLeftWidth   = 1;
            _contextMenu.style.borderRightWidth  = 1;
            _contextMenu.style.borderTopColor    = new StyleColor(new UnityEngine.Color(0.18f, 0.23f, 0.33f));
            _contextMenu.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0.18f, 0.23f, 0.33f));
            _contextMenu.style.borderLeftColor   = new StyleColor(new UnityEngine.Color(0.18f, 0.23f, 0.33f));
            _contextMenu.style.borderRightColor  = new StyleColor(new UnityEngine.Color(0.18f, 0.23f, 0.33f));
            _contextMenu.style.borderTopLeftRadius     = 5;
            _contextMenu.style.borderTopRightRadius    = 5;
            _contextMenu.style.borderBottomLeftRadius  = 5;
            _contextMenu.style.borderBottomRightRadius = 5;
            _contextMenu.style.paddingTop    = 4;
            _contextMenu.style.paddingBottom = 4;
            _contextMenu.style.minWidth = 140;
            _contextMenu.pickingMode = PickingMode.Position;

            // 菜单项：复制 IP
            var btnCopy = new Button(() =>
            {
                NativePlatform.SetClipboard(ip.IP);
                ToastManager.Success("已复制: " + ip.IP);
                HideContextMenu();
            });
            btnCopy.text = "复制 IP";
            btnCopy.style.backgroundColor = new StyleColor(UnityEngine.Color.clear);
            btnCopy.style.borderTopWidth = btnCopy.style.borderBottomWidth =
                btnCopy.style.borderLeftWidth = btnCopy.style.borderRightWidth = 0;
            btnCopy.style.color    = new StyleColor(new UnityEngine.Color(0.91f, 0.91f, 0.95f));
            btnCopy.style.fontSize = 12;
            btnCopy.style.paddingTop = btnCopy.style.paddingBottom = 5;
            btnCopy.style.paddingLeft = btnCopy.style.paddingRight = 12;
            btnCopy.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            _contextMenu.Add(btnCopy);

            // 菜单项：复制 IP:端口（仅IPv4显示）
            var btnCopyPort = new Button(() =>
            {
                string val = ip.IP.Contains(":") ? "[" + ip.IP + "]:443" : ip.IP + ":443";
                NativePlatform.SetClipboard(val);
                ToastManager.Success("已复制: " + val);
                HideContextMenu();
            });
            btnCopyPort.text = "复制 IP:443";
            btnCopyPort.style.backgroundColor = new StyleColor(UnityEngine.Color.clear);
            btnCopyPort.style.borderTopWidth = btnCopyPort.style.borderBottomWidth =
                btnCopyPort.style.borderLeftWidth = btnCopyPort.style.borderRightWidth = 0;
            btnCopyPort.style.color    = new StyleColor(new UnityEngine.Color(0.91f, 0.91f, 0.95f));
            btnCopyPort.style.fontSize = 12;
            btnCopyPort.style.paddingTop = btnCopyPort.style.paddingBottom = 5;
            btnCopyPort.style.paddingLeft = btnCopyPort.style.paddingRight = 12;
            btnCopyPort.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            _contextMenu.Add(btnCopyPort);

            // 添加到根容器
            var pageRoot = _root?.panel?.visualTree?.Q("page-results") ?? _root;
            pageRoot?.Add(_contextMenu);
            _contextTarget = anchor;

            // 点击其他地方关闭
            _root?.RegisterCallback<PointerDownEvent>(OnPointerDownOutside, TrickleDown.TrickleDown);
        }

        private void HideContextMenu()
        {
            if (_contextMenu == null) return;
            _contextMenu.RemoveFromHierarchy();
            _contextMenu = null;
            _contextTarget = null;
            _root?.UnregisterCallback<PointerDownEvent>(OnPointerDownOutside, TrickleDown.TrickleDown);
        }

        private void OnPointerDownOutside(PointerDownEvent e)
        {
            if (_contextMenu == null) return;
            // 如果点击在菜单外则关闭
            if (!_contextMenu.worldBound.Contains(e.position))
                HideContextMenu();
        }

        private static Label MakeCell(string text, string colClass)
        {
            var l = new Label(text);
            l.AddToClassList("table-cell");
            l.AddToClassList(colClass);
            return l;
        }

        private void CopyFirst()
        {
            if (_viewList.Count == 0) return;
            string ip = _viewList[0].IP;
            NativePlatform.SetClipboard(ip);
            ToastManager.Success("已复制: " + ip);
        }

        private void CopyAll()
        {
            if (_viewList.Count == 0) return;
            var ips = string.Join("\n", _viewList.Select(x => x.IP));
            NativePlatform.SetClipboard(ips);
            ToastManager.Success("已复制 " + _viewList.Count + " 个 IP");
        }

        private void ExportCsv()
        {
            string filter = NativePlatform.FileDialog.CreateFilter("CSV 文件(*.csv)", "*.csv");
            string path   = NativePlatform.FileDialog.SaveFilePanel("导出 CSV", filter, "csv", null,
                System.IO.Path.GetFileNameWithoutExtension(_opts.OutputFile ?? "result"));
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("IP,丢包率,平均延迟,下载速度,地区码,地区名");
                foreach (var ip in _viewList)
                    sb.AppendLine($"{ip.IP},{ip.PacketLoss * 100:F0}%,{ip.AvgDelay:F0},{ip.DownloadSpeed:F2},{ip.DataCenter},{ip.Region}");
                System.IO.File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
                ToastManager.Success("已导出到: " + System.IO.Path.GetFileName(path));
            }
            catch (System.Exception ex)
            {
                ToastManager.Error("导出失败: " + ex.Message);
            }
        }

        private void RefreshHostsPreview()
        {
            var r = TestResult.Instance;
            bool hostsEnabled = _opts.HostsDomains != null && _opts.HostsDomains.Count > 0;

            if (_hostsPreviewArea == null) return;
            _hostsPreviewArea.style.display = hostsEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hostsEnabled) return;

            if (_hostsPreviewTitle != null)
                _hostsPreviewTitle.text = _opts.HostsDryRun
                    ? "Hosts 写入预览（仅预览，未实际写入）"
                    : (r.HostsWriteSuccess ? "Hosts 已写入" : "Hosts 写入预览");

            if (_hostsStatusIcon != null && _hostsStatusText != null)
            {
                if (r.HostsWriteSuccess)
                {
                    _hostsStatusIcon.text  = "✓";
                    _hostsStatusIcon.style.color = new StyleColor(new Color(0.24f, 0.86f, 0.52f));
                    _hostsStatusText.text  = "已成功写入系统 Hosts";
                }
                else if (r.HostsPermissionDenied)
                {
                    _hostsStatusIcon.text  = "!";
                    _hostsStatusIcon.style.color = new StyleColor(new Color(1f, 0.8f, 0.27f));
                    _hostsStatusText.text  = "权限不足，已输出至 hosts-pending.txt，可手动合并";
                }
            }

            if (_hostsPreviewContent != null)
                _hostsPreviewContent.text = string.Join("\n", r.HostsWriteLines);
        }
    }
}
