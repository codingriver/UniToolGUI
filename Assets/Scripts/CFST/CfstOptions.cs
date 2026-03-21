// ============================================================
// CfstOptions.cs  —  CloudflareSpeedTest 命令行参数数据结构
// ============================================================
using System;
using System.Collections.Generic;

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

    [Serializable]
    public class HostDomainEntry
    {
        public string Domain { get; set; }
        public int IpRank { get; set; } = 1;
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
        public bool Debug     { get; set; } = false;
        public bool LogToFile { get; set; } = false;  // 是否写日志到文件

        // ── 定时调度（Unity 侧实现，不传给 cfst 命令行）────────────────────
        public bool   ScheduleEnabled { get; set; } = false;
        public string CronExpression  { get; set; }

        // ── Hosts 更新 ───────────────────────────────────────────
        public List<HostDomainEntry> HostsDomains { get; set; } = new List<HostDomainEntry>();
        public string HostsFile    { get; set; }
        public bool   HostsDryRun  { get; set; } = false;

        // ── 运行前钩子 ───────────────────────────────────────────
        // HookPath 可以是脚本(.ps1/.bat/.sh)或可执行程序，根据扩展名自动判断执行方式。
        public bool   PreHookEnabled    { get; set; } = false;
        public string PreHookPath       { get; set; }   // 脚本或程序路径
        public string PreHookArgs       { get; set; }   // 附加参数
        public int    PreHookTimeoutSec { get; set; } = 30;
        public bool   PreHookWait       { get; set; } = true;

        // ── 运行后钩子 ───────────────────────────────────────────
        public bool   PostHookEnabled     { get; set; } = false;
        public string PostHookPath        { get; set; }   // 脚本或程序路径
        public string PostHookArgs        { get; set; }   // 附加参数
        public int    PostHookTimeoutSec  { get; set; } = 30;
        public bool   PostHookOnlySuccess { get; set; } = false;
    }
}
