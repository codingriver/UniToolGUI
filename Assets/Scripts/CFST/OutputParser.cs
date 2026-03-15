using System.Text.RegularExpressions;

namespace CloudflareST.GUI
{
    /// <summary>
    /// 解析 cfst stdout 输出行，更新 AppState 和 TestResult。
    /// </summary>
    public static class OutputParser
    {
        // 示例行: 进度: 486/2000 (24.30%)
        private static readonly Regex _progressRx =
            new Regex(@"(\d+)/(\d+)\s*\((\d+\.?\d*)%\)",
                RegexOptions.Compiled);

        // 示例行: 下载测速中...
        private static readonly Regex _phaseRx =
            new Regex(@"(延迟测速|下载测速|正在|已完成)",
                RegexOptions.Compiled);

        // 结果行: 104.21.56.1, 0.00, 52, 85.23, HKG, 香港
        private static readonly Regex _resultRx =
            new Regex(@"^\s*(\d+\.\d+\.\d+\.\d+[^,]*)\s*,\s*(\d+\.?\d*)\s*,\s*(\d+)\s*,\s*(\d+\.?\d*)\s*,\s*(\w+)\s*,\s*(.*?)\s*$",
                RegexOptions.Compiled);

        private static int _resultRank = 0;

        public static void Parse(string line, AppState state, TestResult result)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            // 进度解析
            var pm = _progressRx.Match(line);
            if (pm.Success)
            {
                if (int.TryParse(pm.Groups[1].Value, out int tested))
                    state.TestedCount = tested;
                if (int.TryParse(pm.Groups[2].Value, out int total))
                    state.TotalCount  = total;
                if (float.TryParse(pm.Groups[3].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float pct))
                    state.Progress = pct / 100f;
            }

            // 阶段文字
            var ph = _phaseRx.Match(line);
            if (ph.Success)
                state.StatusText = line.Trim().Length > 20
                    ? line.Trim().Substring(0, 20) : line.Trim();

            // 结果行
            var rm = _resultRx.Match(line);
            if (rm.Success)
            {
                var ip = new IPInfo
                {
                    Rank   = ++_resultRank,
                    IP     = rm.Groups[1].Value.Trim(),
                    PacketLoss = float.TryParse(rm.Groups[2].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float loss) ? loss : 0f,
                    AvgDelay = float.TryParse(rm.Groups[3].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float delay) ? delay : 0f,
                    DownloadSpeed = float.TryParse(rm.Groups[4].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float spd) ? spd : 0f,
                    DataCenter = rm.Groups[5].Value.Trim(),
                    Region     = rm.Groups[6].Value.Trim(),
                };

                // 更新最优
                if (state.BestLatency < 0 || ip.AvgDelay < state.BestLatency)
                    state.BestLatency = ip.AvgDelay;
                if (state.BestSpeed  < 0 || ip.DownloadSpeed > state.BestSpeed)
                    state.BestSpeed   = ip.DownloadSpeed;

                // 追加到结果（简单做法：重建列表；生产中可用 Add 方法）
                var list = new System.Collections.Generic.List<IPInfo>(
                    result.IpList) { ip };
                result.SetResults(list);
                state.PassedCount = list.Count;
                state.ResultCount = list.Count;
            }
        }

        public static void Reset() => _resultRank = 0;
    }
}
