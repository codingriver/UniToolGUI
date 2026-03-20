// ============================================================
// CfstOptionsExtensions.cs  —  CfstOptions 转命令行参数
// ============================================================
using System;
using System.Text;

namespace CloudflareST.GUI
{
    public static class CfstOptionsExtensions
    {
        /// <summary>
        /// 将 CfstOptions 实例转换为可直接传给 cfst 的命令行参数字符串。
        /// 只输出与默认值不同的参数，保持命令行简洁。
        /// </summary>
        public static string ToArguments(this CfstOptions o)
        {
            var sb = new StringBuilder();

            void Flag(string key) { sb.Append(' '); sb.Append(key); }
            void Num(string key, object val) { sb.Append(' '); sb.Append(key); sb.Append(' '); sb.Append(val); }
            void Str(string key, string val)
            {
                sb.Append(' '); sb.Append(key);
                sb.Append(' '); sb.Append('"'); sb.Append(val); sb.Append('"');
            }

            // ── IP 来源 ──────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(o.IpRanges))
            {
                Str("-ip", o.IpRanges);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(o.IPv4File) && o.IPv4File != "ip.txt")
                    Str("-f", o.IPv4File);
                if (!string.IsNullOrWhiteSpace(o.IPv6File) && o.IPv6File != "ipv6.txt")
                    Str("-f6", o.IPv6File);
            }
            if (o.IpLoadLimit > 0)  Num("-ipn", o.IpLoadLimit);
            if (o.AllIp)             Flag("-allip");

            // ── 测速方式 ─────────────────────────────────────────
            switch (o.PingMode)
            {
                case PingMode.TcPing:  Flag("-tcping");  break;
                case PingMode.Httping: Flag("-httping"); break;
            }
            if (o.ForceIcmp && o.PingMode == PingMode.IcmpAuto)
                Flag("-icmp");

            // ── 延迟测速 ─────────────────────────────────────────
            if (o.PingConcurrency != 200) Num("-n",   o.PingConcurrency);
            if (o.PingCount != 4)         Num("-t",   o.PingCount);
            if (o.LatencyMax != 9999)     Num("-tl",  o.LatencyMax);
            if (o.LatencyMin != 0)        Num("-tll", o.LatencyMin);
            if (Math.Abs(o.PacketLossMax - 1.0) > 1e-9)
                Num("-tlr", o.PacketLossMax.ToString("F2",
                    System.Globalization.CultureInfo.InvariantCulture));

            if (o.PingMode == PingMode.Httping)
            {
                if (o.HttpingCode != 0) Num("-httping-code", o.HttpingCode);
                if (!string.IsNullOrWhiteSpace(o.CfColo)) Str("-cfcolo", o.CfColo);
            }

            // ── 下载测速 ─────────────────────────────────────────
            if (o.DisableDownload)
            {
                Flag("-dd");
            }
            else
            {
                const string defaultUrl = "https://speed.cloudflare.com/__down?bytes=52428800";
                if (!string.IsNullOrWhiteSpace(o.DownloadUrl) && o.DownloadUrl != defaultUrl)
                    Str("-url", o.DownloadUrl);
                if (o.DownloadPort != 443)   Num("-tp", o.DownloadPort);
                if (o.DownloadCount != 10)   Num("-dn", o.DownloadCount);
                if (o.DownloadTimeout != 10) Num("-dt", o.DownloadTimeout);
                if (o.SpeedMin > 1e-9)
                    Num("-sl", o.SpeedMin.ToString("F2",
                        System.Globalization.CultureInfo.InvariantCulture));
            }

            // ── 输出 ─────────────────────────────────────────────
            Str("-o", string.IsNullOrWhiteSpace(o.OutputFile) ? "result.csv" : o.OutputFile);
            if (o.OutputCount > 0 && o.OutputCount != 10)
                Num("-p", o.OutputCount);
            Str("-onlyip", string.IsNullOrWhiteSpace(o.OnlyIpFile) ? "onlyip.txt" : o.OnlyIpFile);
            if (o.Debug) Flag("-debug");

            // 定时调度由 Unity 侧 ScheduleManager 负责，不传给 cfst

            // ── Hosts 更新 ───────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(o.HostsDomains))
            {
                Str("-hosts", o.HostsDomains);
                if (o.HostsIpRank != 1)                          Num("-hosts-ip",   o.HostsIpRank);
                if (!string.IsNullOrWhiteSpace(o.HostsFile))     Str("-hosts-file", o.HostsFile);
                if (o.HostsDryRun)                               Flag("-hosts-dry-run");
            }

            return sb.ToString().TrimStart();
        }

        /// <summary>返回含 exePath 的完整命令字符串，方便日志展示。</summary>
        public static string ToFullCommand(this CfstOptions o, string exePath)
        {
            var args = o.ToArguments();
            return string.IsNullOrWhiteSpace(args)
                ? $"\"{exePath}\""
                : $"\"{exePath}\" {args}";
        }
    }
}
