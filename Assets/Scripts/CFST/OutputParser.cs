// ============================================================
// OutputParser.cs
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloudflareST.GUI
{
    public static class OutputParser
    {
        public static void ParseProgress(string json, AppState state, TestResult result)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                string stage = JStr(json, "stageName");
                switch (stage)
                {
                    case "init":       DoInit(json, state);         break;
                    case "ping":       DoPing(json, state);         break;
                    case "ping_done":  DoPingDone(json, state);     break;
                    case "speed":      DoSpeed(json, state);        break;
                    case "speed_done": DoSpeedDone(json, state);    break;
                    case "done":       DoDone(json, state, result); break;
                    case "error":      DoError(json, state);        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OutputParser] " + ex.Message);
            }
        }

        public static void ParseLog(string line, AppState state) { }

        public static void ApplyDllResults(
            IReadOnlyList<CloudflareST.IPInfo> src,
            AppState state,
            TestResult result)
        {
            var list = new List<IPInfo>();
            if (src != null)
            {
                int rank = 1;
                foreach (var r in src)
                {
                    list.Add(new IPInfo
                    {
                        Rank          = rank++,
                        IP            = r.IP != null ? r.IP.ToString() : "",
                        PacketLoss    = (float)r.LossRate,
                        AvgDelay      = (float)r.DelayMs,
                        DownloadSpeed = (float)(r.DownloadSpeedMbps / 8.0),
                        DataCenter    = r.Colo ?? "",
                        Region        = CloudflareST.ColoProvider.GetColoNameZh(r.Colo),
                    });
                }
            }
            result.SetResults(list);
            state.ResultCount = list.Count;
            state.PassedCount = list.Count;
            if (list.Count > 0)
            {
                state.BestLatency = list[0].AvgDelay;
                float best = 0f;
                foreach (var ip in list)
                    if (ip.DownloadSpeed > best) best = ip.DownloadSpeed;
                state.BestSpeed = best;
            }
        }

        // ── stage handlers ────────────────────────────────────
        private static void DoInit(string j, AppState s)
        {
            int totalStages = JInt(j, "totalStages");
            s.TotalCount    = JInt(j, "totalIps");
            s.TestedCount   = 0;
            s.PassedCount   = 0;
            s.Progress      = 0f;
            // totalStages=4 表示禁用下载测速，=5 表示含下载测速
            s.SpeedDisabled = totalStages <= 4;
            string pm = JStr(j, "pingMode");
            s.PingModeLabel = pm == "httping" ? "HTTPing" :
                              pm == "tcping"  ? "TCPing"  : "ICMP";
            s.StatusText    = s.PingModeLabel + " 延迟测速中...";
        }

        private static void DoPing(string j, AppState s)
        {
            int done   = JInt(j, "done");
            int total  = JInt(j, "total");
            int passed = JInt(j, "passed");
            double pct = JDbl(j, "progressPct");
            s.TestedCount = done;
            s.TotalCount  = total;
            s.PassedCount = passed;
            // ping 阶段：若未禁用下载测速则占 0~50%，否则占 0~100%
            float pingWeight = s.SpeedDisabled ? 1.0f : 0.5f;
            if (s.Progress < 1f)
            {
                s.Progress    = Mathf.Clamp01((float)(pct / 100.0)) * pingWeight;    
            }
            
            // pingMode 字段只在 init 阶段发送，ping 阶段无该字段，使用缓存值
            s.StatusText  = s.PingModeLabel + ": " + done + "/" + total + "  有效:" + passed;
        }

        private static void DoPingDone(string j, AppState s)
        {
            int total  = JInt(j, "total");
            int passed = JInt(j, "passed");
            s.TotalCount  = total;
            s.PassedCount = passed;
            s.TestedCount = total;
            // 若禁用下载测速，ping 完即 100%；否则 50%
            s.Progress    = s.SpeedDisabled ? 1.0f : 0.5f;
            s.StatusText  = "延迟测速完成: " + passed + "/" + total + " 有效";
        }

        private static void DoSpeed(string j, AppState s)
        {
            int done      = JInt(j, "done");
            int total     = JInt(j, "total");
            double pct    = JDbl(j, "progressPct");
            double bestSpd= JDbl(j, "bestSpeedMbps");
            if (s.Progress < 1f)
            {
                s.Progress    = 0.5f + Mathf.Clamp01((float)(pct / 100.0)) * 0.5f;    
            }
            
            s.BestSpeed   = (float)(bestSpd / 8.0);
            s.StatusText  = "下载测速: " + done + "/" + total + "  最高:" + bestSpd.ToString("F1") + "Mbps";
        }

        private static void DoSpeedDone(string j, AppState s)
        {
            double best = JDbl(j, "bestSpeedMbps");
            double avg  = JDbl(j, "avgSpeedMbps");
            if (s.Progress < 1f)
            {
                s.Progress  = 0.95f;    
            }
            s.BestSpeed = (float)(best / 8.0);
            s.StatusText = "下载完成  最高:" + best.ToString("F1") + "Mbps  均速:" + avg.ToString("F1") + "Mbps";
        }

        private static void DoDone(string j, AppState s, TestResult r)
        {
            s.Progress    = 1f;
            s.StatusText  = "已完成";
            s.BestLatency = (float)JDbl(j, "bestDelayMs");
            s.BestSpeed   = (float)(JDbl(j, "bestSpeedMbps") / 8.0);
            var list = ParseResults(j);
            r.SetResults(list);
            s.ResultCount = list.Count;
            s.PassedCount = list.Count;
        }

        private static void DoError(string j, AppState s)
        {
            string code = JStr(j, "errorCode");
            string msg  = JStr(j, "message");
            s.StatusText = "错误: " + code + " - " + msg;
        }

        // ── results array ─────────────────────────────────────
        private static List<IPInfo> ParseResults(string json)
        {
            var list = new List<IPInfo>();
            try
            {
                int arrStart = json.IndexOf("\"results\":", StringComparison.Ordinal);
                if (arrStart < 0) return list;
                int bOpen = json.IndexOf('[', arrStart);
                if (bOpen < 0) return list;
                int bClose = json.LastIndexOf(']');
                if (bClose <= bOpen) return list;

                string content = json.Substring(bOpen + 1, bClose - bOpen - 1).Trim();
                if (string.IsNullOrEmpty(content)) return list;

                int rank = 1;
                foreach (var obj in SplitObjects(content))
                {
                    if (string.IsNullOrWhiteSpace(obj)) continue;
                    string ip = JStr(obj, "ip");
                    if (string.IsNullOrEmpty(ip)) continue;
                    list.Add(new IPInfo
                    {
                        Rank          = rank++,
                        IP            = ip,
                        PacketLoss    = (float)JDbl(obj, "lossRate"),
                        AvgDelay      = (float)JDbl(obj, "delayMs"),
                        DownloadSpeed = (float)(JDbl(obj, "speedMbps") / 8.0),
                        DataCenter    = JStr(obj, "colo") ?? "",
                        Region        = CloudflareST.ColoProvider.GetColoNameZh(JStr(obj, "colo")),
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OutputParser.ParseResults] " + ex.Message);
            }
            return list;
        }

        private static List<string> SplitObjects(string content)
        {
            var result = new List<string>();
            int depth = 0, start = -1;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '{') { if (depth == 0) start = i; depth++; }
                else if (c == '}') { depth--; if (depth == 0 && start >= 0) { result.Add(content.Substring(start, i - start + 1)); start = -1; } }
            }
            return result;
        }

        // ── minimal JSON helpers ──────────────────────────────
        private static string JStr(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            int vs = idx + search.Length;
            while (vs < json.Length && json[vs] == ' ') vs++;
            if (vs >= json.Length) return null;
            if (json[vs] == 'n') return null;
            if (json[vs] == '"')
            {
                int e = json.IndexOf('"', vs + 1);
                if (e < 0) return null;
                return json.Substring(vs + 1, e - vs - 1)
                    .Replace("\\\\", "\\").Replace("\\\"", "\"");
            }
            return null;
        }

        private static int JInt(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return 0;
            int vs = idx + search.Length;
            while (vs < json.Length && json[vs] == ' ') vs++;
            if (vs >= json.Length) return 0;
            int end = vs;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (!int.TryParse(json.Substring(vs, end - vs), out int r)) return 0;
            return r;
        }

        private static double JDbl(string json, string key)
        {
            string search = "\"" + key + "\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return 0.0;
            int vs = idx + search.Length;
            while (vs < json.Length && json[vs] == ' ') vs++;
            if (vs >= json.Length) return 0.0;
            int end = vs;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-' || json[end] == 'E' || json[end] == 'e' || json[end] == '+')) end++;
            if (!double.TryParse(json.Substring(vs, end - vs),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double r)) return 0.0;
            return r;
        }
    }
}
