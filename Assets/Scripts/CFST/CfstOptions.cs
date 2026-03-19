// ============================================================
// CfstOptions.cs  —  CloudflareSpeedTest 命令行参数数据结构
// ============================================================
using System;

namespace CloudflareST.GUI
{
    /// <summary>Ping 测速方式</summary>
    public enum PingMode
    {
        /// <summary>默认：ICMP Ping，不可用时自动降级到 TCPing</summary>
        IcmpAuto,
        /// <summary>手动指定 TCPing (-tcping)</summary>
        TcPing,
        /// <summary>HTTPing (-httping)</summary>
        Httping,
    }

    /// <summary>定时调度模式</summary>
    public enum ScheduleMode
    {
        /// <summary>不启用调度</summary>
        None,
        /// <summary>间隔执行 (-interval)</summary>
        Interval,
        /// <summary>每日定点 (-at)</summary>
        Daily,
        /// <summary>Cron 表达式 (-cron)</summary>
        Cron,
    }

    /// <summary>
    /// CloudflareSpeedTest 全部命令行参数的数据结构。
    /// 对应 cfst 可执行文件支持的所有 -xxx 参数，默认值与程序一致。
    /// </summary>
    public class CfstOptions
    {
        // ── IP 来源 ──────────────────────────────────────────────
        public string IPv4File     { get; set; } = "ip.txt";
        public string IPv6File     { get; set; } = "ipv6.txt";
        public string IpRanges     { get; set; }
        public int    IpLoadLimit  { get; set; } = 0;
        public bool   AllIp        { get; set; } = false;

        // ── 延迟测速 ─────────────────────────────────────────────
        public PingMode PingMode         { get; set; } = PingMode.IcmpAuto;
        public bool     ForceIcmp        { get; set; } = false;
        public int      PingConcurrency  { get; set; } = 200;
        public int      PingCount        { get; set; } = 4;
        public int      LatencyMax       { get; set; } = 9999;
        public int      LatencyMin       { get; set; } = 0;
        public double   PacketLossMax    { get; set; } = 1.0;
        public int      HttpingCode      { get; set; } = 0;
        public string   CfColo           { get; set; }

        // ── 下载测速 ─────────────────────────────────────────────
        public bool   DisableDownload  { get; set; } = false;
        public string DownloadUrl      { get; set; } = "https://speed.cloudflare.com/__down?bytes=52428800";
        public int    DownloadPort     { get; set; } = 443;
        public int    DownloadCount    { get; set; } = 10;
        public int    DownloadTimeout  { get; set; } = 10;
        public double SpeedMin         { get; set; } = 0;

        // ── 输出 ─────────────────────────────────────────────────
        public string OutputFile   { get; set; } = "result.csv";
        public int    OutputCount  { get; set; } = 10;
        public string OnlyIpFile   { get; set; } = "onlyip.txt";

        // ── 调试 ─────────────────────────────────────────────────
        public bool Debug { get; set; } = false;

        // ── 定时调度 ─────────────────────────────────────────────
        public ScheduleMode ScheduleMode    { get; set; } = ScheduleMode.None;
        public int          IntervalMinutes { get; set; } = 0;
        public string       DailyAt         { get; set; }
        public string       CronExpression  { get; set; }
        public string       TimeZone        { get; set; }

        // ── Hosts 更新 ───────────────────────────────────────────
        public string HostsDomains { get; set; }
        public int    HostsIpRank  { get; set; } = 1;
        public string HostsFile    { get; set; }
        public bool   HostsDryRun  { get; set; } = false;
    }
}
